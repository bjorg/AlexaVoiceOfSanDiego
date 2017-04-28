/*
 * MIT License
 *
 * Copyright (c) 2017 Voice of San Diego
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace VoiceOfSanDiego.Alexa.MorningReport {
    public static class MorningReportInfoEx {

        //--- Class Fields ---
        private static readonly Regex _htmlEntitiesRegEx = new Regex("&(?<value>#(x[a-f0-9]+|[0-9]+)|[a-z0-9]+);", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        //--- Class Methods ---
        public static string ConvertContentsToSsml(
            this MorningReportInfo morningReport,
            string _preHeadingBreak = "750ms",
            string _postHeadingBreak = "250ms",
            string _bulletBreak = "750ms",
            int maxLength = 7500
        ) {

            // extract contents into sections and sentences
            var buffer = new StringBuilder();
            var sections = new List<(string Title, List<string> Sentences)>();
            sections.Add((Title: morningReport.Title, Sentences: new List<string>()));
            Visit(morningReport.Document.Root);
            sections.Last().Sentences.Add($"This was the morning report by {morningReport.Author}, published on {morningReport.Date:dddd, MMMM d, yyyy}.");

            // convert HTML to SSML format
            var ssml = new XDocument(new XElement("speak"));
            var root = ssml.Root;
            var currentLength = 0;
            var firstHeading = true;
            foreach(var section in sections) {
                var accumulator = new XElement("acc");
                if(!firstHeading) {
                    accumulator.Add(new XElement("break", new XAttribute("time", _preHeadingBreak)));
                }
                firstHeading = false;
                accumulator.Add(new XText(section.Title));
                accumulator.Add(new XElement("break", new XAttribute("time", _postHeadingBreak)));
                foreach(var sentence in section.Sentences) {
                    if(sentence.StartsWith("\u2022")) {
                        accumulator.Add(new XElement("break", new XAttribute("time", _bulletBreak)));
                        accumulator.Add(new XText(sentence));
                    } else {
                        accumulator.Add(new XElement("p", new XText(sentence)));
                    }
                }
                var accumulatorLength = accumulator.ToString().Length;
                if(currentLength + accumulatorLength > maxLength) {
                    break;
                }
                foreach(var node in accumulator.Nodes()) {
                    root.Add(node);
                }
                currentLength += accumulatorLength;
            }
            root.Add(new XElement("p", new XText($"This was the morning report by {morningReport.Author}, published on {morningReport.Date:dddd, MMMM d, yyyy}.")));
            return ssml.ToString();

            //--- Local Functions ---
            void VisitNodes(XElement element) {
                foreach(var node in element.Nodes()) {
                    Visit(node);
                }
            }

            void Visit(XNode node) {
                switch(node) {
                case XElement xelement:
                    var name = xelement.Name.ToString();
                    switch(name) {
                    case "p":
                        VisitNodes(xelement);
                        if(buffer.Length > 0) {
                            sections.Last().Sentences.Add(buffer.ToString());
                            buffer.Clear();
                        }
                        break;
                    case "h1":
                    case "h2":
                    case "h3":
                    case "h4":
                    case "h5":
                    case "h6":
                        if(buffer.Length > 0) {
                            sections.Last().Sentences.Add(buffer.ToString());
                            buffer.Clear();
                        }
                        VisitNodes(xelement);
                        sections.Add((Title: buffer.ToString().Trim(), Sentences: new List<string>()));
                        buffer.Clear();
                        break;
                    default:
                        VisitNodes(xelement);
                        break;
                    }
                    break;
                case XText xtext:
                    var decodedText = DecodeHtmlEntities(xtext.Value).Trim();
                    if(decodedText.Length > 0) {
                        if(buffer.Length > 0) {
                            buffer.Append(' ');
                        }
                        buffer.Append(decodedText);
                    }
                    break;
                }
            }
        }

        public static string ConvertContentsToText(this MorningReportInfo morningReport) {

            // extract all inner text nodes
            var text = new StringBuilder();

            // add title when present
            if(morningReport.Title != null) {
                text.AppendLine($"=== {morningReport.Title} ===");
                if(morningReport.Author != null) {
                    text.AppendLine($"{morningReport.Date:dddd, MMMM d, yyyy} by {morningReport.Author}");
                } else {
                    text.AppendLine(morningReport.Date.ToString("dddd, MMMM d, yyyy"));
                }
                text.AppendLine();
            }

            // convert HTML to plain-text format
            Visit(morningReport.Document.Root);
            return text.ToString();

            //--- Local Functions ---
            void VisitNodes(XElement element) {
                foreach(var node in element.Nodes()) {
                    Visit(node);
                }
            }

            void Visit(XNode node) {
                switch(node) {
                case XElement xelement:
                    var name = xelement.Name.ToString();
                    switch(name) {
                    case "p":
                        VisitNodes(xelement);
                        text.AppendLine();
                        text.AppendLine();
                        break;
                    case "h1":
                    case "h2":
                    case "h3":
                    case "h4":
                    case "h5":
                    case "h6":
                        text.Append("-- ");
                        VisitNodes(xelement);
                        text.Append(" --");
                        text.AppendLine();
                        break;
                    default:
                        VisitNodes(xelement);
                        break;
                    }
                    break;
                case XText xtext:
                    text.Append(DecodeHtmlEntities(xtext.Value));
                    break;
                }
            }
        }

        public static string DecodeHtmlEntities(string text) {
            return _htmlEntitiesRegEx.Replace(text, m => {
                string v = m.Groups["value"].Value;
                if(v[0] == '#') {
                    if(char.ToLowerInvariant(v[1]) == 'x') {
                        string value = v.Substring(2);
                        return ((char)int.Parse(value, NumberStyles.HexNumber)).ToString();
                    } else {
                        string value = v.Substring(1);
                        return ((char)int.Parse(value)).ToString();
                    }
                } else {
                    switch(v) {
                    case "amp":
                        return "&";
                    case "apos":
                        return "'";
                    case "gt":
                        return ">";
                    case "lt":
                        return "<";
                    case "quot":
                        return "\"";
                    }
                    return v;
                }
            }, int.MaxValue);
        }
    }
}