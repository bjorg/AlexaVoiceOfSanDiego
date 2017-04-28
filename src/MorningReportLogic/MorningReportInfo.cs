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

using System;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace VoiceOfSanDiego.Alexa.MorningReport {
    public class MorningReportInfo {

        //--- Constants ---
        public const string ROW_KEY = "morningreport";

        //--- Class Methods ---
        public static MorningReportInfo FromJson(string json) {
            return JsonConvert.DeserializeObject<MorningReportInfo>(json);
        }

        //--- Fields ---
        private XDocument _document;

        //--- Properties ---
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string Author { get; set; }
        public string Xml { get; set; }

        [JsonIgnore]
        public XDocument Document {
            get {
                if((_document == null) && (Xml != null)) {
                    _document = XDocument.Parse(Xml);
                }
                return _document;
            }
            set {
                if(value == null) {
                    Xml = null;
                } else {
                    Xml = value.ToString(SaveOptions.DisableFormatting);
                }
                _document = value;
            }
        }

        //--- Methods ---
        public string ToJson() {
            return JsonConvert.SerializeObject(this);
        }
    }
}