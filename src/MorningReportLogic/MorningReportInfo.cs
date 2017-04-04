
using System;
using System.Xml.Linq;

namespace VoiceOfSanDiego.Alexa.MorningReport {
    public class MorningReportInfo {

        //--- Properties ---
        public string Title { get; set; }
        public DateTime? Date { get; set; }
        public string Author { get; set; }
        public XDocument Document { get; set; }
    }
}