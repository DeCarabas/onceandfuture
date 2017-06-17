namespace onceandfuture
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    public class ParsedOpts
    {
        public string Error;
        public VerbDef Verb;
        public Dictionary<string, Opt> Opts { get; } = new Dictionary<string, Opt>();

        public Opt this[string key] => this.Opts[key];
    }

    public class Opt
    {
        public string Value;
        public int Count;

        public Opt(OptDef optDef)
        {
            this.Option = optDef;
            this.Value = optDef.Default;
        }

        public bool Flag => Count > 0;
        public OptDef Option { get; }
    }

    public class ProgramOpts
    {
        public List<OptDef> Options { get; } = new List<OptDef>();
        public Dictionary<string, VerbDef> Verbs { get; } = new Dictionary<string, VerbDef>();

        public ProgramOpts AddOption(string longName, string help, Func<OptDef, OptDef> func = null)
        {
            var opt = new OptDef(longName, help);
            if (func != null) { opt = func(opt); }
            Options.Add(opt);
            return this;
        }

        public ProgramOpts AddVerb(string verb, string help, Func<ParsedOpts, int> handler, Func<VerbDef, VerbDef> func)
        {
            var vdef = new VerbDef(verb, help, handler);
            if (func != null) { vdef = func(vdef); }
            Verbs.Add(verb, vdef);
            return this;
        }

        public ParsedOpts ParseArguments(string[] args)
        {
            ParsedOpts results = new ParsedOpts();
            for (int i = 0; i < Options.Count; i++)
            {
                results.Opts[Options[i].Long] = new Opt(Options[i]);
            }

            List<OptDef> verbOptions = null;
            for (int argIndex = 0; argIndex < args.Length; argIndex++)
            {
                OptDef opt;
                string arg = args[argIndex];
                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    string[] parts = arg.Substring(2).Split(new char[] { '=' }, 2);
                    string longArg = parts[0];

                    string val = null;
                    if (parts.Length == 2) { val = parts[1]; }

                    opt = Options.Find(o => String.Equals(o.Long, longArg));
                    if (opt == null && verbOptions != null)
                    {
                        opt = verbOptions.Find(o => String.Equals(o.Long, longArg));
                    }

                    HandleOpt(results, arg, opt, val);
                }
                else if (arg[0] == '-')
                {
                    if (arg.Length == 1)
                    {
                        results.Error = String.Format("Unrecognized argument: '{0}'", arg);
                    }
                    else
                    {
                        for (int flagIndex = 1; flagIndex < arg.Length; flagIndex++)
                        {
                            string val = null;
                            char flag = arg[flagIndex];
                            if (flagIndex + 1 < arg.Length && arg[flagIndex] == '=')
                            {
                                val = arg.Substring(flagIndex + 2);
                                flagIndex = arg.Length;
                            }

                            opt = Options.Find(o => o.Short == flag);
                            if (opt == null && verbOptions != null)
                            {
                                opt = verbOptions.Find(o => o.Short == flag);
                            }

                            HandleOpt(results, flag.ToString(), opt, val);
                            if (results.Error != null) { break; }
                        }
                    }
                }
                else
                {
                    if (results.Verb == null && this.Verbs.Count > 0)
                    {
                        VerbDef verbdef;
                        if (!this.Verbs.TryGetValue(arg, out verbdef))
                        {
                            results.Error = String.Format("Unknown verb: {0}", arg);
                        }
                        else
                        {
                            results.Verb = verbdef;
                            verbOptions = verbdef.Options;
                            for (int verbOptionIndex = 0; verbOptionIndex < verbOptions.Count; verbOptionIndex++)
                            {
                                results.Opts[verbOptions[verbOptionIndex].Long] =
                                    new Opt(verbOptions[verbOptionIndex]);
                            }
                        }
                    }
                    else
                    {
                        results.Error = String.Format("Unrecognized argument: '{0}'", arg);
                    }
                }

                if (results.Error != null) { break; }
            }

            // Check for missing verb
            if (results.Error == null && results.Verb == null && this.Verbs.Count > 0)
            {
                results.Error = String.Format(
                    "Did not find a verb; specify one of: {0}", String.Join(", ", this.Verbs.Keys));
            }

            // Check for missing required option
            if (results.Error == null)
            {
                foreach (Opt opt in results.Opts.Values)
                {
                    if (opt.Option.Required && opt.Count == 0)
                    {
                        results.Error = String.Format("Missing required option {0}", opt.Option.Long);
                        break;
                    }
                }
            }

            return results;
        }

        public string GetHelp(VerbDef verb)
        {
            var buffer = new StringWriter();
            var writer = new IndentedTextWriter(buffer);

            if (Options.Count > 0)
            {
                writer.WriteLine("Common options:");
                writer.Indent += 1;
                WriteOptionBlock(writer, Options);
                writer.WriteLine();
                writer.Indent -= 1;
            }

            if (Verbs.Count > 0)
            {
                writer.WriteLine("Commands:");
                writer.Indent += 1;

                var verbs = new List<string>(Verbs.Keys);
                verbs.Sort(StringComparer.Ordinal);

                foreach (string v in verbs)
                {
                    if (verb != null && verb.Name != v) { continue; }
                    WriteVerbBlock(writer, v, Verbs[v]);
                    writer.WriteLine();
                }

                writer.Indent -= 1;
            }

            writer.Flush();
            buffer.Flush();
            return buffer.ToString();
        }

        void WriteVerbBlock(IndentedTextWriter writer, string verb, VerbDef verbDef)
        {
            writer.WriteLine("{0}: {1}", verb, verbDef.Help);
            writer.Indent += 1;
            WriteOptionBlock(writer, verbDef.Options);
            writer.Indent -= 1;
        }

        void WriteOptionBlock(IndentedTextWriter writer, List<OptDef> options)
        {
            const string ValuePlaceholder = "=<value>";
            int columnLimit = 79 - writer.Indent;

            int optwidth = 0;
            foreach (OptDef opt in options)
            {
                int ow = opt.Long.Length + 2;
                if (opt.Short != 0) { ow += 3; }
                if (opt.Value) { ow += ValuePlaceholder.Length; }
                if (ow > optwidth) { optwidth = ow; }
            }

            int descriptionColumn = optwidth + 2;
            foreach (OptDef opt in options)
            {
                int col = 0;
                if (opt.Short != 0)
                {
                    writer.Write('-');
                    writer.Write(opt.Short);
                    writer.Write('|');
                    col += 3;
                }
                writer.Write("--"); col += 2;
                writer.Write(opt.Long); col += opt.Long.Length;
                if (opt.Value)
                {
                    writer.Write(ValuePlaceholder); col += ValuePlaceholder.Length;
                }

                while (col < descriptionColumn) { writer.Write(' '); col++; }

                var optHelp = new StringBuilder(opt.Help);
                if (opt.Required)
                {
                    optHelp.Append(" (Required.)");
                }
                else if (opt.Default != null)
                {
                    optHelp.Append(" (Default is '");
                    optHelp.Append(opt.Default);
                    optHelp.Append("'.)");
                }
                string[] words = optHelp.ToString().Split();
                for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
                {
                    string word = words[wordIndex];
                    if (col + word.Length >= columnLimit)
                    {
                        writer.WriteLine();
                        for (col = 0; col < descriptionColumn; col++) { writer.Write(' '); }
                    }
                    writer.Write(word); col += word.Length;
                    writer.Write(' '); col += 1;
                }
                writer.WriteLine();
            }            
        }

        static void HandleOpt(ParsedOpts results, string arg, OptDef opt, string value)
        {
            if (opt != null)
            {
                Opt optVal = results.Opts[opt.Long];
                if (value != null)
                {
                    if (!opt.Value)
                    {
                        results.Error = String.Format("Argument '{0}' doesn't take a value", arg);
                    }
                    else if (optVal.Count > 0)
                    {
                        results.Error = String.Format("Multiple values found for '{0}'", arg);
                    }
                    else
                    {
                        optVal.Value = value;
                    }
                }
                else if (opt.Value)
                {
                    results.Error = String.Format("Argument '{0}' requires a value", arg);
                }
                optVal.Count++;
            }
            else
            {
                results.Error = String.Format("Unrecognized argument: '{0}'", arg);
            }
        }
    }

    public class VerbDef
    {
        public string Name { get; }
        public string Help { get; }
        public List<OptDef> Options { get; } = new List<OptDef>();
        public Func<ParsedOpts, int> Handler { get; }

        public VerbDef(string name, string help, Func<ParsedOpts, int> handler)
        {
            this.Name = name;
            this.Help = help;
            this.Handler = handler;
        }

        public VerbDef AddOption(string longName, string help, Func<OptDef, OptDef> func = null)
        {
            var opt = new OptDef(longName, help);
            if (func != null) { opt = func(opt); }
            Options.Add(opt);
            return this;
        }
    }

    public class OptDef
    {
        public char Short;
        public string Long;
        public string Help;
        public bool Value;
        public string Default;
        public bool Required;

        public OptDef(string longName, string help)
        {
            Long = longName;
            Short = longName[0];
            Help = help;
        }

        public OptDef AcceptValue()
        {
            Value = true;
            return this;
        }

        public OptDef IsRequired()
        {
            Value = true;
            Required = true;
            return this;
        }

        public OptDef HasDefault(string val)
        {
            Default = val;
            Value = true;
            Required = false;
            return this;
        }

        public OptDef Flag(char flag)
        {
            Short = flag;
            return this;
        }
    }
}
