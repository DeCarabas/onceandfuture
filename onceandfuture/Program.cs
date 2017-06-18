namespace OnceAndFuture
{
    using ImageSharp;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Serilog;
    using Serilog.Events;
    using Serilog.Formatting.Compact;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    class Program
    {
        static ProgramOpts Options = new ProgramOpts()
            .AddOption("help", "Display this help.", o => o.Flag('?'))
            .AddOption("verbose", "Increase the logging verbosity. (Specify more than once to be even more verbose.)")
            .AddVerb("update", "Update one or more feeds.", DoUpdate, v => v
                .AddOption("feed", "The single feed URL to update.", o => o.AcceptValue())
                .AddOption("user", "The user to update feeds for.", o => o.AcceptValue())
                .AddOption("river", "The river to update.", o => o.AcceptValue())
            )
            .AddVerb("show", "Show items in one or more feeds.", DoShow, v => v
                .AddOption("feed", "The single feed URL to show.", o => o.AcceptValue())
                .AddOption("user", "The user to show for.", o => o.AcceptValue())
                .AddOption("river", "The ID of the aggregated river to show.", o => o.AcceptValue())
                .AddOption("noload", "Fetch from the internet rather than loading from the store.")
            )
            .AddVerb("sub", "Subscribe to a feed.", DoSubscribe, v => v
                .AddOption("user", "The user to add a subscription for.", o => o.IsRequired())
                .AddOption("river", "The river to add to.", o => o.HasDefault("main"))
                .AddOption("feed", "The feed to add.", o => o.IsRequired())
            )
            .AddVerb("unsub", "Unsubscribe from a feed.", DoUnsubscribe, v => v
                .AddOption("user", "The user to remove a subscription for.", o => o.IsRequired())
                .AddOption("river", "The river to remove from.", o => o.HasDefault("main"))
                .AddOption("feed", "The feed to remove.", o => o.IsRequired())
            )
            .AddVerb("list", "List a user's subscriptions.", DoList, v => v
                .AddOption("user", "The user whose subscriptions we're showing.", o => o.IsRequired())
            )
            .AddVerb("serve", "Start the web server.", DoServe, v => v
                .AddOption("url", "The URL to listen on.", o => o.HasDefault("http://localhost:5000"))
                .AddOption("environment", "The environment to run as.", o => o.HasDefault("Development"))
            )
            .AddVerb("setpw", "Set a user's password", DoSetPassword, v => v
                .AddOption("user", "The user to set the password for.", o => o.IsRequired())
                .AddOption("password", "The password to set it to.", o => o.IsRequired())
            )
            .AddVerb("setemail", "Set a user's email address", DoSetEmail, v => v
                .AddOption("user", "The user to set the password for.", o => o.IsRequired())
                .AddOption("email", "The address to set it to.", o => o.IsRequired())
            )
            .AddVerb("resetlogin", "Reset a user's login cookies", DoResetLogins, v => v
                .AddOption("user", "The user to reset.", o => o.IsRequired())
            )
            .AddVerb("findfeed", "Find the feed for an URL", DoFindFeed, v => v
                .AddOption("url", "The URL to find a feed for.", o => o.IsRequired())
            )
            .AddVerb("thumbnail", "Load an image and produce a thumbnail", DoThumbnail, v => v
                .AddOption("url", "The URL of the image to load.", o => o.IsRequired())
                .AddOption("out", "The file to write.", o => o.IsRequired())
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

                ConfigureGlobalSettings(parsedArgs);

                int result = parsedArgs.Verb.Handler(parsedArgs);
                Serilog.Log.CloseAndFlush();
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 99;
            }
        }

        static bool IsHealthPingEvent(LogEvent le)
            => le.Properties.ContainsKey("RequestPath")
            && le.Properties["RequestPath"].ToString() == "\"/health/ping\"";

        static bool IsDevelopmentEnvironment(ParsedOpts parsedArgs)
        {
            if (!parsedArgs.Opts.ContainsKey("environment")) { return true; }
            if (parsedArgs["environment"].Value.ToLowerInvariant() == "development") { return true; }
            return false;
        }

        static void ConfigureGlobalSettings(ParsedOpts parsedArgs)
        {
            var logLevel = (LogEventLevel)Math.Max((int)(LogEventLevel.Information - parsedArgs["verbose"].Count), 0);
            var logConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(logLevel)
                .Filter.ByExcluding(e => IsHealthPingEvent(e));

            if (IsDevelopmentEnvironment(parsedArgs))
            {
                logConfig.WriteTo.LiterateConsole(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {RequestId}] {Message}{NewLine}{Exception}"
                );
            }
            else
            {
                logConfig.WriteTo.Console(new RenderedCompactJsonFormatter());
            }

            string honeycomb_key = Environment.GetEnvironmentVariable("HONEYCOMB_KEY");
            if (honeycomb_key != null)
            {
                Console.WriteLine("(Honeycomb configured.)");
                logConfig.WriteTo.Honeycomb("server", honeycomb_key);
            }
            Serilog.Log.Logger = logConfig.CreateLogger();

            ThumbnailExtractor.ConfigureProcess();
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
            else if (args["river"].Value != null)
            {
                return DoShowRiver(args);
            }
            else
            {
                Console.Error.WriteLine("Must specify user, feed, or river.");
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

            River river;
            if (args["noload"].Flag)
            {
                var cleanRiver = new River(metadata: new RiverFeedMeta(originUrl: feedUrl));
                river = new RiverFeedParser().UpdateAsync(cleanRiver).Result;
            }
            else
            {
                var feedStore = new RiverFeedStore();
                river = feedStore.LoadRiverForFeed(feedUrl).Result;
            }

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

            return 0;
        }

        static int DoShowRiver(ParsedOpts args)
        {
            string aggregateId = args["river"].Value;
            var aggregateStore = new AggregateRiverStore();

            River river = aggregateStore.LoadAggregate(aggregateId).Result;
            if (river.UpdatedFeeds.Feeds.Count > 0)
            {
                foreach (RiverFeed feed in river.UpdatedFeeds.Feeds)
                {
                    DumpFeed(feed);
                }
            }
            else
            {
                Console.WriteLine("No data for {0}", aggregateId);
            }

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
                if (args["river"].Value != null)
                {
                    return DoUpdateForRiver(args);
                }
                else
                {
                    return DoUpdateForUser(args);
                }
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

            Console.WriteLine("Refreshing {0}...", feedUrl);
            Stopwatch loadTimer = Stopwatch.StartNew();
            parser.FetchAndUpdateRiver(feedUrl).Wait();
            Console.WriteLine("Refreshed {0} in {1}", feedUrl, loadTimer.Elapsed);
            return 0;
        }

        static int DoUpdateForRiver(ParsedOpts args)
        {
            string user = args["user"].Value;
            string river = args["river"].Value;

            var subscriptionStore = new UserProfileStore();
            var parser = new RiverFeedParser();

            Console.WriteLine("Refreshing for {0}/{1}...", user, river);
            Stopwatch loadTimer = Stopwatch.StartNew();

            UserProfile profile = subscriptionStore.GetProfileFor(user).Result;
            var tasks =
                from rd in profile.Rivers
                where rd.Id == river
                select parser.RefreshAggregateRiverWithFeeds(rd.Id, rd.Feeds);
            Task.WhenAll(tasks).Wait();

            Console.WriteLine("Refreshed {0} rivers in {1}", profile.Rivers.Count, loadTimer.Elapsed);
            return 0;
        }

        static int DoUpdateForUser(ParsedOpts args)
        {
            string user = args["user"].Value;

            var subscriptionStore = new UserProfileStore();
            var parser = new RiverFeedParser();

            Console.WriteLine("Refreshing for {0}...", user);
            Stopwatch loadTimer = Stopwatch.StartNew();

            UserProfile profile = subscriptionStore.GetProfileFor(user).Result;
            var tasks = from rd in profile.Rivers select parser.RefreshAggregateRiverWithFeeds(rd.Id, rd.Feeds);
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
            River feedRiver = parser.FetchAndUpdateRiver(new Uri(feed)).Result;
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
                Console.WriteLine("{0} ({1}):", river.Name, river.Id);
                if (river.Feeds.Count > 0)
                {
                    foreach (var feed in river.Feeds)
                    {
                        Console.WriteLine("  {0}", feed);
                    }
                }
                else
                {
                    Console.WriteLine("  No feeds.");
                }
                Console.WriteLine();
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

        static int DoSetEmail(ParsedOpts args)
        {
            string user = args["user"].Value;
            string email = args["email"].Value;

            var profileStore = new UserProfileStore();
            var profile = profileStore.GetProfileFor(user).Result;
            var newProfile = profile.With(
                email: email,
                emailVerified: false);
            profileStore.SaveProfileFor(user, newProfile).Wait();
            Console.WriteLine("OK");
            return 0;
        }

        static int DoSetPassword(ParsedOpts args)
        {
            string user = args["user"].Value;
            string password = args["password"].Value;

            var profileStore = new UserProfileStore();
            var profile = profileStore.GetProfileFor(user).Result;
            var newProfile = profile.With(
                password: AuthenticationManager.EncryptPassword(password),
                logins: new LoginCookie[0]);
            profileStore.SaveProfileFor(user, newProfile).Wait();
            Console.WriteLine("OK");
            return 0;
        }

        static int DoResetLogins(ParsedOpts args)
        {
            string user = args["user"].Value;

            var profileStore = new UserProfileStore();
            var profile = profileStore.GetProfileFor(user).Result;
            var newProfile = profile.With(logins: new LoginCookie[0]);
            profileStore.SaveProfileFor(user, newProfile).Wait();
            Console.WriteLine("OK");
            return 0;
        }

        static int DoFindFeed(ParsedOpts args)
        {
            string url = args["url"].Value;
            Console.WriteLine("Finding feed for {0}...", url);
            IList<Uri> found = FeedDetector.GetFeedUrls(args["url"].Value).Result;
            foreach (Uri uri in found)
            {
                Console.WriteLine("{0}", uri.AbsoluteUri);
            }

            Console.WriteLine("Found {0} feeds", found.Count);
            return 0;
        }

        static int DoThumbnail(ParsedOpts args)
        {
            Uri url = new Uri(args["url"].Value);

            Image<Rgba32> sourceImage = ThumbnailExtractor.FindImageAsync(url).Result;
            if (sourceImage == null)
            {
                Console.Error.WriteLine("No image found @ {0}.", url);
                return 1;
            }

            Image<Rgba32> thumb = ThumbnailExtractor.MakeThumbnail(sourceImage);
            using (var stream = File.Create(args["out"].Value))
            {
                thumb.SaveAsPng(stream);
            }
            return 0;
        }

        static void DumpFeed(RiverFeed riverFeed)
        {
            if (riverFeed != null)
            {
                Console.WriteLine("{0}", riverFeed.FeedTitle);
                Console.WriteLine(new String('=', riverFeed.FeedTitle.Length));
                Console.WriteLine("Updated: {0}", riverFeed.WhenLastUpdate);
                Console.WriteLine("Description: {0}", riverFeed.FeedDescription);
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
