using System;
using System.Globalization;

namespace YouTubeStuff {
    public class Video {
        public string Title { get; set; }
        public string Link { get; set; }
        public string Thumbnail { get; set; }
        public string Site { get; set; }
        public string Playlist { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }

        public double Duration { get; set; }
        public string DurationString { 
            get {
                if (Duration > 0)
                    return $"{(int)TimeSpan.FromSeconds(Duration).TotalMinutes:00}:{TimeSpan.FromSeconds(Duration).Seconds:00}";
                return "xx:xx";
            } 
        }

        public static double ParseSeconds(string value) {
            if (value.Contains(':'))
                return TimeSpan.ParseExact(value, [@"h\:m\:s\.FFFF", @"m\:s\.FFFF", @"h\:m\:s", @"m\:s"], CultureInfo.InvariantCulture).TotalSeconds;
            else return double.Parse(value);
        }
        public static bool TryParseSeconds(string value, out double seconds) {
            try {
                seconds = ParseSeconds(value); 
                return true;
            }
            catch {
                seconds = double.NaN;
                return false; 
            }
        }
    }
}
