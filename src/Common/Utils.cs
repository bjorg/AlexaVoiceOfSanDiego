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