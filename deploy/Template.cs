using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace deploy
{
    class Template
    {
        readonly List<TemplateSection> sections = new List<TemplateSection>();

        public Template(string text)
        {
            this.sections = ParseTemplate(text);
        }

        public string Format(IDictionary<string, object> values)
        {
            var newContext = new TemplateContext(values);
            var builder = new StringBuilder();

            for (int i = 0; i < this.sections.Count; i++)
            {
                this.sections[i].Render(newContext, builder);
            }

            return builder.ToString();
        }

        static List<TemplateSection> ParseTemplate(string text)
        {
            int position = 0;
            int literalStart = 0;
            Stack<ParserStackEntry> stack = new Stack<ParserStackEntry>();

            var sections = new List<TemplateSection>();
            while (!IsEOF())
            {
                if (LookingAt("{{"))
                {
                    CaptureLiteralText();

                    if (LookingAt("{{#"))
                    {
                        position += 3;
                        string key = ExtractKey();

                        stack.Push(new ParserStackEntry { key = key, sections = sections });
                        sections = new List<TemplateSection>();
                    }
                    else if (LookingAt("{{/"))
                    {
                        position += 3;
                        string key = ExtractKey();

                        if (stack.Count == 0)
                        {
                            throw Error($"Found a close for '{key}', but no section is open.");
                        }
                        ParserStackEntry top = stack.Pop();
                        if (top.key != key)
                        {
                            throw Error($"Mismatched close; expected a close for '{top.key}' but got '{key}'.");
                        }
                        NestedSection section = new NestedSection(key, sections);
                        sections = top.sections;
                        sections.Add(section);
                    }
                    else
                    {
                        position += 2;
                        string key = ExtractKey();

                        sections.Add(new VariableSection(key));
                    }

                    literalStart = position;
                }
                else
                {
                    position++;
                }
            }

            CaptureLiteralText();
            if (stack.Count > 0)
            {
                throw Error($"Unexpected end of template; did not find a close for section '{stack.Peek().key}'.");
            }

            return sections;

            void CaptureLiteralText()
            {
                if (position != literalStart)
                {
                    string literal = text.Substring(literalStart, position - literalStart);
                    sections.Add(new LiteralSection(literal));
                }
            }

            bool IsEOF()
            {
                return position >= text.Length;
            }

            Exception Error(string v)
            {
                throw new InvalidDataException($"{v} (at position {position}");
            }

            string ExtractKey()
            {
                if (IsEOF()) { throw Error("Unexpected end of template; did not find '}}'"); }
                int end = text.IndexOf("}}", position);
                if (end < 0) { throw Error("Unexpected end of template; did not find '}}'"); }
                string key = text.Substring(position, end - position);
                position = end + 2;

                return key;
            }

            bool LookingAt(string token)
            {
                int i = position;
                int j = 0;
                while (i < text.Length)
                {
                    if (j == token.Length) { return true; }
                    if (text[i] != token[j]) { break; }
                    i++; j++;
                }
                return false;
            }
        }        

        class ParserStackEntry
        {
            public string key;
            public List<TemplateSection> sections;
        }

        class TemplateContext
        {
            public TemplateContext parent;
            public IDictionary<string, object> values;

            public TemplateContext(IDictionary<string, object> values, TemplateContext parent = null)
            {
                this.parent = parent;
                this.values = values;
            }

            public object Lookup(string key)
            {
                TemplateContext context = this;
                while (context != null)
                {
                    if (context.values.TryGetValue(key, out object value)) { return value; }
                    context = context.parent;
                }

                throw new KeyNotFoundException();
            }
        }

        abstract class TemplateSection
        {
            public abstract void Render(TemplateContext context, StringBuilder builder);
        }

        class LiteralSection : TemplateSection
        {
            readonly string value;

            public LiteralSection(string value) { this.value = value; }

            public override void Render(TemplateContext context, StringBuilder builder)
            {
                builder.Append(this.value);
            }
        }

        class VariableSection : TemplateSection
        {
            readonly string variable;

            public VariableSection(string variable) { this.variable = variable; }

            public override void Render(TemplateContext context, StringBuilder builder)
            {
                object value = context.Lookup(this.variable);
                if (value != null) { builder.Append(value); }
            }
        }

        class NestedSection : TemplateSection
        {
            readonly string variable;
            readonly List<TemplateSection> sections;

            public NestedSection(string variable, List<TemplateSection> sections)
            {
                this.variable = variable;
                this.sections = sections;
            }

            public override void Render(TemplateContext context, StringBuilder builder)
            {
                object value = context.Lookup(variable);
                if (value != null)
                {
                    if (value is IDictionary<string, object> dictionary)
                    {
                        RenderDictionary(context, builder, dictionary);
                    }
                    else if (value is IList list)
                    {
                        RenderList(context, builder, list);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Cannot handle context of type {value.GetType()} in section"
                        );
                    }
                }
            }

            void RenderList(TemplateContext context, StringBuilder builder, IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    object item = list[i];
                    if (item is IDictionary<string, object> dictionary)
                    {
                        RenderDictionary(context, builder, dictionary);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot handle item of type {item.GetType()} in section");
                    }
                }
            }

            void RenderDictionary(TemplateContext context, StringBuilder builder, IDictionary<string, object> values)
            {
                var newContext = new TemplateContext(values, context);
                for (int i = 0; i < this.sections.Count; i++)
                {
                    this.sections[i].Render(newContext, builder);
                }
            }
        }
    }
}
