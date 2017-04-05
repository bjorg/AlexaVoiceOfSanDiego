using System;
using Newtonsoft.Json;

namespace VoiceOfSanDiego.Alexa.Podcasts {
    public class PodcastInfo {

        //--- Constants ---
        public const string ROW_KEY = "podcasts";

        //--- Class Methods ---
        public static PodcastInfo FromJson(string json) {
            return JsonConvert.DeserializeObject<PodcastInfo>(json);
        }

        //--- Properties ---
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string Url { get; set; }
        public string Token { get; set; }

        //--- Methods ---
        public string ToJson() {
            return JsonConvert.SerializeObject(this);
        }
    }
}