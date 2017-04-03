
using System;

namespace VoiceOfSanDiego.Alexa.Common {
    public class MorningReportInfo {

        //--- Properties ---
        public string Title { get; set; }
        public DateTime? Date { get; set; }
        public string Author { get; set; }
        public string Ssml { get; set; }
        public string Text { get; set; }
    }
}