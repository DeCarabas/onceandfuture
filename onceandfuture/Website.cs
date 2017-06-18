namespace OnceAndFuture
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using ImageSharp;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Scrypt;
    using Serilog;
    using Microsoft.AspNetCore.ResponseCompression;

    public class Fault
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("details")]
        public string Details { get; set; }

        [JsonProperty("data")]
        public object Data { get; set; }

        public override string ToString() => $"Fault: {Code}: {Details}";
    }

    public class FaultException : Exception
    {
        public FaultException(HttpStatusCode statusCode, Fault fault) : base(fault.ToString())
        {
            StatusCode = statusCode;
            Fault = fault;
        }

        public HttpStatusCode StatusCode { get; }

        public Fault Fault { get; }

        public static FaultException AccessDenied(string message = null) =>
            new FaultException(HttpStatusCode.Forbidden, new Fault
            {
                Status = "error",
                Code = "accessdenied",
                Details = message ?? "User is not authorized to access this resource.",
            });

        public static FaultException DecodingError(string message) =>
            new FaultException(HttpStatusCode.BadRequest, new Fault
            {
                Status = "error",
                Code = "badrequest",
                Details = "Error decoding request: " + message,
            });

        public static FaultException NoFeed(string url) =>
            new FaultException(HttpStatusCode.BadRequest, new Fault
            {
                Status = "error",
                Code = "nofeed",
                Details = "No feed found @ " + url
            });

        internal static Exception NoRiver(string id) =>
            new FaultException(HttpStatusCode.BadRequest, new Fault
            {
                Status = "error",
                Code = "noriver",
                Details = "No river found @ " + id
            });

        public static Exception DuplicateName(string name) =>
            new FaultException(HttpStatusCode.BadRequest, new Fault
            {
                Status = "error",
                Code = "duplicatename",
                Details = "You already have a river named " + name,
            });

        public static Exception AmbiguousFeedUrl(string url, Dictionary<string, string> foundFeeds) =>
            new FaultException(HttpStatusCode.BadRequest, new Fault
            {
                Status = "error",
                Code = "ambigousfeedurl",
                Details = "There are multiple feeds @ " + url,
                Data = foundFeeds,
            });
    }

    public static class CustomMiddlewareExtensions
    {
        public static IApplicationBuilder UseFaultHandler(this IApplicationBuilder builder)
        {
            builder.Use(async (ctxt, next) =>
            {
                try
                {
                    await next();
                }
                catch (FaultException fe)
                {
                    if (!ctxt.Response.HasStarted)
                    {
                        ctxt.Response.StatusCode = (int)fe.StatusCode;

                        byte[] resp = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fe.Fault));
                        ctxt.Response.ContentLength = resp.Length;
                        ctxt.Response.ContentType = "application/json";
                        await ctxt.Response.Body.WriteAsync(resp, 0, resp.Length);
                    }
                }
            });
            return builder;
        }
    }

    public sealed class SecretToken : IEquatable<SecretToken>
    {
        const int CookieSize = 32;
        static readonly ScryptEncoder Encoder = new ScryptEncoder();
        static readonly RandomNumberGenerator RandomGenerator = RandomNumberGenerator.Create();

        readonly byte[] bytes;

        SecretToken(byte[] bytes)
        {
            if (bytes == null) { throw new ArgumentNullException(nameof(bytes)); }
            this.bytes = bytes;
        }

        public static SecretToken Empty { get; } = new SecretToken(Array.Empty<byte>());

        public static SecretToken Create()
        {
            byte[] bytes = new byte[CookieSize];
            RandomGenerator.GetBytes(bytes);
            return new SecretToken(bytes);
        }

        public static bool TryParse(string input, out SecretToken token)
        {
            token = null;
            try
            {
                token = new SecretToken(Convert.FromBase64String(input));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public string Encrypt()
        {
            if (this.bytes.Length == 0) { return String.Empty; }
            return Encoder.Encode(ToString());
        }

        public bool Equals(SecretToken other)
        {
            if (ReferenceEquals(other, null)) { return false; }
            if (other.bytes.Length != this.bytes.Length) { return false; }
            for (var i = 0; i < this.bytes.Length; i++)
            {
                if (other.bytes[i] != this.bytes[i]) { return false; }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SecretToken);
        }

        public bool EqualsEncrypted(string encrypted)
        {
            if (this.bytes.Length == 0) { return false; }
            if (String.IsNullOrEmpty(encrypted)) { return false; }
            return Encoder.Compare(ToString(), encrypted);
        }

        public override int GetHashCode() => (int)Murmur3.Hash32(this.bytes);

        public override string ToString() => Convert.ToBase64String(this.bytes);

        public static bool operator ==(SecretToken a, SecretToken b)
        {
            if (ReferenceEquals(a, null) && ReferenceEquals(b, null)) { return true; }
            if (ReferenceEquals(a, null)) { return false; }
            return a.Equals(b);
        }

        public static bool operator !=(SecretToken a, SecretToken b) => !(a == b);
    }

    public class AuthenticationManager
    {
        const string CookieName = "onceandfuture-feed";
        const int MaxConcurrentSessions = 10;
        static readonly ScryptEncoder Scrypt = new ScryptEncoder();

        readonly ConcurrentDictionary<string, LoginCookieCache[]> loginCache =
            new ConcurrentDictionary<string, LoginCookieCache[]>();
        readonly UserProfileStore profileStore;

        public AuthenticationManager(UserProfileStore profileStore)
        {
            // TODO: Cache scrubber.
            this.profileStore = profileStore;
        }

        bool ParseCookie(string cookie, out string user, out SecretToken token)
        {
            user = null;
            token = SecretToken.Empty;

            string[] parts = cookie.Split(new[] { ',' }, 2);
            if (parts.Length != 2) { return false; }
            if (!SecretToken.TryParse(parts[0], out token)) { return false; }
            user = parts[1];
            return true;
        }

        static string MakeCookie(string user, SecretToken token) => $"{token},{user}";

        static bool CheckPassword(string password, string encrypted) => Scrypt.Compare(password, encrypted);
        public static string EncryptPassword(string password) => Scrypt.Encode(password);

        bool ValidateAgainstLoginCache(SecretToken token, LoginCookieCache[] cache)
        {
            for (int i = 0; i < cache.Length; i++)
            {
                // Check plaintext values; we might have seen this before.
                if (cache[i].Plaintext == token)
                {
                    // We found it; even if it's expired don't bother looping through the encrypted tokens.
                    return (cache[i].ExpireAt >= DateTimeOffset.UtcNow);
                }
            }

            for (int i = 0; i < cache.Length; i++)
            {
                // Check against cyphertext; this is slow but faster than hitting S3 again.
                if (token.EqualsEncrypted(cache[i].Token))
                {
                    // Hooray! Make sure the cache is updated.
                    cache[i].Plaintext = token;
                    return (cache[i].ExpireAt >= DateTimeOffset.UtcNow);
                }
            }

            // Neither encrypted nor plaintext, no access.
            return false;
        }

        static LoginCookieCache[] RebuildCache(LoginCookieCache[] existingCache, IList<LoginCookie> validLogins)
        {
            int existingPointer = 0;
            existingCache = existingCache ?? Array.Empty<LoginCookieCache>();

            // Very silly, but these things are always in a predictable order so scanning to rebuild should be quick.
            // (Trying to keep from throwing away the scrypt work every time we get a new login; the scrypt work is
            // relatively slow.)
            LoginCookieCache[] newCache = new LoginCookieCache[validLogins.Count];
            for (int i = 0; i < validLogins.Count; i++)
            {
                // Find the matching entry...
                while (existingPointer < existingCache.Length)
                {
                    if (String.CompareOrdinal(existingCache[existingPointer].Token, validLogins[i].Id) == 0)
                    {
                        newCache[i].Plaintext = existingCache[existingPointer].Plaintext;
                        break;
                    }
                    existingPointer++;
                }

                newCache[i].ExpireAt = validLogins[i].ExpireAt;
                newCache[i].Token = validLogins[i].Id;
            }

            return newCache;
        }

        public async Task<string> GetAuthenticatedUser(HttpContext context)
        {
            // Has somebody already asked me for this request?
            object alreadyUser;
            if (context.Items.TryGetValue(CookieName, out alreadyUser)) { return (string)alreadyUser; }

            // Did I get a cookie?
            string cookie;
            if (!context.Request.Cookies.TryGetValue(CookieName, out cookie))
            {
                Serilog.Log.Debug("Auth: No cookie found {cookie}", cookie);
                return null;
            }

            // Is it really a valid cookie?
            string user;
            SecretToken token;
            if (!ParseCookie(cookie, out user, out token))
            {
                Serilog.Log.Debug("Auth: Unable to parse cookie {cookie}", cookie);
                return null;
            }

            // Is it valid? Check the cache.
            LoginCookieCache[] cachedCookies;
            if (this.loginCache.TryGetValue(user, out cachedCookies))
            {
                if (ValidateAgainstLoginCache(token, cachedCookies))
                {
                    Serilog.Log.Debug("Auth: Login session {token} found in cache", token);
                    context.Items.Add(CookieName, user);
                    return user;
                }
            }

            // Not in the cache; pull the user profile. (Might have signed in on another node.)
            UserProfile profile = await this.profileStore.GetProfileFor(user);

            // Rebuild the cache from the profile. Note that this rebuilds the cyphertext.
            // NOTE: Only bother to go down this path if there are *any* valid logins. That way somebody can't spam my
            //       server with fake user names and waste space in my cache with bogus entries.
            //
            // TODO: Possible abuse: I can send requests at a node with a valid user name and a garbage token and
            //       cause that node to hit the profile store over and over and over and over. Maybe we should rate-
            //       limit this stuff?
            if (profile.Logins.Count > 0)
            {
                cachedCookies = RebuildCache(cachedCookies, profile.Logins);
                this.loginCache[user] = cachedCookies;
                if (ValidateAgainstLoginCache(token, cachedCookies))
                {
                    Serilog.Log.Debug("Auth: Login session {token} found in cache after refresh", token);
                    context.Items.Add(CookieName, user);
                    return user;
                }
            }

            // No profile, or no matching cookie-- not authn.
            Serilog.Log.Debug("Auth: Login session {token} not found", token);
            return null;
        }

        public async Task<bool> ValidateLogin(HttpContext context, string user, string password)
        {
            UserProfile profile = await this.profileStore.GetProfileFor(user);
            if (profile.Password == null) { return false; } // No password: disabled
            if (!CheckPassword(password, profile.Password)) { return false; }

            // TODO: Fix this login duration. :P
            SecretToken token = SecretToken.Create();
            var newLogin = new LoginCookie(token.Encrypt(), DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30));

            // Insert the new session at the front (because it's most likely to be tried first), and try to keep the
            // number of sessions bounded.
            List<LoginCookie> validLogins = profile.Logins.Where(c => c.ExpireAt >= DateTimeOffset.UtcNow).ToList();
            validLogins.Insert(0, newLogin);
            if (validLogins.Count > MaxConcurrentSessions)
            {
                validLogins.RemoveRange(MaxConcurrentSessions, validLogins.Count);
            }

            // State mutation order below is important. (Durable store, then cache, then cookie.)
            var newProfile = profile.With(logins: validLogins);
            await this.profileStore.SaveProfileFor(user, newProfile);

            // Now update the cache.
            LoginCookieCache[] cache;
            if (!this.loginCache.TryGetValue(user, out cache)) { cache = Array.Empty<LoginCookieCache>(); }
            LoginCookieCache[] newCache = new LoginCookieCache[cache.Length + 1];
            Array.Copy(cache, 0, newCache, 1, cache.Length);
            newCache[0].ExpireAt = newLogin.ExpireAt;
            newCache[0].Token = newLogin.Id;
            newCache[0].Plaintext = token;
            this.loginCache[user] = newCache;

            // Now store the cookie.
            context.Response.Cookies.Append(CookieName, MakeCookie(user, token));
            return true;
        }

        public void SignOut(HttpContext context)
        {
            context.Response.Cookies.Delete(CookieName);
        }

        struct LoginCookieCache
        {
            public SecretToken Plaintext;
            public string Token;
            public DateTimeOffset ExpireAt;
        }
    }    

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class EnforceAuthenticationAttribute : Attribute, IAsyncAuthorizationFilter
    {
        readonly string userParameterName;

        public EnforceAuthenticationAttribute(string userParameterName)
        {
            this.userParameterName = userParameterName;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            string user = (string)context.RouteData.Values[this.userParameterName];

            var authmgr = context.HttpContext.RequestServices.GetRequiredService<AuthenticationManager>();
            string authnUser = await authmgr.GetAuthenticatedUser(context.HttpContext);

            if (authnUser == null || !String.Equals(user, authnUser, StringComparison.OrdinalIgnoreCase))
            {
                throw FaultException.AccessDenied();
            }
        }
    }
        
    public class AppController : Controller
    {
        readonly AuthenticationManager authenticationManager;

        public AppController(AuthenticationManager authenticationManager)
        {
            this.authenticationManager = authenticationManager;
        }

        [HttpGet("/")]
        public async Task<IActionResult> Index()
        {
            string user = await this.authenticationManager.GetAuthenticatedUser(HttpContext);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }
            else
            {
                return RedirectToAction(nameof(App), new { user });
            }
        }

        [HttpGet("/feed/{user}")]
        public async Task<IActionResult> App(string user)
        {
            string authnUser = await this.authenticationManager.GetAuthenticatedUser(HttpContext);
            if (!string.Equals(authnUser, user, StringComparison.Ordinal))
            {
                return RedirectToAction(nameof(Login));
            }

            return PhysicalFile(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "main.html"),
                "text/html");
        }

        [HttpGet("/login")]
        public IActionResult Login() => PhysicalFile(
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "login.html"),
            "text/html");

        [HttpPost("/login")]
        public async Task<IActionResult> ProcessLogin()
        {
            IFormCollection form = await Request.ReadFormAsync(HttpContext.RequestAborted);
            string user = form["username"];
            string password = form["password"];

            var authnManager = HttpContext.RequestServices.GetRequiredService<AuthenticationManager>();
            bool isAuthenticated = await authnManager.ValidateLogin(HttpContext, user, password);
            if (isAuthenticated)
            {
                return RedirectToAction(nameof(App), new { user });
            }
            else
            {
                return RedirectToAction(nameof(Login));
            }
        }
    }

    public class AsyncProgressActionResult : IActionResult
    {
        readonly Task[] tasks;
        readonly string[] descriptions;
        readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);

        public AsyncProgressActionResult(Task[] tasks, string[] descriptions)
        {
            this.tasks = tasks;
            this.descriptions = descriptions;

            Debug.Assert(
                tasks.Length == descriptions.Length, 
                "Task and description arrays must be the same");

            var callback = new Action<Task>(OnTaskCompleted);
            for (int i = 0; i < this.tasks.Length; i++)
            {
                this.tasks[i].ContinueWith(callback);
            }
        }

        public bool AllComplete => this.tasks.All(t => t?.IsCompleted ?? false);

        public int GetCompletionPercent()
        {
            float total = this.tasks.Length;
            if (Math.Abs(total) <= float.Epsilon) { return 100; }

            float complete = 0.0f;
            for (int i = 0; i < this.tasks.Length; i++)
            {
                if (this.tasks[i]?.IsCompleted ?? false) { complete += 1.0f; }
            }
            return (int)Math.Max(100.0f * complete / total, 1.0f);
        }

        public string GetStatusMessage()
        {
            for (int i = 0; i < this.tasks.Length; i++)
            {
                if (!this.tasks[i].IsCompleted) { return this.descriptions[i]; }
            }
            return "Done.";
        }

        string GetTaskDescription(Task task)
        {
            int index = Array.IndexOf(this.tasks, task);
            return (index >= 0) ? this.descriptions[index] : "??";
        }

        void OnTaskCompleted(Task task)
        {
            Log.AsyncProgressTaskComplete(task, GetTaskDescription(task));
            this.semaphore.Release();
        }

        async Task UpdateProgress(TextWriter writer)
        {
            int progress = GetCompletionPercent();
            string message = GetStatusMessage();

            string line = String.Format("{0}|{1}", progress, message);
            await writer.WriteLineAsync(line);
            await writer.FlushAsync();
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.OK;
            context.HttpContext.Response.ContentType = "application/octet-stream";
            using (var writer = new StreamWriter(context.HttpContext.Response.Body, Encoding.UTF8))
            {
                while (!AllComplete)
                {
                    // Wait for progress to come in, or one second, whichever comes first.
                    await this.semaphore.WaitAsync(TimeSpan.FromSeconds(1), context.HttpContext.RequestAborted);
                    await UpdateProgress(writer);
                }

                // One last update, make sure the 100% gets sent.
                await UpdateProgress(writer);
            }
        }
    }

    [EnforceAuthentication(userParameterName: "user")]
    public class ApiController : Controller
    {
        readonly UserProfileStore profileStore;
        readonly AggregateRiverStore aggregateStore;
        readonly RiverFeedParser feedParser;
        readonly RiverFeedStore feedStore;

        public ApiController(
            UserProfileStore profileStore,
            AggregateRiverStore aggregateStore,
            RiverFeedParser feedParser,
            RiverFeedStore feedStore)
        {
            this.profileStore = profileStore;
            this.aggregateStore = aggregateStore;
            this.feedParser = feedParser;
            this.feedStore = feedStore;
        }

        [HttpGet("/api/v1/user/{user}")]
        public async Task<IActionResult> GetRiverList(string user)
        {
            UserProfile profile = await this.profileStore.GetProfileFor(user);
            var rivers = (from r in profile.Rivers
                          select new
                          {
                              name = r.Name,
                              id = r.Id,
                              url = Url.Action(nameof(GetRiver), new { user, id = r.Id }),
                          }).ToArray();
            return Json(new { rivers });
        }

        [HttpPost("/api/v1/user/{user}")]
        public async Task<IActionResult> CreateOrRestoreRiver(string user)
        {
            River existing = null;
            var requestBody = await ReadRequest<CreateOrRestoreRequest>();
            if (requestBody.Id != null)
            {
                existing = await LoadAggregate(user, requestBody.Id);
            }

            UserProfile profile = await this.profileStore.GetProfileFor(user);

            string name = requestBody.Name ?? Util.RiverName(profile.Rivers);
            RiverDefinition river = profile.Rivers.FirstOrDefault(
                rd => String.Equals(rd.Name, name, StringComparison.OrdinalIgnoreCase));
            if (river == null)
            {
                var newRiver = new RiverDefinition(name, requestBody.Id ?? Util.MakeID());
                var newProfile = profile.With(rivers: profile.Rivers.Add(newRiver));

                await this.profileStore.SaveProfileFor(user, newProfile);

                profile = newProfile;
            }

            var rivers = (from r in profile.Rivers
                          select new
                          {
                              name = r.Name,
                              id = r.Id,
                              url = Url.Action(nameof(GetRiver), new { user, id = r.Id }),
                          }).ToArray();
            return Json(new { status = "ok", rivers, existing });
        }

        [HttpPost("/api/v1/user/{user}/refresh_all")]
        public async Task<IActionResult> PostRefreshAll(string user)
        {
            UserProfile profile = await this.profileStore.GetProfileFor(user);

            Uri[] feeds = profile.Rivers.SelectMany(rd => rd.Feeds).Distinct().ToArray();
            Task<River>[] feedTasks = UpdateAllFeeds(feeds);
            Task allFeedsTask = Task.WhenAll(feedTasks);
            Task<River>[] riverTasks = RefreshAllAggregates(user, profile, Task.WhenAll(feedTasks));

            List<Task> allTasks = new List<Task>(feedTasks.Length + riverTasks.Length);
            List<string> allDescriptions = new List<String>(allTasks.Count);
            for (int i = 0; i < feedTasks.Length; i++)
            {
                allTasks.Add(feedTasks[i]);
                allDescriptions.Add(String.Format("Fetching feed {0}...", feeds[i].AbsoluteUri));
            }
            for (int i = 0; i < riverTasks.Length; i++)
            {
                allTasks.Add(riverTasks[i]);
                allDescriptions.Add(String.Format("Updating river {0}...", profile.Rivers[i].Name));
            }

            return new AsyncProgressActionResult(allTasks.ToArray(), allDescriptions.ToArray());
        }

        [HttpPost("/api/v1/user/{user}/signout")]
        public IActionResult SignOut(string user)
        {
            var authnManager = HttpContext.RequestServices.GetRequiredService<AuthenticationManager>();
            authnManager.SignOut(HttpContext);
            return Ok();
        }

        [HttpDelete("/api/v1/user/{user}/river/{id}")]
        public async Task<IActionResult> DeleteRiver(string user, string id)
        {
            UserProfile profile = await this.profileStore.GetProfileFor(user);
            UserProfile newProfile = profile.With(
                rivers: profile.Rivers.RemoveAll(rd => String.CompareOrdinal(rd.Id, id) == 0));
            await this.profileStore.SaveProfileFor(user, newProfile);

            var rivers = (from r in newProfile.Rivers
                          select new
                          {
                              name = r.Name,
                              id = r.Id,
                              url = Url.Action(nameof(GetRiver), new { user, id = r.Id }),
                          }).ToArray();
            return Json(new { status = "ok", rivers });
        }

        [HttpGet("/api/v1/user/{user}/river/{id}")]
        public async Task<IActionResult> GetRiver(string user, string id)
        {
            River river = await LoadAggregate(user, id);
            return Json(river);
        }

        [HttpPut("/api/v1/user/{user}/river/{id}/name")]
        public async Task<IActionResult> PutRiverName(string user, string id)
        {
            var requestBody = await ReadRequest<SetNameRequest>();
            UserProfile profile = await this.profileStore.GetProfileFor(user);
            RiverDefinition river = profile.Rivers.FirstOrDefault(rd => String.CompareOrdinal(rd.Id, id) == 0);
            if (river == null) { throw FaultException.NoRiver(id); }
            if (profile.Rivers.Any(rd => String.CompareOrdinal(rd.Name, requestBody.Name) == 0))
            {
                throw FaultException.DuplicateName(requestBody.Name);
            }

            var newRiver = river.With(name: requestBody.Name);
            var newProfile = profile.With(rivers: profile.Rivers.Replace(river, newRiver));
            await this.profileStore.SaveProfileFor(user, newProfile);

            return Ok();
        }

        [HttpGet("/api/v1/user/{user}/river/{id}/sources")]
        public async Task<IActionResult> GetRiverSources(string user, string id)
        {
            UserProfile profile = await this.profileStore.GetProfileFor(user);
            RiverDefinition river = profile.Rivers.FirstOrDefault(rd => String.CompareOrdinal(rd.Id, id) == 0);
            IList<Uri> feedUris = river?.Feeds ?? (IList<Uri>)(Array.Empty<Uri>());
            River[] feedrivers = await Task.WhenAll(feedUris.Select(f => this.feedStore.LoadRiverForFeed(f)));
            return GetSourcesForRiver(feedrivers);
        }

        [HttpPost("/api/v1/user/{user}/river/{id}/sources")]
        public async Task<IActionResult> AddRiverSource(string user, string id)
        {
            var requestBody = await ReadRequest<AddRiverSourceRequest>();

            Task<IList<Uri>> feedUrlsTask = FeedDetector.GetFeedUrls(requestBody.Url);
            Task<UserProfile> loadProfileTask = this.profileStore.GetProfileFor(user);

            await Task.WhenAll(feedUrlsTask, loadProfileTask);

            IList<Uri> feedUrls = await feedUrlsTask;
            if (feedUrls.Count == 0) { throw FaultException.NoFeed(requestBody.Url); }

            UserProfile profile = await loadProfileTask;
            RiverDefinition river = profile.Rivers.FirstOrDefault(rd => String.CompareOrdinal(rd.Id, id) == 0);
            if (river == null) { throw FaultException.NoRiver(id); }

            if (feedUrls.Count > 1)
            {
                // NOTE: I considered and rejected filtering the set of returned feeds by the set of feeds already
                //       subscribed; but then I thought the UX of the "the URL is ambiguous, which feed do you want?"
                //       dialog with a single feed was dumb. Then I considered (and rejected) filtering the list of 
                //       feedUrls directly, and auto-subscribing to the only non-subscribed feed. But that's a bad UX:
                //       if I've forgotten I've subscribed to something and try to re-subscribe then I will end up 
                //       subscribing to a feed I didn't intend to. (Consider the SFP page, where I find RSS by <a> 
                //       tags, and where there is a link to both the page comments and the comic present on the front
                //       page.)
                // 
                Tuple<Uri, string>[] foundFeeds = await LoadFeedTitles(feedUrls);
                return Json(new
                {
                    status = "ambiguous",
                    feeds = (from kvp in foundFeeds
                             select new
                             {
                                 feedUrl = kvp.Item1,
                                 title = kvp.Item2,
                                 isSubscribed = river.Feeds.Contains(kvp.Item1),
                             }).ToList(),
                }, Policies.SerializerSettings);
            }
            else
            {
                Uri feedUrl = feedUrls[0];

                RiverDefinition newRiver = await SubscribeRiverToFeed(profile, river, feedUrl);

                IList<Uri> feedUris = newRiver.Feeds ?? (IList<Uri>)(Array.Empty<Uri>());
                River[] feedrivers = await Task.WhenAll(feedUris.Select(f => this.feedStore.LoadRiverForFeed(f)));
                return GetSourcesForRiver(feedrivers);
            }
        }

        [HttpDelete("/api/v1/user/{user}/river/{id}/sources/{sourceId}")]
        public async Task<IActionResult> RemoveRiverSource(string user, string id, string sourceId)
        {
            UserProfile profile = await this.profileStore.GetProfileFor(user);
            RiverDefinition river = profile.Rivers.FirstOrDefault(rd => String.CompareOrdinal(rd.Id, id) == 0);
            if (river == null) { throw FaultException.NoRiver(id); }

            var feedrivers = new List<River>(
                await Task.WhenAll(river.Feeds.Select(f => this.feedStore.LoadRiverForFeed(f))));

            RiverDefinition newRiver = river;
            for (int i = feedrivers.Count - 1; i >= 0; i--)
            {
                River r = feedrivers[i];
                if (Util.HashString(r.Metadata.OriginUrl.AbsoluteUri) == sourceId)
                {
                    newRiver = newRiver.With(feeds: newRiver.Feeds.RemoveAt(i));
                    feedrivers.RemoveAt(i);
                }
            }

            if (newRiver.Feeds.Count != river.Feeds.Count)
            {
                // Remove all instances of the source from the current aggregate.
                River aggregate = await this.aggregateStore.LoadAggregate(id);
                ImmutableList<RiverFeed> newFeeds = aggregate.UpdatedFeeds.Feeds.RemoveAll(
                    f => Util.HashString(f.FeedUrl.AbsoluteUri) == sourceId);
                River newAggregate = aggregate.With(updatedFeeds: aggregate.UpdatedFeeds.With(feeds: newFeeds));
                await this.aggregateStore.WriteAggregate(id, newAggregate);

                // Remove the source from the profile.
                UserProfile newProfile = profile.With(rivers: profile.Rivers.Replace(river, newRiver));
                await this.profileStore.SaveProfileFor(user, newProfile);
            }

            return GetSourcesForRiver(feedrivers.ToArray());
        }

        [HttpPost("/api/v1/user/{user}/river/{id}/mode")]
        public async Task<IActionResult> PostRiverMode(string user, string id)
        {
            var requestBody = await ReadRequest<SetModeRequest>();
            River river = await LoadAggregate(user, id);
            RiverFeedMeta newMeta = river.Metadata.With(mode: requestBody.Mode);
            River newRiver = river.With(metadata: newMeta);
            await this.aggregateStore.WriteAggregate(id, newRiver);
            return Ok();
        }

        [HttpPost("/api/v1/user/{user}/set_order")]
        public async Task<IActionResult> PostSetOrder(string user)
        {
            // should be genva
            var requestBody = await ReadRequest<SetOrderRequest>();
            UserProfile profile = await this.profileStore.GetProfileFor(user);

            Dictionary<string, RiverDefinition> rivers = profile.Rivers.ToDictionary(rd => rd.Id);
            var newRivers = new List<RiverDefinition>();
            foreach (string id in requestBody.RiverIds)
            {
                RiverDefinition rd;
                if (rivers.TryGetValue(id, out rd))
                {
                    newRivers.Add(rd);
                    rivers.Remove(id);
                }
            }

            foreach (RiverDefinition rd in profile.Rivers)
            {
                if (rivers.ContainsKey(rd.Id))
                {
                    newRivers.Add(rd);
                }
            }

            UserProfile newProfile = profile.With(rivers: newRivers);
            await this.profileStore.SaveProfileFor(user, newProfile);

            return Ok(); // TODO: Progress?
        }

        [HttpPost("/api/v1/user/{user}/password")]
        public async Task<IActionResult> PostSetPassword(string user)
        {
            // should be genva
            var requestBody = await ReadRequest<SetPasswordRequest>();
            UserProfile profile = await this.profileStore.GetProfileFor(user);

            var newProfile = profile.With(
                password: AuthenticationManager.EncryptPassword(requestBody.Password),
                logins: new LoginCookie[0]);
            await this.profileStore.SaveProfileFor(user, newProfile);

            // TODO: Send mail.

            return Ok();
        }

        [HttpGet("/api/v1/user/{user}/email")]
        public async Task<IActionResult> GetEmail(string user)
        {
            UserProfile profile = await this.profileStore.GetProfileFor(user);
            return Json(new
            {
                email = profile.Email,
                emailVerified = profile.EmailVerified,
            });
        }

        [HttpPost("/api/v1/user/{user}/email")]
        public async Task<IActionResult> PostSetEmail(string user)
        {
            // should be genva
            var requestBody = await ReadRequest<SetEmailRequest>();
            UserProfile profile = await this.profileStore.GetProfileFor(user);

            var newProfile = profile.With(
                email: requestBody.Email,
                emailVerified: false);
            await this.profileStore.SaveProfileFor(user, newProfile);

            // TODO: Send mail to old and new.

            return Ok();
        }

        IActionResult GetSourcesForRiver(River[] feedrivers)
        {
            return Json(new
            {
                status = "ok",
                sources = feedrivers.Select(r => new
                {
                    id = Util.HashString(r.Metadata.OriginUrl.AbsoluteUri),
                    name = r.UpdatedFeeds.Feeds.FirstOrDefault()?.FeedTitle ?? r.Metadata.OriginUrl.AbsoluteUri,
                    webUrl = r.UpdatedFeeds.Feeds.FirstOrDefault()?.WebsiteUrl ?? r.Metadata.OriginUrl.AbsoluteUri,
                    feedUrl = r.Metadata.OriginUrl,
                    lastStatus = r.Metadata.LastStatus,
                    lastUpdated = r.UpdatedFeeds.Feeds.Count > 0
                        ? r.UpdatedFeeds.Feeds.Max(f => f.WhenLastUpdate)
                        : DateTimeOffset.MinValue,
                }).ToArray()
            });
        }

        async Task<TRequest> ReadRequest<TRequest>()
        {
            try
            {
                var content = new MemoryStream();
                await Request.Body.CopyToAsync(content);
                content.Position = 0;
                using (var reader = new StreamReader(content))
                {
                    string data = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<TRequest>(data);
                }
            }
            catch (Exception e)
            {
                throw FaultException.DecodingError(e.Message);
            }
        }

        async Task<River> LoadAggregate(string user, string riverId)
        {
            River river = await this.aggregateStore.LoadAggregate(riverId);
            if (river.Metadata.Owner != null && String.CompareOrdinal(river.Metadata.Owner, user) != 0)
            {
                Serilog.Log.Warning("{user} attempted to access river {id} owned by {owner}",
                    user, riverId, river.Metadata.Owner);
                throw FaultException.AccessDenied("Not allowed to access somebody else's river.");
            }
            return river;
        }

        async Task<Tuple<Uri, string>[]> LoadFeedTitles(IList<Uri> feedUrls)
        {
            var fetchTitleTasks = new Task<Tuple<Uri, string>>[feedUrls.Count];
            for (int i = 0; i < feedUrls.Count; i++)
            {
                Uri feedUrl = feedUrls[i];
                fetchTitleTasks[i] = this.feedParser.FetchAndUpdateRiver(feedUrls[i]).ContinueWith(tr =>
                {
                    string title = feedUrl.AbsoluteUri;
                    if (tr.Exception == null && tr.Result.UpdatedFeeds.Feeds.Count > 0)
                    {
                        title = tr.Result.UpdatedFeeds.Feeds[0].FeedTitle;
                        feedUrl = tr.Result.Metadata.OriginUrl;
                    }

                    return Tuple.Create(feedUrl, title);
                });
            }


            return await Task.WhenAll(fetchTitleTasks);
        }

        async Task<River> RefreshAggregate(string owner, RiverDefinition riverDef, Task feedsTask)
        {
            await feedsTask;
            River river = await this.feedParser.RefreshAggregateRiverWithFeeds(riverDef.Id, riverDef.Feeds);
            if (river.Metadata.Owner == null)
            {
                var newRiver = river.With(metadata: river.Metadata.With(owner: owner));
                await this.aggregateStore.WriteAggregate(riverDef.Id, newRiver);
                river = newRiver;
            }
            return river;
        }

        Task<River>[] RefreshAllAggregates(string user, UserProfile profile, Task feedsTask)
        {
            var riverTasks = new Task<River>[profile.Rivers.Count];
            for (int i = 0; i < profile.Rivers.Count; i++)
            {
                RiverDefinition riverDef = profile.Rivers[i];
                riverTasks[i] = RefreshAggregate(user, riverDef, feedsTask);
            }
            return riverTasks;
        }

        async Task<RiverDefinition> SubscribeRiverToFeed(UserProfile profile, RiverDefinition river, Uri feedUrl)
        {
            River parsedFeed = await this.feedParser.FetchAndUpdateRiver(feedUrl);
            feedUrl = parsedFeed.Metadata.OriginUrl;

            if (!river.Feeds.Contains(feedUrl))
            {
                RiverDefinition newRiver = river.With(feeds: river.Feeds.Add(feedUrl));
                UserProfile newProfile = profile.With(rivers: profile.Rivers.Replace(river, newRiver));
                await this.profileStore.SaveProfile(newProfile);

                // Grab some number of items out of the udpated river; we're gonna forge an update.
                RiverFeed newUpdate = parsedFeed.UpdatedFeeds.Feeds[0].With(
                    items: parsedFeed.UpdatedFeeds.Feeds.SelectMany(f => f.Items).Take(30));

                River aggregate = await this.aggregateStore.LoadAggregate(newRiver.Id);
                River newAggregate = aggregate.With(
                    updatedFeeds: aggregate.UpdatedFeeds.With(
                        feeds: aggregate.UpdatedFeeds.Feeds.Insert(0, newUpdate)
                    )
                );
                await this.aggregateStore.WriteAggregate(newRiver.Id, newAggregate);

                river = newRiver;
            }

            return river;
        }

        Task<River>[] UpdateAllFeeds(Uri[] feeds)
        {
            Task<River>[] feedTasks = new Task<River>[feeds.Length];
            for (int i = 0; i < feeds.Length; i++)
            {
                Uri feedUri = feeds[i];
                feedTasks[i] = this.feedParser.FetchAndUpdateRiver(feedUri);
            }
            return feedTasks;
        }

        public class AddRiverSourceRequest
        {
            public AddRiverSourceRequest(string url) { Url = url; }

            [JsonProperty("url", Required = Required.Always)]
            public string Url { get; }
        }

        public class CreateOrRestoreRequest
        {
            public CreateOrRestoreRequest(string name, string id) { Name = name; Id = id; }

            [JsonProperty("name")]
            public string Name { get; }

            [JsonProperty("id")]
            public string Id { get; }
        }

        public class SetNameRequest
        {
            public SetNameRequest(string name)
            {
                Name = name;
            }

            [JsonProperty("name", Required = Required.Always)]
            public string Name { get; }
        }

        public class SetOrderRequest
        {
            public SetOrderRequest(IEnumerable<string> riverIds)
            {
                RiverIds = ImmutableList.CreateRange(riverIds);
            }

            [JsonProperty("riverIds", Required = Required.Always)]
            public ImmutableList<string> RiverIds { get; }
        }

        public class SetModeRequest
        {
            public SetModeRequest(string mode)
            {
                Mode = mode;
            }

            [JsonProperty("mode", Required = Required.Always)]
            public string Mode { get; }
        }

        public class SetPasswordRequest
        {
            public SetPasswordRequest(string password)
            {
                Password = password;
            }

            [JsonProperty("password", Required = Required.Always)]
            public string Password { get; }
        }

        public class SetEmailRequest
        {
            public SetEmailRequest(string email)
            {
                Email = email;
            }

            [JsonProperty("email", Required = Required.Always)]
            public string Email { get; }
        }
    }

    public class HealthController : Controller
    {
        static HealthReport CachedReport;

        readonly Func<Task<HealthResult>>[] healthChecks;
        readonly UserProfileStore profileStore;
        readonly RiverFeedStore feedStore;
        readonly AggregateRiverStore aggregateStore;
        readonly RiverThumbnailStore thumbnailStore;

        public HealthController(
            UserProfileStore profileStore,
            RiverFeedStore feedStore,
            AggregateRiverStore aggregateStore,
            RiverThumbnailStore thumbnailStore)
        {
            this.profileStore = profileStore;
            this.feedStore = feedStore;
            this.aggregateStore = aggregateStore;
            this.thumbnailStore = thumbnailStore;

            this.healthChecks = new Func<Task<HealthResult>>[]
            {
                CheckAggregateStore,
                CheckFeedStore,
                CheckProfileStore,
                CheckThumbnailStore,
                CheckCanMakeThumbnail,
            };
        }

        async Task<HealthResult> CheckProfileStore()
        {
            var result = new HealthResult { Title = "Profile Store", Healthy = false };
            try
            {
                UserProfile dummy = await this.profileStore.GetProfileFor("@@@health");
                await this.profileStore.SaveProfileFor("@@@health", dummy);
                result.Healthy = true;
                result.Log.Add("OK");
            }
            catch (Exception e)
            {
                result.Healthy = false;
                result.Log.AddRange(e.ToString().Split('\n'));
            }
            return result;
        }

        async Task<HealthResult> CheckAggregateStore()
        {
            var result = new HealthResult { Title = "Aggregate Store", Healthy = false };
            try
            {
                River aggregate = await this.aggregateStore.LoadAggregate("fooble");
                await this.aggregateStore.WriteAggregate("fooble", aggregate);
                result.Healthy = true;
                result.Log.Add("OK");
            }
            catch (Exception e)
            {
                result.Healthy = false;
                result.Log.AddRange(e.ToString().Split('\n'));
            }
            return result;
        }

        async Task<HealthResult> CheckFeedStore()
        {
            var result = new HealthResult { Title = "Feed Store", Healthy = false };
            try
            {
                River river = await this.feedStore.LoadRiverForFeed(new Uri("http://dummy/"));
                await this.feedStore.WriteRiver(new Uri("http://dummy"), river);
                result.Healthy = true;
                result.Log.Add("OK");
            }
            catch (Exception e)
            {
                result.Healthy = false;
                result.Log.AddRange(e.ToString().Split('\n'));
            }
            return result;
        }

        async Task<HealthResult> CheckThumbnailStore()
        {
            var result = new HealthResult { Title = "Thumbnail Store", Healthy = false };
            try
            {
                byte[] bytes = await LoadFileBytes("dummy.png");
                Image<Rgba32> img = Image.Load(bytes);
                /* Uri uri = */
                await this.thumbnailStore.StoreImage(img);
                // TODO: validate URI makes an image.

                result.Healthy = true;
                result.Log.Add("OK");
            }
            catch (Exception e)
            {
                result.Healthy = false;
                result.Log.AddRange(e.ToString().Split('\n'));
            }
            return result;
        }

        async Task<HealthResult> CheckCanMakeThumbnail()
        {
            var result = new HealthResult { Title = "Make Thumbnail", Healthy = false };
            try
            {
                byte[] bytes = await LoadFileBytes("dummy.png");
                Image<Rgba32> img = Image.Load(bytes);

                /* var dstImage =*/
                ThumbnailExtractor.MakeThumbnail(img);

                result.Healthy = true;
                result.Log.Add("OK");
            }
            catch (Exception e)
            {
                result.Healthy = false;
                result.Log.AddRange(e.ToString().Split('\n'));
            }
            return result;
        }

        static async Task<byte[]> LoadFileBytes(string file)
        {
            using (var stream = System.IO.File.OpenRead(file))
            {
                byte[] img = new byte[stream.Length];
                MemoryStream ms = new MemoryStream(img);
                await stream.CopyToAsync(ms);
                return img;
            }
        }

        [HttpGet("/health/ping")]
        public IActionResult Ping() => Ok();

        [HttpGet("/health")]
        public async Task<IActionResult> HealthView()
        {
            HealthReport report = await CheckHealth();
            XDocument document = new XDocument(
                new XProcessingInstruction("xml-stylesheet", "type='text/xsl' href='/health.xslt'"),
                report.ToXml());

            var result = Content(document.ToString(), "text/xml", Encoding.UTF8);
            if (report.Checks.Any(c => !c.Healthy))
            {
                result.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            return result;
        }

        async Task<HealthReport> CheckHealth()
        {
            if (CachedReport != null) { return CachedReport; }
            HealthResult[] results = await Task.WhenAll(from check in this.healthChecks select check());
            CachedReport = new HealthReport(results);
            return CachedReport;
        }

        class HealthReport
        {
            public HealthReport(IEnumerable<HealthResult> checks)
            {
                Checks.AddRange(checks);
            }

            public HealthRuntimeSummary RuntimeSummary { get; } = new HealthRuntimeSummary();
            public List<HealthResult> Checks { get; } = new List<HealthResult>();

            public XElement ToXml() => new XElement(
                "healthReport",
                RuntimeSummary.ToXml(),
                new XElement("checks", Checks.Select(c => c.ToXml())));
        }

        class RuntimeProperty
        {
            public string Name { get; set; }
            public string Value { get; set; }

            public XElement ToXml() => new XElement(
                "runtimeProperty",
                new XAttribute("name", Name),
                new XAttribute("value", Value));
        }

        class HealthRuntimeSummary
        {
            public HealthRuntimeSummary()
            {
                Properties = new List<RuntimeProperty>
                {
                    new RuntimeProperty
                    {
                        Name = "Host Name",
                        Value = Environment.MachineName
                    },
                    new RuntimeProperty
                    {
                         Name = "Framework Description",
                         Value = RuntimeInformation.FrameworkDescription
                    },
                    new RuntimeProperty
                    {
                         Name = "OS Architecture",
                         Value = RuntimeInformation.OSArchitecture.ToString()
                    },
                    new RuntimeProperty
                    {
                        Name = "OS Description",
                        Value = RuntimeInformation.OSDescription
                    },
                    new RuntimeProperty
                    {
                        Name = "Process Architecture",
                        Value = RuntimeInformation.ProcessArchitecture.ToString()
                    },
                };
            }

            List<RuntimeProperty> Properties { get; }

            public XElement ToXml() => new XElement(
                "runtimeSummary",
                Properties.Select(p => p.ToXml()));
        }

        class HealthResult
        {
            public string Title { get; set; }
            public bool Healthy { get; set; }
            public List<string> Log { get; } = new List<string>();

            public XElement ToXml() => new XElement(
                "healthResult",
                new XElement("title", Title),
                new XElement("healthy", Healthy ? "true" : "false"),
                new XElement("log", Log.Select(line => new XElement("p", line))));
        }
    }

    class WebPacker
    {
        ManualResetEvent stop = new ManualResetEvent(false);
        Thread thread;

        public void Start()
        {
            Serilog.Log.Information("Starting webpack thread");
            this.thread = new Thread(PackThread) { IsBackground = true };
            this.thread.Start();
        }

        public void Stop()
        {
            Serilog.Log.Information("Stopping webpack thread");
            this.stop.Set();
            this.thread.Join();
        }

        void PackThread()
        {
            Serilog.Log.Information("Packing thread starting.");

            string[] args = new[]
            {
                Path.Combine("node_modules", "webpack", "bin", "webpack.js"),
                "--watch",
            };

            var startinfo = new ProcessStartInfo()
            {
                FileName = "node",
                Arguments = String.Join(" ", args),
                WorkingDirectory = Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            Process process;
            var processStopped = new AutoResetEvent(false);
            while (true)
            {
                Serilog.Log.Information("Starting webpack");
                process = Process.Start(startinfo);
                if (process != null)
                {
                    try
                    {
                        process.EnableRaisingEvents = true; // Only applies to Exited though.
                        process.Exited += (o, e) => processStopped.Set();
                        process.ErrorDataReceived += (o, e) => Serilog.Log.Warning("webpack: {webpack_error}", e.Data);
                        process.OutputDataReceived += (o, e) => Serilog.Log.Information("webpack: {webpack_info}", e.Data);
                        process.Start();
                        process.BeginErrorReadLine();
                        process.BeginOutputReadLine();

                        Serilog.Log.Information("Waiting for webpack to terminate");
                        int which = WaitHandle.WaitAny(new WaitHandle[] { this.stop, processStopped });
                        if (which == 0) { break; }
                        Serilog.Log.Information("Webpack stopped");
                    }
                    catch (Exception e)
                    {
                        Serilog.Log.Error(e, "Exception occurred while running webpack");
                        if (!process.HasExited) { process.Kill(); }
                    }
                }
                Serilog.Log.Information("Waiting before looping");
                if (this.stop.WaitOne(TimeSpan.FromSeconds(1))) { break; }
            }

            Serilog.Log.Information("Shutting down webpack");
            if (process != null && !process.HasExited) { process.Kill(); }

            Serilog.Log.Information("Packing thread stopping");
        }
    }

    class WebStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // enable MVC framework
            services.AddMvc();

            // enable response compression
            services.AddResponseCompression(options =>
            {
                // N.B.: This enables some known security holes, so be careful. In particular,
                //       if some part of the request is echoed in the response body then a 
                //       compression oracle can be used to decode the rest of the body. (See 
                //       the CRIME, BREACH, and HEIST attacks.) 
                //
                //       We still enable this because it's too huge a win to not do, and we 
                //       only put secrets in the headers which are not subject to compression 
                //       anyway. (I understand that these count as famous last words.)
                options.EnableForHttps = true;
            });

            var aggStore = new AggregateRiverStore();
            var feedStore = new RiverFeedStore();
            var thumbStore = new RiverThumbnailStore();
            var feedParser = new RiverFeedParser();

            var profileStore = new UserProfileStore();
            var authenticationManager = new AuthenticationManager(profileStore);

            services.AddSingleton(typeof(RiverThumbnailStore), thumbStore);
            services.AddSingleton(typeof(AggregateRiverStore), aggStore);
            services.AddSingleton(typeof(RiverFeedStore), feedStore);
            services.AddSingleton(typeof(UserProfileStore), profileStore);
            services.AddSingleton(typeof(AuthenticationManager), authenticationManager);
            services.AddSingleton(typeof(RiverFeedParser), feedParser);
        }

        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IApplicationLifetime appLifetime)
        {
            loggerFactory.AddSerilog();
            appLifetime.ApplicationStopped.Register(Serilog.Log.CloseAndFlush);

            // enable exception pages in development
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                var packer = new WebPacker();
                packer.Start();
                appLifetime.ApplicationStopping.Register(packer.Stop);
            }

            // compress responses
            app.UseResponseCompression();

            // serve static files from wwwroot/*
            app.UseStaticFiles();

            // Transform fault exceptions to responses.
            app.UseFaultHandler();

            // use MVC framework
            app.UseMvc();
        }
    }
}
