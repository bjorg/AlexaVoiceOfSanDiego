using System;
using Newtonsoft.Json;

namespace VoiceOfSanDiego.Alexa.Podcasts {
    public class PodcastInfo {

        //--- Constants ---
        public const string ROW_KEY = "podcasts";

        //--- Class Methods ---
        public static PodcastInfo[] FromJson(string json) {
            return JsonConvert.DeserializeObject<PodcastInfo[]>(json);
        }

        public static string ToJson(PodcastInfo[] podcasts) {
            return JsonConvert.SerializeObject(podcasts);
        }

        //--- Properties ---
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string Url { get; set; }
        public string Token { get; set; }
    }
}