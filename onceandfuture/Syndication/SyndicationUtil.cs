namespace OnceAndFuture.Syndication
{
    using AngleSharp.Dom;
    using AngleSharp.Dom.Html;
    using AngleSharp.Parser.Html;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Xml.Linq;

    public static class SyndicationUtil
    {
        readonly static Func<string, DateTime?>[] DateParsers = new Func<string, DateTime?>[]
        {
            TryParseDateNative,
            TryParseDateRFC822,
            TryParseDateAscTime,
            TryParseDateGreek,
            TryParseDateHungarian,
            TryParseDateIso8601,
            TryParseDateKorean,
            TryParseDatePerforce,
            TryParseDateW3DTF,
        };

        readonly static Dictionary<string, int> TimeZoneNames = new Dictionary<string, int>
        {
            { "ut", 0 },
            { "gmt", 0 },
            { "z", 0 },
            { "adt", -3 },
            { "ast", -4 },
            { "at", -4 },
            { "edt", -4 },
            { "est", -5 },
            { "et", -5 },
            { "cdt", -5 },
            { "cst", -6 },
            { "ct", -6 },
            { "mdt", -6 },
            { "mst", -7 },
            { "mt", -7 },
            { "pdt", -7 },
            { "pst", -8 },
            { "pt", -8 },
            { "a", -1 },
            { "n", 1 },
            { "m", -12 },
            { "y", 12 },
            { "met", 1 },
            { "mest", 2 },
        };

        readonly static HashSet<string> DayNames = new HashSet<string>
        {
            "mon", "tue", "wed", "thu", "fri", "sat", "sun"
        };

        readonly static Dictionary<string, int> Months = new Dictionary<string, int> {
            {"jan", 1}, {"feb", 2}, {"mar", 3}, {"apr", 4}, {"may", 5}, {"jun", 6},
            {"jul", 7}, {"aug", 8}, {"sep", 9}, {"oct", 10}, {"nov", 11}, {"dec", 12},
        };

        static readonly string[] RiverNames = new[] {
            "Nile", "Kagera", "Amazon", "Ucayali", "Apurímac", "Yangtze", "Mississippi", "Missouri", "Jefferson",
            "Yenisei", "Angara", "Selenge", "Huang He", "Ob", "Irtysh", "Paraná", "Río de la Plata", "Congo",
            "Chambeshi", "Amur", "Argun", "Lena", "Mekong", "Mackenzie", "Slave", "Peace", "Finlay", "Niger", "Murray",
            "Darling", "Tocantins", "Araguaia", "Volga", "Shatt al-Arab", "Euphrates", "Madeira", "Mamoré", "Grande",
            "Caine", "Rocha", "Purús", "Yukon", "Indus", "São Francisco", "Syr Darya", "Naryn", "Salween",
            "Saint Lawrence", "Niagara", "Detroit", "Saint Clair", "Saint Marys", "Saint Louis", "Rio Grande",
            "Lower Tunguska", "Brahmaputra", "Tsangpo", "Danube", "Breg", "Zambezi", "Vilyuy", "Araguaia", "Ganges",
            "Hooghly", "Padma", "Amu Darya", "Panj", "Japurá", "Nelson", "Saskatchewan", "Paraguay", "Kolyma",
            "Pilcomayo", "Upper Ob", "Katun", "Ishim", "Juruá", "Ural", "Arkansas", "Colorado", "Olenyok", "Dnieper",
            "Aldan", "Ubangi", "Uele", "Negro", "Columbia", "Pearl", "Zhu Jiang", "Red", "Ayeyarwady", "Kasai", "Ohio",
            "Allegheny", "Orinoco", "Tarim", "Xingu", "Orange", "Northern Salado", "Vitim", "Tigris", "Songhua",
            "Tapajós", "Don", "Stony Tunguska", "Pechora", "Kama", "Limpopo", "Guaporé", "Indigirka", "Snake",
            "Senegal", "Uruguay", "Murrumbidgee", "Blue Nile", "Churchill", "Khatanga", "Okavango", "Volta", "Beni",
            "Platte", "Tobol", "Jubba", "Shebelle", "Içá", "Magdalena", "Han", "Oka", "Pecos", "Upper Yenisei",
            "Little Yenisei", "Godavari", "Guapay", "Belaya", "Cooper", "Barcoo", "Marañón", "Dniester", "Benue",
            "Ili", "Warburton", "Georgina", "Sutlej", "Yamuna", "Vyatka", "Fraser", "Mtkvari", "Grande", "Brazos",
            "Cauca", "Liao", "Yalong", "Iguaçu", "Olyokma", "Northern Dvina", "Sukhona", "Krishna", "Iriri", "Narmada",
            "Lomami", "Ottawa", "Lerma", "Rio Grande de Santiago", "Elbe", "Vltava", "Zeya", "Juruena",
            "Upper Mississippi", "Rhine", "Athabasca", "Canadian", "North Saskatchewan", "Vaal", "Shire", "Nen",
            "Kızıl", "Green", "Milk", "Chindwin", "Sankuru", "Wu", "James", "Kapuas", "Desna", "Helmand",
            "Madre de Dios", "Tietê", "Vychegda", "Sepik", "Cimarron", "Anadyr", "Paraíba do Sul", "Jialing River",
            "Liard", "Cumberland", "White", "Huallaga", "Kwango", "Draa", "Gambia", "Chenab", "Yellowstone",
            "Ghaghara", "Huai", "Aras", "Seversky Donets", "Bermejo", "Fly", "Guaviare", "Kuskokwim", "Tennessee",
            "Vistula", "Aruwimi", "Daugava", "Gila", "Loire", "Essequibo", "Khoper", "Tagus",
        };

        public static string RiverName(IList<RiverDefinition> rivers)
        {
            int index = (rivers.Count + 1) % RiverNames.Length;
            while (rivers.Any(rd => rd.Name == RiverNames[index]))
            {
                index = (index + 1) % RiverNames.Length;
            }
            return RiverNames[index];
        }

        public static IEnumerable<TItem> ConcatSequence<TItem>(params IEnumerable<TItem>[] sequences)
        {
            return sequences.SelectMany(x => x);
        }

        public static string HashString(string input) => HashBytes(Encoding.UTF8.GetBytes(input));

        public static string HashBytes(byte[] input)
        {
            byte[] hash = SHA1.Create().ComputeHash(input);
            return Convert.ToBase64String(hash).Replace('/', '-');
        }

        public static string MakeID() => Guid.NewGuid().ToString("N");

        public static string ParseBody(XElement body)
        {
            var parser = new HtmlParser();
            IHtmlDocument document = parser.Parse(body.Value);

            return HtmlFormatter.Format(document.Body);
        }

        public static DateTime? ParseDate(XElement dateTime)
        {
            for (int i = 0; i < DateParsers.Length; i++)
            {
                DateTime? result = DateParsers[i](dateTime.Value);
                if (result != null) { return result; }
            }

            Log.BadDate(dateTime.BaseUri, dateTime.Value);
            return null;
        }

        public static Uri ParseLink(string value, XElement xe)
        {
            return TryParseUrl(value, null, xe);
        }

        public static Uri TryParseUrl(string url, Uri baseUri = null, XElement xe = null)
        {
            // Create a fully-resolved URI as best as you know how to do it.
            if (String.IsNullOrWhiteSpace(url)) { return null; }

            Uri result;
            if (url[0] == '/')
            {
                // If we send this to Uri.TryCreate, it will make an absolute
                // file:// Uri which... is not useful. We need to force it to
                // be a relative URL, which means we're gonna need some kind
                // of base.
                if (!Uri.TryCreate(url, UriKind.Relative, out result)) { return null; }
            }
            else
            {
                if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out result)) { return null; }
            }

            if (result.IsAbsoluteUri) { return result; }

            // If we are reading this URL from an XElement then respect the
            // base URL that might be set on the document.
            if (xe != null)
            {
                Uri xeBase = TryParseAbsoluteUrl(xe.BaseUri, baseUri);
                if (xeBase != null) { baseUri = xeBase; }
            }

            // No base, can't refine further.
            if (baseUri == null) { return result; }

            Uri absoluteUri;
            if (!Uri.TryCreate(baseUri, result, out absoluteUri)) { return result; }
            return absoluteUri;
        }

        public static Uri TryParseAbsoluteUrl(string url, Uri baseUri = null, XElement xe = null)
        {
            Uri result = TryParseUrl(url, baseUri, xe);
            if (result == null) { return null; }
            if (!result.IsAbsoluteUri) { return null; }
            return result;
        }

        public static Uri Rebase(Uri link, Uri baseUri)
        {
            if (link == null) { return null; }
            if (link.IsAbsoluteUri) { return link; }
            if (baseUri == null) { return link; }

            Uri absUri;
            if (!Uri.TryCreate(baseUri, link, out absUri)) { return link; }
            return baseUri;
        }

        static string Slice(string str, int min, int max)
        {
            if (min >= str.Length) { return String.Empty; }
            if (max > str.Length) { max = str.Length; }
            return str.Substring(min, max - min);
        }

        static string ThreeChar(string str) => str.Length <= 3 ? str : str.Substring(0, 3);

        static DateTime? TryParseDateAscTime(string timeString)
        {
            // Like:
            //
            //    {weekday_name} {month_name} dd hh:mm:ss {+-tz} yyyy
            //    {weekday_name} {month_name} dd hh:mm:ss yyyy
            //
            List<string> parts = new List<string>(timeString.Split());
            if (parts.Count == 5) { parts.Insert(4, "+0000"); }
            if (parts.Count != 6) { return null; }
            return TryParseDateRFC822(String.Join(" ", parts[0], parts[2], parts[1], parts[5], parts[3], parts[4]));
        }

        static DateTime? TryParseDateGreek(string timeString) => null;

        static DateTime? TryParseDateHungarian(string timeString) => null;

        static DateTime? TryParseDateIso8601(string timeString) => null;

        static DateTime? TryParseDateKorean(string timeString) => null;

        static DateTime? TryParseDatePerforce(string timeString) => null;

        static DateTime? TryParseDateRFC822(string timeString)
        {
            List<string> parts = new List<string>(timeString.ToLowerInvariant().Split());
            if (parts.Count < 5)
            {
                // Assume that the time and TZ are missing.
                parts.Add("00:00:00");
                parts.Add("0000");
            }

            // If there's a day name at the front, remove it.
            if (DayNames.Contains(ThreeChar(parts[0]))) { parts.RemoveAt(0); }

            // If we don't have at least 5 parts now there's not enough information to interpret.
            if (parts.Count < 5) { return null; }

            // Handle day and month name.
            int month = 0, day = 0;
            if (!(Months.TryGetValue(ThreeChar(parts[1]), out month) && int.TryParse(parts[0], out day)))
            {
                // Maybe month and day are swapped.
                if (!(Months.TryGetValue(ThreeChar(parts[0]), out month) && int.TryParse(parts[1], out day)))
                {
                    // Nope.
                    return null;
                }
            }
            if (month > 12) { return null; }
            if (day > 31) { return null; }

            // Year, including 2-digit years.
            int year = 0;
            if (!int.TryParse(parts[2], out year)) { return null; }
            if (year < 100) { year += (year < 90) ? 2000 : 1900; }
            if (year > 9999) { return null; }

            // Time.
            int hour = 0;
            int minute = 0;
            float second = 0;
            string[] timeParts = parts[3].Split(':');
            if (timeParts.Length > 0 && !int.TryParse(timeParts[0], out hour)) { return null; }
            if (timeParts.Length > 1 && !int.TryParse(timeParts[1], out minute)) { return null; }
            if (timeParts.Length > 2 && !float.TryParse(timeParts[2], out second)) { return null; }

            // Timezone
            if (parts[4].StartsWith("etc/")) { parts[4] = parts[4].Substring(4); }
            if (parts[4].StartsWith("gmt"))
            {
                // Normalize timezones that start with GMT:
                // GMT-05:00 => -0500
                // GMT => GMT
                parts[4] = String.Join("", parts[4].Substring(3).Split(':'));
                if (parts[4].Length == 0) { parts[4] = "gmt"; }
            }

            // Handle timezones like '-0500', '+0500', and 'EST'
            int tz_hours = 0;
            int tz_minutes = 0;
            if (parts[4].Length >= 3 && (parts[4][0] == '-' || parts[4][0] == '+'))
            {
                if (!int.TryParse(Slice(parts[4], 1, 3), out tz_hours)) { return null; }
                if (parts[4].Length > 3)
                {
                    if (!int.TryParse(parts[4].Substring(3), out tz_minutes)) { return null; }
                }
            }
            else
            {
                if (!TimeZoneNames.TryGetValue(parts[4], out tz_hours)) { tz_hours = 0; }
            }

            // OK!
            try
            {
                TimeSpan offset = new TimeSpan(tz_hours, tz_minutes, 0);
                return new DateTimeOffset(year, month, day, hour, minute, (int)second, offset).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        static DateTime? TryParseDateW3DTF(string timeString) => null;

        static DateTime? TryParseDateNative(string timeString)
        {
            if (DateTimeOffset.TryParse(timeString, out DateTimeOffset result))
            {
                return result.UtcDateTime;
            }
            return null;
        }

        class HtmlFormatter
        {
            readonly StringBuilder builder = new StringBuilder();

            HtmlFormatter() { }

            void AppendWhitespace()
            {
                if (builder.Length > 0 && !Char.IsWhiteSpace(builder[builder.Length - 1]))
                {
                    builder.Append(" ");
                }
            }


            public static string Format(IHtmlElement element)
            {
                var formatter = new HtmlFormatter();
                formatter.Visit(element);
                return formatter.builder.ToString();
            }

            bool Visit(INode node)
            {
                switch (node.NodeType)
                {
                    case NodeType.Element:
                        if (node.NodeName == "SCRIPT") { break; }
                        if (node.NodeName == "FIGURE") { break; }

                        foreach (INode child in node.ChildNodes)
                        {
                            if (!Visit(child)) { return false; }
                        }
                        if (node.NodeName == "P" || node.NodeName == "DIV" || node.NodeName == "BR")
                        {
                            AppendWhitespace();
                        }
                        break;

                    case NodeType.Text:
                    case NodeType.CharacterData:
                    case NodeType.EntityReference:
                        for (int i = 0; i < node.TextContent.Length && builder.Length < 280; i++)
                        {
                            if (Char.IsWhiteSpace(node.TextContent[i]))
                            {
                                AppendWhitespace();
                            }
                            else
                            {
                                builder.Append(node.TextContent[i]);
                            }
                        }
                        break;

                    default:
                        break;
                }

                if (builder.Length > 280)
                {
                    builder.Length = 277;
                    builder.Append("...");
                    return false;
                }

                return true;
            }
        }
    }

}
