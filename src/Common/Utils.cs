using System;
using System.Globalization;

namespace VoiceOfSanDiego.Alexa.Common {
    public static class Utils {

        //--- Class Methods ---
        public static DateTime? ParseDate(string date) {
            var today = DateTime.UtcNow.Date;
            if(date == null) {
                return today;
            }

            // only parse the date and time information (skip the day of week and offset)
            var commaChar = date.IndexOf(',');
            if(commaChar >= 0) {
                date = date.Substring(commaChar + 1);
            }
            var plusChar = date.IndexOf('+');
            if(plusChar >= 0) {
                date = date.Substring(0, plusChar);
            }
            if(!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime result)) {
                return today;
            }
            return result;
        }
    }
}