namespace onceandfuture
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Serilog;
    using Serilog.Configuration;
    using Serilog.Events;
    using Serilog.Sinks.PeriodicBatching;

    public class HoneycombSink : PeriodicBatchingSink
    {
        public const string DatasetPropertyKey = "HoneycombDataset";

        HttpClient client;
        string defaultDataset;

        public HoneycombSink(string defaultDataset, string writeKey, int batchSizeLimit, TimeSpan period)
            : base(batchSizeLimit, period)
        {
            this.client = new HttpClient();
            this.client.BaseAddress = new Uri("https://api.honeycomb.io/1/");
            this.client.DefaultRequestHeaders.Add("X-Honeycomb-Team", writeKey);

            this.defaultDataset = defaultDataset;

        }

        protected override bool CanInclude(LogEvent evt)
        {
            return base.CanInclude(evt);
        }

        protected override Task EmitBatchAsync(IEnumerable<LogEvent> events)
            => Task.WhenAll(events.Select(e => SendEvent(e)).ToArray());

        async Task SendEvent(LogEvent logEvent)
        {
            LogEventPropertyValue datasetProperty = null;
            logEvent.Properties.TryGetValue(DatasetPropertyKey, out datasetProperty);
            string dataset = ((datasetProperty as ScalarValue)?.Value as string) ?? this.defaultDataset;

            var request = new HttpRequestMessage(HttpMethod.Post, "events/" + dataset)
            {
                Content = new StringContent(
                    content: JsonConvert.SerializeObject(EventToJObject(logEvent)),
                    encoding: Encoding.UTF8,
                    mediaType: "application/json"
                ),
                Headers =
                {
                    { "X-Honeycomb-Event-Time", logEvent.Timestamp.ToString("o") },
                }
            };

            using (HttpResponseMessage message = await client.SendAsync(request))
            {
                // TODO: Log if unsuccessful. This requires... hum.
                if (!message.IsSuccessStatusCode)
                {
                    string msg = await message.Content.ReadAsStringAsync();
                    Console.WriteLine($"Warning: Failed to send event: {message.StatusCode}: {msg}");
                }
            }
        }

        public static JObject EventToJObject(LogEvent logEvent)
        {
            JObject evt = new JObject(
                new JProperty("message", logEvent.RenderMessage()),
                new JProperty("level", logEvent.Level.ToString())
            );
            foreach (KeyValuePair<string, LogEventPropertyValue> prop in logEvent.Properties)
            {
                if (prop.Key == DatasetPropertyKey) { continue; }
                evt[prop.Key] = ConvertPropertyValue(prop.Value);
            }

            return evt;
        }

        static JToken ConvertPropertyValue(LogEventPropertyValue value)
        {
            var scalar = value as ScalarValue;
            if (scalar != null) { return ConvertScalarValue(scalar); }

            var seq = value as SequenceValue;
            if (seq != null) { return ConvertSequenceValue(seq); }

            var strct = value as StructureValue;
            if (strct != null) { return ConvertStructureValue(strct); }

            var dict = value as DictionaryValue;
            if (dict != null) { return ConvertDictionaryValue(dict); }

            // TODO: Log or something.
            return JValue.CreateUndefined();
        }

        static JToken ConvertDictionaryValue(DictionaryValue dict)
        {
            JObject obj = new JObject();
            foreach (KeyValuePair<ScalarValue, LogEventPropertyValue> kvp in dict.Elements)
            {
                string propname = kvp.Key.Value?.ToString() ?? "<<null>>";
                obj.Add(propname, ConvertPropertyValue(kvp.Value));
            }
            return obj;
        }

        static JToken ConvertStructureValue(StructureValue strct)
        {
            JObject obj = new JObject();
            obj.Add("__type", new JValue(strct.TypeTag));
            foreach (LogEventProperty prop in strct.Properties)
            {
                obj.Add(prop.Name, ConvertPropertyValue(prop.Value));
            }
            return obj;
        }

        static JToken ConvertSequenceValue(SequenceValue seq)
        {
            JArray arr = new JArray();
            foreach (LogEventPropertyValue elem in seq.Elements)
            {
                arr.Add(ConvertPropertyValue(elem));
            }
            return arr;
        }

        static JToken ConvertScalarValue(ScalarValue scalar)
        {
            return new JValue(scalar.Value);
        }
    }

    public static class LoggerConfigurationHoneycombExtensions
    {
        public static LoggerConfiguration Honeycomb(
            this LoggerSinkConfiguration sinkConfiguration,
            string dataset,
            string writeKey,
            int batchSizeLimit = 50,
            TimeSpan? period = null,
            LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
        {
            return sinkConfiguration.Sink(
                new HoneycombSink(dataset, writeKey, batchSizeLimit, period ?? TimeSpan.FromMinutes(1)),
                restrictedToMinimumLevel
            );
        }
    }

}
