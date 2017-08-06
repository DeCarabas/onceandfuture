using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace OnceAndFuture.Templates
{
    abstract class TemplateSection
    {
        public abstract void Render(TemplateContext context, StringBuilder builder);
    }

    class LiteralTemplateSection : TemplateSection
    {
        readonly string value;

        public LiteralTemplateSection(string value) { this.value = value; }

        public override void Render(TemplateContext context, StringBuilder builder)
        {
            builder.Append(this.value);
        }
    }

    class VariableReferenceTemplateSection : TemplateSection
    {
        readonly string variable;

        public VariableReferenceTemplateSection(string variable) { this.variable = variable; }

        public override void Render(TemplateContext context, StringBuilder builder)
        {
            object value = context.Lookup(this.variable);
            if (value != null) { builder.Append(value); }
        }
    }

    class BlockTemplateSection : TemplateSection
    {
        readonly string variable;
        readonly List<TemplateSection> sections;

        public BlockTemplateSection(string variable, List<TemplateSection> sections)
        {
            this.variable = variable;
            this.sections = sections;
        }

        public override void Render(TemplateContext context, StringBuilder builder)
        {
            if (context.TryLookup(variable, out object value))
            {
                if (IsTruthy(value))
                {
                    if (value is IList list)
                    {
                        RenderList(context, builder, list);
                    }
                    else
                    {
                        RenderObject(context, builder, value);
                    }
                }
            }
        }

        void RenderList(TemplateContext context, StringBuilder builder, IList list)
        {
            for (int i = 0; i < list.Count; i++) { RenderObject(context, builder, list[i]); }
        }

        void RenderObject(TemplateContext context, StringBuilder builder, object value)
        {
            var newContext = new TemplateContext(value, context);
            for (int i = 0; i < this.sections.Count; i++)
            {
                this.sections[i].Render(newContext, builder);
            }
        }

        static bool IsTruthy(object value)
        {
            if (value == null) { return false; }
            if (value is string str) { return !String.IsNullOrEmpty(str); }
            if (value is bool b) { return b; }
            if (value is ICollection list) { return list.Count > 0; }            
            return true;
        }
    }

    class TemplateContext
    {
        public TemplateContext parent;
        public object context;

        public TemplateContext(object context, TemplateContext parent = null)
        {
            this.parent = parent;
            this.context = context;
        }

        public object Lookup(string key)
        {
            if (TryLookup(key, out object value)) { return value; }
            throw new KeyNotFoundException($"The key `{key}` was not found in the current context.");
        }

        public bool TryLookup(string key, out object value)
        {
            TemplateContext context = this;
            while (context != null)
            {
                if (context.TryGetValue(key, out value)) { return true; }
                context = context.parent;
            }

            value = null;
            return false;
        }

        bool TryGetValue(string key, out object value)
        {
            if (this.context == null)
            {
                value = null;
                return false;
            }

            if (this.context is IDictionary dictionary)
            {
                if (dictionary.Contains(key))
                {
                    value = dictionary[key];
                    return true;
                }
                else
                {
                    value = null;
                    return false;
                }
            }

            PropertyInfo property = this.context.GetType().GetRuntimeProperty(key);
            if (property != null)
            {
                value = property.GetValue(this.context);
                return true;
            }


            value = null;
            return false;
        }
    }

    class TemplateParser
    {
        int position;
        int literalStart;
        string text;
        List<TemplateSection> sections = new List<TemplateSection>();

        public List<TemplateSection> Parse(string text)
        {
            this.text = text;
            var stack = new Stack<ParserStackEntry>();

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
                        BlockTemplateSection section = new BlockTemplateSection(key, sections);
                        sections = top.sections;
                        sections.Add(section);
                    }
                    else
                    {
                        position += 2;
                        string key = ExtractKey();

                        sections.Add(new VariableReferenceTemplateSection(key));
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
        }

        void CaptureLiteralText()
        {
            if (position != literalStart)
            {
                string literal = text.Substring(literalStart, position - literalStart);
                sections.Add(new LiteralTemplateSection(literal));
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

        class ParserStackEntry
        {
            public string key;
            public List<TemplateSection> sections;
        }
    }

    class TextTemplate
    {
        readonly List<TemplateSection> sections = new List<TemplateSection>();

        public TextTemplate(string text)
        {
            this.sections = new TemplateParser().Parse(text);
        }

        public string Format(object context)
        {
            var newContext = new TemplateContext(context);
            var builder = new StringBuilder();

            for (int i = 0; i < this.sections.Count; i++)
            {
                this.sections[i].Render(newContext, builder);
            }

            return builder.ToString();
        }
    }
}
