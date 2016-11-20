using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AngleSharp.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;


namespace onceandfuture
{

    public class HomeController : Controller
    {
        [HttpGet("/")]
        public IActionResult Index() => Ok(); // Index.cshtml
    }

    class WebStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // enable MVC framework
            services.AddMvc();
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
            List<OpmlEntry> feeds;
            if (args["feed"].Value != null)
            {
                Uri feedUrl;
                if (!Uri.TryCreate(args["feed"].Value, UriKind.Absolute, out feedUrl))
                {
                    Console.Error.WriteLine("Feed not a valid url: {0}", args["feed"].Value);
                    return 100;
                }
                feeds = new List<OpmlEntry> { new OpmlEntry(xmlUrl: feedUrl) };
            }
            else
            {
                XDocument doc = XDocument.Load(@"C:\Users\John\Downloads\NewsBlur-DeCarabas-2016-11-08");
                XElement body = doc.Root.Element(XNames.OPML.Body);

                feeds =
                    (from elem in body.Descendants(XNames.OPML.Outline)
                     where elem.Attribute(XNames.OPML.XmlUrl) != null
                     select OpmlEntry.FromXml(elem)).ToList();
            }

            var feedStore = new RiverFeedStore();
            foreach (var entry in feeds)
            {
                River river = feedStore.LoadRiverForFeed(entry.XmlUrl).Result;
                if (river.UpdatedFeeds.Feeds.Count > 0)
                {
                    foreach (RiverFeed feed in river.UpdatedFeeds.Feeds)
                    {
                        DumpFeed(feed);
                    }
                }
                else
                {
                    Console.WriteLine("No data for {0}", entry.XmlUrl);
                }

                Console.WriteLine("(Press enter to continue)");
                Console.ReadLine();
            }
            return 0;
        }

        static int DoUpdate(ParsedOpts args)
        {
            List<Uri> feeds;
            if (args["feed"].Value != null)
            {
                Uri feedUrl;
                if (!Uri.TryCreate(args["feed"].Value, UriKind.Absolute, out feedUrl))
                {
                    Console.Error.WriteLine("Feed not a valid url: {0}", args["feed"].Value);
                    return 100;
                }
                feeds = new List<Uri> { feedUrl };
            }
            else if (args["user"].Value != null)
            {
                UserProfile profile = SubscriptionStore.GetProfileFor(args["user"].Value).Result;
                feeds = (from river in profile.Rivers from feed in river.Feeds select feed).ToList();
            }
            else
            {
                Console.Error.WriteLine("Must specify either user or feed.");
                return -1;
            }

            Stopwatch loadTimer = Stopwatch.StartNew();

            var parser = new RiverFeedParser();
            var feedStore = new RiverFeedStore();
            var parses =
                (from entry in feeds
                 select new
                 {
                     url = entry,
                     task = parser.FetchAndUpdateRiver(feedStore, entry, CancellationToken.None),
                 }).ToList();

            Console.WriteLine("Started {0} feeds...", parses.Count);
            Task<River[]> doneTask = Task.WhenAll(parses.Select(p => p.task).ToArray()).ContinueWith(t =>
            {
                loadTimer.Stop();
                return t.Result;
            });

            doneTask.Wait();
            Console.WriteLine("Refreshed {0} feeds in {1}", parses.Count, loadTimer.Elapsed);
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


            UserProfile profile = SubscriptionStore.GetProfileFor(user).Result;
            RiverDefinition river = profile.Rivers.FirstOrDefault(r => r.Name == riverName);

            UserProfile newProfile;
            if (river == null)
            {
                newProfile = new UserProfile(
                    otherProfile: profile,
                    rivers: profile.Rivers.Add(
                        new RiverDefinition(
                            name: riverName,
                            feeds: new Uri[] { feedRiver.Metadata.OriginUrl })));
            }
            else
            {
                var newRiver = new RiverDefinition(
                    otherRiver: river,
                    feeds: river.Feeds.Add(feedRiver.Metadata.OriginUrl));

                newProfile = new UserProfile(
                    otherProfile: profile,
                    rivers: profile.Rivers.Replace(river, newRiver));
            }

            SubscriptionStore.SaveProfileFor(user, newProfile).Wait();

            Console.WriteLine("OK");
            return 0;
        }

        static int DoList(ParsedOpts args)
        {
            string user = args["user"].Value;
            UserProfile profile = SubscriptionStore.GetProfileFor(user).Result;
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

            UserProfile profile = SubscriptionStore.GetProfileFor(user).Result;
            RiverDefinition river = profile.Rivers.FirstOrDefault(r => r.Name == riverName);
            if (river == null)
            {
                Console.WriteLine("River {0} not found.", riverName);
                return 0;
            }

            RiverDefinition newRiver = new RiverDefinition(otherRiver: river, feeds: river.Feeds.Remove(feed));
            UserProfile newProfile = new UserProfile(
                otherProfile: profile,
                rivers: profile.Rivers.Replace(river, newRiver));

            SubscriptionStore.SaveProfileFor(user, newProfile).Wait();
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
