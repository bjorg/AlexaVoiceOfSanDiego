
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