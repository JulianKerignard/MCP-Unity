using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace McpUnity.Chat
{
    /// <summary>
    /// Lightweight markdown parser for IMGUI rendering.
    /// Splits text into typed segments (prose, code block, header, blockquote, horizontal rule) so each
    /// can be rendered with its own GUIStyle in the chat panel.
    /// </summary>
    public static class McpMarkdownRenderer
    {
        // ====================================================================
        // Segment Types
        // ====================================================================

        public enum SegmentType
        {
            Prose,          // Normal text (with inline rich text transforms applied)
            CodeBlock,      // Fenced ``` block (raw text, monospace rendering)
            Header,         // # / ## / ### line (bold, larger font)
            Blockquote,     // > quoted text (indented, left-border styling)
            HorizontalRule, // --- / *** / ___ divider line
            Table           // | col | col | table with separator row
        }

        public class MarkdownSegment
        {
            public SegmentType Type;
            public string Text;         // Rich text for prose, raw for code
            public int HeaderLevel;     // 1-3 for headers, 0 otherwise
            public string Language;     // Language hint from ``` fence (e.g. "csharp")
            public List<string> Links;  // UX-05: Extracted URLs from [text](url) patterns
            public string[][] TableRows; // [0]=headers, [1..]=data rows; null unless Type==Table
        }

        // ====================================================================
        // Regex patterns (compiled once)
        // ====================================================================

        // SEC-#418: 100ms timeout on every regex to prevent ReDoS via crafted LLM output.
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

        private static readonly Regex BoldItalicPattern = new Regex(@"\*\*\*(.+?)\*\*\*", RegexOptions.Compiled, RegexTimeout);
        private static readonly Regex BoldPattern = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled, RegexTimeout);
        private static readonly Regex ItalicPattern = new Regex(@"(?<!\w)\*(.+?)\*(?!\w)", RegexOptions.Compiled, RegexTimeout);
        private static readonly Regex StrikethroughPattern = new Regex(@"~~(.+?)~~", RegexOptions.Compiled, RegexTimeout);
        private static readonly Regex InlineCodePattern = new Regex(@"`([^`]+)`", RegexOptions.Compiled, RegexTimeout);
        private static readonly Regex LinkPattern = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled, RegexTimeout);
        private static readonly Regex HorizontalRulePattern = new Regex(@"^(\s*[-*_]){3,}\s*$", RegexOptions.Compiled, RegexTimeout);

        // ====================================================================
        // Public API
        // ====================================================================

        /// <summary>
        /// Parse raw markdown text into a list of typed segments.
        /// Cached on ChatDisplayEntry.parsedSegments to avoid re-parsing every OnGUI frame.
        /// </summary>
        public static List<MarkdownSegment> Parse(string rawText)
        {
            if (string.IsNullOrEmpty(rawText))
                return new List<MarkdownSegment> { new MarkdownSegment { Type = SegmentType.Prose, Text = "" } };

            var segments = new List<MarkdownSegment>();
            var lines = rawText.Split('\n');

            bool inCodeBlock = false;
            string codeLanguage = "";
            var codeBuffer = new StringBuilder();
            var proseBuffer = new StringBuilder();

            var blockquoteBuffer = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();

                // Check for table (| cell | ... row followed by | --- | separator)
                if (!inCodeBlock && trimmed.StartsWith("|") && i + 1 < lines.Length && IsTableSeparator(lines[i + 1].TrimStart()))
                {
                    FlushProse(proseBuffer, segments);
                    FlushBlockquote(blockquoteBuffer, segments);

                    // Parse header row
                    var headers = ParseTableRow(trimmed);
                    i++; // skip separator row

                    // Collect data rows (all consecutive lines starting with |)
                    var dataRows = new List<string[]>();
                    while (i + 1 < lines.Length && lines[i + 1].TrimStart().StartsWith("|"))
                    {
                        i++;
                        dataRows.Add(ParseTableRow(lines[i].TrimStart()));
                    }

                    // Build TableRows: [0]=headers, [1..n]=data rows
                    var tableRows = new string[1 + dataRows.Count][];
                    tableRows[0] = headers;
                    for (int r = 0; r < dataRows.Count; r++)
                        tableRows[r + 1] = dataRows[r];

                    segments.Add(new MarkdownSegment { Type = SegmentType.Table, TableRows = tableRows });
                    continue;
                }

                // Check for code fence
                if (trimmed.StartsWith("```"))
                {
                    if (!inCodeBlock)
                    {
                        // Flush prose and blockquote
                        FlushProse(proseBuffer, segments);
                        FlushBlockquote(blockquoteBuffer, segments);

                        // Start code block
                        inCodeBlock = true;
                        codeLanguage = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : "";
                        codeBuffer.Clear();
                    }
                    else
                    {
                        // End code block
                        inCodeBlock = false;
                        segments.Add(new MarkdownSegment
                        {
                            Type = SegmentType.CodeBlock,
                            Text = codeBuffer.ToString().TrimEnd('\n'),
                            Language = codeLanguage
                        });
                        codeBuffer.Clear();
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    if (codeBuffer.Length > 0) codeBuffer.Append('\n');
                    codeBuffer.Append(line);
                    continue;
                }

                // Check for horizontal rule (---, ***, ___)
                if (HorizontalRulePattern.IsMatch(trimmed))
                {
                    FlushProse(proseBuffer, segments);
                    FlushBlockquote(blockquoteBuffer, segments);
                    segments.Add(new MarkdownSegment { Type = SegmentType.HorizontalRule });
                    continue;
                }

                // Check for blockquote
                if (trimmed.StartsWith("> ") || trimmed == ">")
                {
                    // Flush prose before blockquote
                    FlushProse(proseBuffer, segments);

                    string quoteText = trimmed.Length > 2 ? trimmed.Substring(2) : "";
                    if (blockquoteBuffer.Length > 0) blockquoteBuffer.Append('\n');
                    blockquoteBuffer.Append(quoteText);
                    continue;
                }

                // If we were in a blockquote, flush it before processing other block types
                if (blockquoteBuffer.Length > 0)
                    FlushBlockquote(blockquoteBuffer, segments);

                // Check for header
                if (trimmed.StartsWith("#"))
                {
                    // Flush prose before header
                    FlushProse(proseBuffer, segments);

                    int level = 0;
                    while (level < trimmed.Length && level < 3 && trimmed[level] == '#') level++;

                    if (level > 0 && level < trimmed.Length && trimmed[level] == ' ')
                    {
                        string headerText = trimmed.Substring(level + 1);
                        segments.Add(new MarkdownSegment
                        {
                            Type = SegmentType.Header,
                            Text = headerText,
                            HeaderLevel = level
                        });
                        continue;
                    }
                }

                // Regular line — add to prose buffer
                if (proseBuffer.Length > 0) proseBuffer.Append('\n');
                proseBuffer.Append(line);
            }

            // Handle unclosed code block
            if (inCodeBlock && codeBuffer.Length > 0)
            {
                segments.Add(new MarkdownSegment
                {
                    Type = SegmentType.CodeBlock,
                    Text = codeBuffer.ToString().TrimEnd('\n'),
                    Language = codeLanguage
                });
            }
            else
            {
                FlushBlockquote(blockquoteBuffer, segments);
                FlushProse(proseBuffer, segments);
            }

            if (segments.Count == 0)
                segments.Add(new MarkdownSegment { Type = SegmentType.Prose, Text = "" });

            return segments;
        }

        // ====================================================================
        // Internal Helpers
        // ====================================================================

        // ====================================================================
        // Table Parsing Helpers
        // ====================================================================

        /// <summary>Returns true if the line is a markdown table separator (| --- | :---: | ---: |).</summary>
        private static bool IsTableSeparator(string line)
        {
            if (!line.StartsWith("|")) return false;
            bool hasDash = false;
            foreach (char c in line)
            {
                if (c == '-') { hasDash = true; continue; }
                if (c == '|' || c == ':' || c == ' ') continue;
                return false; // unexpected character → not a separator
            }
            return hasDash;
        }

        /// <summary>Split a table row string into trimmed, inline-converted cell strings.</summary>
        private static string[] ParseTableRow(string line)
        {
            // Split by | — first and last elements are empty (outside the pipes)
            var parts = line.Split('|');
            var cells = new List<string>(parts.Length);
            for (int i = 1; i < parts.Length - 1; i++)
            {
                string raw = parts[i].Trim();
                cells.Add(ConvertInlineMarkdown(raw));
            }
            return cells.ToArray();
        }

        private static void FlushProse(StringBuilder buffer, List<MarkdownSegment> segments)
        {
            if (buffer.Length == 0) return;

            string rawText = buffer.ToString();
            buffer.Clear();

            // UX-05: Extract link URLs before inline transforms mangle the pattern
            var links = ExtractLinks(rawText);

            // Apply inline markdown transforms → Unity rich text
            string text = ConvertInlineMarkdown(rawText);

            segments.Add(new MarkdownSegment
            {
                Type = SegmentType.Prose,
                Text = text,
                Links = links
            });
        }

        private static void FlushBlockquote(StringBuilder buffer, List<MarkdownSegment> segments)
        {
            if (buffer.Length == 0) return;

            string text = buffer.ToString();
            buffer.Clear();

            // Apply inline markdown transforms inside blockquotes too
            text = ConvertInlineMarkdown(text);

            segments.Add(new MarkdownSegment
            {
                Type = SegmentType.Blockquote,
                Text = text
            });
        }

        /// <summary>
        /// Convert inline markdown to Unity rich text tags.
        /// Handles bold, italic, strikethrough, inline code, links, and list bullets (ordered + unordered).
        /// </summary>
        public static string ConvertInlineMarkdown(string text)
        {
            // Bold+italic: ***text***
            text = BoldItalicPattern.Replace(text, "<b><i>$1</i></b>");

            // Bold: **text**
            text = BoldPattern.Replace(text, "<b>$1</b>");

            // Italic: *text* (not inside words)
            text = ItalicPattern.Replace(text, "<i>$1</i>");

            // Strikethrough: ~~text~~ → Unicode combining stroke (U+0336) on each character + grey tint
            // Unity IMGUI has no native strikethrough tag; combining stroke is the best portable approximation.
            text = StrikethroughPattern.Replace(text, m =>
            {
                var inner = m.Groups[1].Value;
                var sb = new System.Text.StringBuilder(inner.Length * 2);
                foreach (char c in inner)
                {
                    sb.Append(c);
                    sb.Append('\u0336'); // Combining long stroke overlay (U+0336)
                }
                return "<color=#888888>" + sb + "</color>";
            });

            // Inline code: `code` → colored
            text = InlineCodePattern.Replace(text, "<color=#e06c75>$1</color>");

            // Links: [text](url) → "text (url)" with blue coloring
            text = LinkPattern.Replace(text, "<color=#61afef>$1</color> <color=#666666>($2)</color>");

            // List bullets: "- item" / "* item" / "1. item" at start of line
            text = ConvertListBullets(text);

            return text;
        }

        private static readonly Regex NumberedListPattern = new Regex(@"^(\d+)\.\s+(.+)$", RegexOptions.Compiled, RegexTimeout);

        private static string ConvertListBullets(string text)
        {
            var lines = text.Split('\n');
            bool changed = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                int indent = lines[i].Length - trimmed.Length;

                // Unordered lists: "- item" or "* item"
                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    string bullet = indent > 0 ? "\u25E6 " : "\u2022 ";
                    lines[i] = new string(' ', indent) + "  " + bullet + trimmed.Substring(2);
                    changed = true;
                    continue;
                }

                // Ordered lists: "1. item", "2. item", etc.
                var match = NumberedListPattern.Match(trimmed);
                if (match.Success)
                {
                    string number = match.Groups[1].Value;
                    string content = match.Groups[2].Value;
                    string prefix = indent > 0 ? "    " : "  ";
                    lines[i] = new string(' ', indent) + prefix + number + ". " + content;
                    changed = true;
                }
            }

            return changed ? string.Join("\n", lines) : text;
        }

        // ====================================================================
        // UX-05: Link Extraction
        // ====================================================================

        /// <summary>Extract all URLs from [text](url) patterns in raw text.</summary>
        public static List<string> ExtractLinks(string rawText)
        {
            if (string.IsNullOrEmpty(rawText)) return null;
            var matches = LinkPattern.Matches(rawText);
            if (matches.Count == 0) return null;
            var links = new List<string>(matches.Count);
            foreach (Match m in matches)
                links.Add(m.Groups[2].Value);
            return links;
        }

        // ====================================================================
        // UX-04: Basic Syntax Highlighting for Code Blocks
        // ====================================================================

        // Common keywords by language family
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
        {
            "abstract","as","base","bool","break","byte","case","catch","char","checked","class",
            "const","continue","decimal","default","delegate","do","double","else","enum","event",
            "explicit","extern","false","finally","fixed","float","for","foreach","goto","if",
            "implicit","in","int","interface","internal","is","lock","long","namespace","new","null",
            "object","operator","out","override","params","private","protected","public","readonly",
            "ref","return","sbyte","sealed","short","sizeof","stackalloc","static","string","struct",
            "switch","this","throw","true","try","typeof","uint","ulong","unchecked","unsafe",
            "ushort","using","var","virtual","void","volatile","while","async","await","yield",
            "partial","where","get","set","value","add","remove","when","record","init","required"
        };

        private static readonly HashSet<string> JsKeywords = new HashSet<string>
        {
            "abstract","arguments","await","boolean","break","byte","case","catch","char","class",
            "const","continue","debugger","default","delete","do","double","else","enum","eval",
            "export","extends","false","final","finally","float","for","function","goto","if",
            "implements","import","in","instanceof","int","interface","let","long","native","new",
            "null","of","package","private","protected","public","return","short","static","super",
            "switch","synchronized","this","throw","throws","transient","true","try","typeof",
            "undefined","var","void","volatile","while","with","yield","async","from","type"
        };

        private static readonly Regex HighlightWordBoundary = new Regex(@"\b([a-zA-Z_]\w*)\b", RegexOptions.Compiled, RegexTimeout);
        private static readonly Regex HighlightString = new Regex(@"(""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'|`(?:[^`\\]|\\.)*`)", RegexOptions.Compiled, RegexTimeout);
        private static readonly Regex HighlightNumber = new Regex(@"\b(\d+\.?\d*[fFdDmMlL]?)\b", RegexOptions.Compiled, RegexTimeout);
        private static readonly Regex HighlightComment = new Regex(@"(\/\/[^\n]*|\/\*[\s\S]*?\*\/)", RegexOptions.Compiled, RegexTimeout);

        // Colors (One Dark inspired, harmonized with existing palette)
        private const string KeywordColor = "#c678dd";  // purple
        private const string StringColor = "#98c379";    // green
        private const string CommentColor = "#5c6370";   // grey
        private const string NumberColor = "#d19a66";    // orange
        private const string TypeColor = "#e5c07b";      // yellow

        /// <summary>
        /// Apply basic syntax highlighting to code block text using Unity rich text tags.
        /// Supports C#, TypeScript, JavaScript. Returns original text for unknown languages.
        /// </summary>
        public static string HighlightCode(string code, string language)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(language))
                return code;

            string lang = language.ToLowerInvariant();
            HashSet<string> keywords;

            switch (lang)
            {
                case "csharp":
                case "cs":
                case "c#":
                    keywords = CSharpKeywords;
                    break;
                case "typescript":
                case "ts":
                case "javascript":
                case "js":
                case "jsx":
                case "tsx":
                    keywords = JsKeywords;
                    break;
                default:
                    return code; // No highlighting for unknown languages
            }

            // Apply highlighting in order: comments first (highest priority), then strings, then keywords/numbers
            // We use a placeholder replacement strategy to avoid double-highlighting
            var sb = new StringBuilder(code.Length * 2);
            var regions = new List<(int start, int end, string replacement)>();

            // Phase 1: Collect comment regions
            foreach (Match m in HighlightComment.Matches(code))
                regions.Add((m.Index, m.Index + m.Length, $"<color={CommentColor}>{EscapeRichText(m.Value)}</color>"));

            // Phase 2: Collect string regions (skip if inside comment)
            foreach (Match m in HighlightString.Matches(code))
            {
                if (!IsInsideRegion(m.Index, regions))
                    regions.Add((m.Index, m.Index + m.Length, $"<color={StringColor}>{EscapeRichText(m.Value)}</color>"));
            }

            // Sort regions by start position
            regions.Sort((a, b) => a.start.CompareTo(b.start));

            // Phase 3: Build output — process character by character, highlighting keywords and numbers in gaps
            int pos = 0;
            foreach (var region in regions)
            {
                if (region.start > pos)
                {
                    // Process the gap between regions for keywords and numbers
                    string gap = code.Substring(pos, region.start - pos);
                    AppendHighlightedGap(sb, gap, keywords);
                }
                sb.Append(region.replacement);
                pos = region.end;
            }

            // Process remaining text after last region
            if (pos < code.Length)
            {
                string remaining = code.Substring(pos);
                AppendHighlightedGap(sb, remaining, keywords);
            }

            return sb.ToString();
        }

        private static bool IsInsideRegion(int index, List<(int start, int end, string replacement)> regions)
        {
            foreach (var r in regions)
            {
                if (index >= r.start && index < r.end) return true;
            }
            return false;
        }

        private static void AppendHighlightedGap(StringBuilder sb, string text, HashSet<string> keywords)
        {
            // Highlight keywords and numbers in non-string, non-comment text
            int lastEnd = 0;
            foreach (Match m in HighlightWordBoundary.Matches(text))
            {
                // Append text before this match
                if (m.Index > lastEnd)
                    sb.Append(text, lastEnd, m.Index - lastEnd);

                string word = m.Groups[1].Value;
                if (keywords.Contains(word))
                    sb.Append($"<color={KeywordColor}>{word}</color>");
                else if (word.Length > 0 && char.IsUpper(word[0]) && word.Length > 1) // PascalCase = likely type
                    sb.Append($"<color={TypeColor}>{word}</color>");
                else
                    sb.Append(word);

                lastEnd = m.Index + m.Length;
            }

            // Append remaining text
            if (lastEnd < text.Length)
            {
                string remaining = text.Substring(lastEnd);
                // Highlight numbers in remaining non-word parts
                remaining = HighlightNumber.Replace(remaining, $"<color={NumberColor}>$1</color>");
                sb.Append(remaining);
            }
        }

        /// <summary>Escape &lt; in code content to prevent Unity rich text tag interpretation.</summary>
        private static string EscapeRichText(string text)
        {
            // Insert zero-width space (U+200B) after < to break IMGUI tag detection.
            // e.g. List<T> becomes List<​T> — ZWS is invisible but prevents <tag> parsing.
            // We cannot use &lt; because Unity rich text does not support HTML entities.
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0) return text;
            return text.Replace("<", "<\u200B");
        }
    }
}
