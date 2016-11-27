﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AngleSharp.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Scrypt;
using Serilog;
using Serilog.Events;

namespace onceandfuture
{
    public class Fault
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("details")]
        public string Details { get; set; }
    }

    public class AuthenticationManager
    {
        const string CookieName = "onceandfuture-feed";
        const int MaxConcurrentSessions = 10;

        readonly ConcurrentDictionary<string, LoginCookieCache[]> loginCache =
            new ConcurrentDictionary<string, LoginCookieCache[]>();
        readonly UserProfileStore profileStore;
        readonly ScryptEncoder scrypt = new ScryptEncoder();

        public AuthenticationManager(UserProfileStore profileStore)
        {
            // TODO: Cache scrubber.
            this.profileStore = profileStore;
        }

        bool ParseCookie(string cookie, out string user, out Guid token)
        {
            user = null;
            token = Guid.Empty;

            string[] parts = cookie.Split(new char[] { ',' }, 2);
            if (parts.Length != 2) { return false; }
            if (!Guid.TryParseExact(parts[0], "n", out token)) { return false; }
            user = parts[1];
            return true;
        }

        string MakeCookie(string user, Guid token)
        {
            return String.Format("{0},{1}", token.ToString("n"), user);
        }

        string EncryptToken(Guid token) => scrypt.Encode(token.ToString("N"));

        bool ValidateAgainstLoginCache(Guid token, LoginCookieCache[] cache)
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

            string encryptedToken = EncryptToken(token);
            for(int i = 0; i < cache.Length; i++)
            {
                // Check against cyphertext; this is slow but faster than hitting S3 again.
                if (String.Equals(encryptedToken, cache[i].Token, StringComparison.Ordinal))
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
            for(int i = 0; i < validLogins.Count; i++)
            {
                // Find the matching entry...
                while(existingPointer < existingCache.Length)
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
            if (!context.Request.Cookies.TryGetValue(CookieName, out cookie)) { return null; }

            // Is it really a valid cookie?
            string user;
            Guid token;
            if (!ParseCookie(cookie, out user, out token)) { return null; }

            // Is it valid? Check the cache.
            LoginCookieCache[] cachedCookies;
            if (loginCache.TryGetValue(user, out cachedCookies))
            {
                if (ValidateAgainstLoginCache(token, cachedCookies))
                {
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
                if (ValidateAgainstLoginCache(token, cachedCookies))
                {                
                    context.Items.Add(CookieName, user);
                    return user;                
                }
            }
            // No profile, or no matching cookie-- not authn.
            return null;
        }

        public async Task<bool> ValidateLogin(HttpContext context, string user, string password)
        {
            // TODO: Obviously.
            if (password != "swordfish") { return false; }

            UserProfile profile = await this.profileStore.GetProfileFor(user);

            // TODO: Fix this login duration. :P
            Guid token = Guid.NewGuid();
            var newLogin = new LoginCookie(EncryptToken(token), DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30));

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

        struct LoginCookieCache
        {
            public Guid Plaintext;
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
            string authn_user = await authmgr.GetAuthenticatedUser(context.HttpContext);

            if (authn_user == null || !String.Equals(user, authn_user, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new JsonResult(new Fault
                {
                    Code = "accessDenied",
                    Status = "error",
                    Details = "User is not authorized to access this resource.",
                })
                {
                    StatusCode = (int)HttpStatusCode.Forbidden
                };
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
                return RedirectToAction(nameof(AppController.Login));
            }
            else
            {
                return RedirectToAction(nameof(AppController.App), new { user = user });
            }
        }

        [HttpGet("/feed/{user}")]
        public async Task<IActionResult> App(string user)
        {
            string authn_user = await this.authenticationManager.GetAuthenticatedUser(HttpContext);
            if (!string.Equals(authn_user, user, StringComparison.Ordinal))
            {
                return RedirectToAction(nameof(AppController.Login));
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
                return RedirectToAction(nameof(AppController.App), new { user = user });
            }
            else
            {
                return RedirectToAction(nameof(AppController.Login));
            }
        }
    }

    [EnforceAuthentication(userParameterName: "user")]
    public class ApiController : Controller
    {
        readonly UserProfileStore profileStore;
        readonly AggregateRiverStore aggregateStore;
        readonly RiverFeedStore feedStore;

        public ApiController(
            UserProfileStore profileStore,
            AggregateRiverStore aggregateStore,
            RiverFeedStore feedStore)
        {
            this.profileStore = profileStore;
            this.aggregateStore = aggregateStore;
            this.feedStore = feedStore;
        }
        
        [HttpGet("/api/v1/river/{user}")]
        public async Task<IActionResult> GetRiverList(string user)
        {
            UserProfile profile = await this.profileStore.GetProfileFor(user);
            var rivers = (from r in profile.Rivers
                          select new
                          {
                              name = r.Name,
                              id = r.Id,
                              url = String.Format("/api/v1/river/{0}/{1}", user, r.Id),
                          }).ToArray();
            return Json(new { rivers = rivers });
        }

        [HttpGet("/api/v1/river/{user}/{id}")]
        public async Task<IActionResult> GetRiver(string user, string id)
        {
            River river = await this.aggregateStore.LoadAggregate(id);
            return Json(river);
        }

        [HttpPost("/api/v1/river/{user}/{id}")]
        public Task<IActionResult> AddFeed(string user, string id)
        {
            throw new NotImplementedException();
        }

        [HttpPost("/api/v1/river/{user}/{id}/mode")]
        public Task<IActionResult> PostRiverMode(string user, string id)
        {
            throw new NotImplementedException();
        }

        [HttpPost("/api/v1/river/{user}/refresh_all")]
        public async Task<IActionResult> PostRefreshAll(string user)
        {
            var parser = new RiverFeedParser();

            UserProfile profile = await this.profileStore.GetProfileFor(user);
            await Task.WhenAll(
                profile.Rivers.Select(r => parser.RefreshAggregateRiverWithFeeds(
                    r.Id, r.Feeds, this.aggregateStore, this.feedStore, CancellationToken.None)));
            return Ok(); // Progress?
        }
    }

    public class HealthController : Controller
    {
        static Func<Task<HealthResult>>[] HealthChecks = new Func<Task<HealthResult>>[]
        {
            // TODO: Health checks
        };

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
            HealthResult[] results = await Task.WhenAll(from check in HealthChecks select check());
            return new HealthReport(results);
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
                    new RuntimeProperty
                    {
                        Name = "Runtime Directory",
                        Value = RuntimeEnvironment.GetRuntimeDirectory()
                    },
                    new RuntimeProperty
                    {
                        Name = "System Version",
                        Value = RuntimeEnvironment.GetSystemVersion()
                    },
                    new RuntimeProperty
                    {
                        Name = "System Configuration File",
                        Value = RuntimeEnvironment.SystemConfigurationFile
                    },
                };
            }

            public List<RuntimeProperty> Properties { get; } = new List<RuntimeProperty>();

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
                "-d --watch",
                "--output-path wwwroot",
                "--output-filename bundle.js",
                "--entry ./wwwroot/main.js",
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

            Process process = null;
            var processStopped = new AutoResetEvent(false);
            while (true)
            {
                Serilog.Log.Information("Starting webpack");
                process = Process.Start(startinfo);
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
                Serilog.Log.Information("Waiting before looping");
                Thread.Sleep(TimeSpan.FromSeconds(1));
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

            var aggStore = new AggregateRiverStore();
            var feedStore = new RiverFeedStore();
            var profileStore = new UserProfileStore();
            var thumbStore = new RiverThumbnailStore();

            var authenticationManager = new AuthenticationManager(profileStore);

            services.AddSingleton(typeof(RiverThumbnailStore), thumbStore);
            services.AddSingleton(typeof(AggregateRiverStore), aggStore);
            services.AddSingleton(typeof(RiverFeedStore), feedStore);
            services.AddSingleton(typeof(UserProfileStore), profileStore);
            services.AddSingleton(typeof(AuthenticationManager), authenticationManager);
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

            // serve static files from wwwroot/*
            app.UseStaticFiles();            

            // use MVC framework
            app.UseMvc();
        }
    }

    class Program
    {
        static ProgramOpts Options = new ProgramOpts()
            .AddOption("help", "Display this help.", o => o.Flag('?'))
            .AddOption("verbose", "Increase the logging verbosity. (Specify more than once to be even more verbose.)")
            .AddVerb("update", "Update one or more feeds.", v => v
                .AddOption("feed", "The single feed URL to update.", o => o.AcceptValue())
                .AddOption("user", "The user to update feeds for.", o => o.AcceptValue())
            )
            .AddVerb("show", "Show items in one or more feeds.", v => v
                .AddOption("feed", "The single feed URL to show.", o => o.AcceptValue())
                .AddOption("user", "The user to show for.", o => o.AcceptValue())
            )
            .AddVerb("sub", "Subscribe to a feed.", v => v
                .AddOption("user", "The user to add a subscription for.", o => o.IsRequired())
                .AddOption("river", "The river to add to.", o => o.HasDefault("main"))
                .AddOption("feed", "The feed to add.", o => o.IsRequired())
            )
            .AddVerb("unsub", "Unsubscribe from a feed.", v => v
                .AddOption("user", "The user to remove a subscription for.", o => o.IsRequired())
                .AddOption("river", "The river to remove from.", o => o.HasDefault("main"))
                .AddOption("feed", "The feed to remove.", o => o.IsRequired())
            )
            .AddVerb("list", "List a user's subscriptions.", v => v
                .AddOption("user", "The user whose subscriptions we're showing.", o => o.IsRequired())
            )
            .AddVerb("serve", "Start the web server.", v => v
                .AddOption("url", "The URL to listen on.", o => o.HasDefault("http://localhost:5000"))
                .AddOption("environment", "The environment to run as.", o => o.HasDefault("Development"))
            )
            ;

        static int Main(string[] args)
        {
            try
            {
                ParsedOpts parsedArgs = Options.ParseArguments(args);
                if (parsedArgs.Error != null)
                {
                    Console.Error.WriteLine(parsedArgs.Error);
                    Console.Error.WriteLine(Options.GetHelp(parsedArgs.Verb));
                    return 1;
                }
                if (parsedArgs["help"].Flag)
                {
                    Console.WriteLine(Options.GetHelp(parsedArgs.Verb));
                    return 0;
                }

                var logLevel = (LogEventLevel)Math.Max((int)(LogEventLevel.Error - parsedArgs["verbose"].Count), 0);
                Serilog.Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .MinimumLevel.Is(logLevel)
                    .WriteTo.LiterateConsole()
                    .CreateLogger();

                switch (parsedArgs.Verb)
                {
                case "update": return DoUpdate(parsedArgs);
                case "show": return DoShow(parsedArgs);
                case "sub": return DoSubscribe(parsedArgs);
                case "list": return DoList(parsedArgs);
                case "unsub": return DoUnsubscribe(parsedArgs);
                case "serve": return DoServe(parsedArgs);
                }

                throw new NotSupportedException();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 99;
            }
        }

        static int DoShow(ParsedOpts args)
        {
            if (args["feed"].Value != null)
            {
                return DoShowSingle(args);
            }
            else if (args["user"].Value != null)
            {
                return DoShowAll(args);
            }
            else
            {
                Console.Error.WriteLine("Must specify either user or feed.");
                return -1;
            }
        }

        static int DoShowSingle(ParsedOpts args)
        {
            Uri feedUrl;
            if (!Uri.TryCreate(args["feed"].Value, UriKind.Absolute, out feedUrl))
            {
                Console.Error.WriteLine("Feed not a valid url: {0}", args["feed"].Value);
                return 100;
            }

            var feedStore = new RiverFeedStore();
            River river = feedStore.LoadRiverForFeed(feedUrl).Result;
            if (river.UpdatedFeeds.Feeds.Count > 0)
            {
                foreach (RiverFeed feed in river.UpdatedFeeds.Feeds)
                {
                    DumpFeed(feed);
                }
            }
            else
            {
                Console.WriteLine("No data for {0}", feedUrl);
            }

            Console.WriteLine("(Press enter to continue)");
            Console.ReadLine();

            return 0;
        }

        static int DoShowAll(ParsedOpts args)
        {
            var profileStore = new UserProfileStore();
            var aggregateStore = new AggregateRiverStore();

            UserProfile profile = profileStore.GetProfileFor(args["user"].Value).Result;
            foreach (RiverDefinition rd in profile.Rivers)
            {
                Console.WriteLine("Loading {0} ({1})...", rd.Name, rd.Id);
                River river = aggregateStore.LoadAggregate(rd.Id).Result;
                if (river.UpdatedFeeds.Feeds.Count > 0)
                {
                    foreach (RiverFeed feed in river.UpdatedFeeds.Feeds)
                    {
                        DumpFeed(feed);
                    }
                }
                else
                {
                    Console.WriteLine("No data for {0}", rd.Name);
                }

                Console.WriteLine("(Press enter to continue)");
                Console.ReadLine();
            }

            return 0;
        }

        static int DoUpdate(ParsedOpts args)
        {
            if (args["feed"].Value != null)
            {
                return DoUpdateSingleFeed(args);
            }
            else if (args["user"].Value != null)
            {
                return DoUpdateForUser(args);
            }
            else
            {
                Console.Error.WriteLine("Must specify either user or feed.");
                return -1;
            }
        }

        static int DoUpdateSingleFeed(ParsedOpts args)
        {
            Uri feedUrl;
            if (!Uri.TryCreate(args["feed"].Value, UriKind.Absolute, out feedUrl))
            {
                Console.Error.WriteLine("Feed not a valid url: {0}", args["feed"].Value);
                return 100;
            }

            var parser = new RiverFeedParser();
            var feedStore = new RiverFeedStore();

            Console.WriteLine("Refreshing {0}...", feedUrl);
            Stopwatch loadTimer = Stopwatch.StartNew();
            parser.FetchAndUpdateRiver(feedStore, feedUrl, CancellationToken.None).Wait();
            Console.WriteLine("Refreshed {0} in {1}", feedUrl, loadTimer.Elapsed);
            return 0;
        }

        static int DoUpdateForUser(ParsedOpts args)
        {
            string user = args["user"].Value;

            var subscriptionStore = new UserProfileStore();
            var parser = new RiverFeedParser();
            var feedStore = new RiverFeedStore();
            var aggregateStore = new AggregateRiverStore();

            Console.WriteLine("Refreshing for {0}...", user);
            Stopwatch loadTimer = Stopwatch.StartNew();

            UserProfile profile = subscriptionStore.GetProfileFor(user).Result;
            var tasks = from rd in profile.Rivers
                        select parser.RefreshAggregateRiverWithFeeds(
                            rd.Id, rd.Feeds, aggregateStore, feedStore, CancellationToken.None);
            Task.WhenAll(tasks).Wait();

            Console.WriteLine("Refreshed {0} rivers in {1}", profile.Rivers.Count, loadTimer.Elapsed);
            return 0;
        }

        static int DoSubscribe(ParsedOpts args)
        {
            string user = args["user"].Value;
            string feed = args["feed"].Value;
            string riverName = args["river"].Value;

            // Check feed.
            var parser = new RiverFeedParser();
            var feedStore = new RiverFeedStore();
            River feedRiver = parser.FetchAndUpdateRiver(feedStore, new Uri(feed), CancellationToken.None).Result;
            if (feedRiver.Metadata.LastStatus < (HttpStatusCode)200 ||
                feedRiver.Metadata.LastStatus >= (HttpStatusCode)400)
            {
                Console.Error.WriteLine("Could not fetch feed {0}", feed);
                return -1;
            }

            var subscriptionStore = new UserProfileStore();
            UserProfile profile = subscriptionStore.GetProfileFor(user).Result;
            RiverDefinition river = profile.Rivers.FirstOrDefault(r => r.Name == riverName);

            UserProfile newProfile;
            if (river == null)
            {
                newProfile = profile.With(
                    rivers: profile.Rivers.Add(
                        new RiverDefinition(
                            name: riverName,
                            id: Util.MakeID(),
                            feeds: new Uri[] { feedRiver.Metadata.OriginUrl })));
            }
            else
            {
                var newRiver = river.With(feeds: river.Feeds.Add(feedRiver.Metadata.OriginUrl));
                newProfile = profile.With(rivers: profile.Rivers.Replace(river, newRiver));
            }

            subscriptionStore.SaveProfileFor(user, newProfile).Wait();

            Console.WriteLine("OK");
            return 0;
        }

        static int DoList(ParsedOpts args)
        {
            string user = args["user"].Value;
            UserProfile profile = new UserProfileStore().GetProfileFor(user).Result;
            foreach (var river in profile.Rivers)
            {
                Console.WriteLine("{0}:", river.Name);
                foreach (var feed in river.Feeds)
                {
                    Console.WriteLine("  {0}", feed);
                }
            }
            return 0;
        }

        static int DoUnsubscribe(ParsedOpts args)
        {
            string user = args["user"].Value;
            Uri feed = new Uri(args["feed"].Value);
            string riverName = args["river"].Value;

            var subscriptionStore = new UserProfileStore();
            UserProfile profile = subscriptionStore.GetProfileFor(user).Result;
            RiverDefinition river = profile.Rivers.FirstOrDefault(r => r.Name == riverName);
            if (river == null)
            {
                Console.WriteLine("River {0} not found.", riverName);
                return 0;
            }

            RiverDefinition newRiver = river.With(feeds: river.Feeds.Remove(feed));
            UserProfile newProfile = profile.With(rivers: profile.Rivers.Replace(river, newRiver));

            subscriptionStore.SaveProfileFor(user, newProfile).Wait();
            Console.WriteLine("OK");
            return 0;
        }

        static int DoServe(ParsedOpts args)
        {
            // read configuration values from environment variables
            var config = new ConfigurationBuilder()
                //.AddCommandLine(args)
                .AddEnvironmentVariables()
                .Build();

            // use Kestrel server with cwd content root
            var host = new WebHostBuilder()
                .UseConfiguration(config)
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<WebStartup>()
                .UseUrls(args["url"].Value)
                .UseEnvironment(args["environment"].Value)
                .Build();
            host.Run();
            return 0;
        }

        static void DumpFeed(RiverFeed riverFeed)
        {
            if (riverFeed != null)
            {
                Console.WriteLine("{0}", riverFeed.FeedTitle);
                Console.WriteLine(new String('=', riverFeed.FeedTitle.Length));
                Console.WriteLine(riverFeed.FeedDescription);
                Console.WriteLine();

                foreach (RiverItem item in riverFeed.Items)
                {
                    Console.WriteLine(item.Title);
                    Console.WriteLine(new String('-', item.Title.Length));
                    Console.WriteLine("ID:        {0}", item.Id);
                    Console.WriteLine("Link:      {0}", item.Link);
                    Console.WriteLine("Permalink: {0}", item.PermaLink);
                    Console.WriteLine("Thumbnail: {0}", item.Thumbnail?.Url);
                    Console.WriteLine();
                    Console.WriteLine(item.Body);
                    Console.WriteLine();
                    if (item.Enclosures.Count > 0)
                    {
                        Console.WriteLine("  Enclosures:");
                        foreach (RiverItemEnclosure e in item.Enclosures)
                        {
                            Console.WriteLine("    {0} ({1}): {2}", e.Type, e.Length, e.Url);
                        }
                        Console.WriteLine();
                    }
                }
            }
        }
    }
}
