using System;
using VideoDL_m3u8.Utils;

namespace VideoDL_m3u8.Events
{
    public class RecProgressEventArgs
    {
        public TimeSpan RecTime { get; set; }
        public int Finish { get; set; }
        public long DownloadBytes { get; set; }
        public int MaxRetry { get; set; }
        public int Retry { get; set; }
        public long Speed { get; set; }
        public int Lost { get; set; }
        public string Format 
        { 
            get
            {
                var recTime =
                    RecTime.Hours.ToString("00") + ":" +
                    RecTime.Minutes.ToString("00") + ":" +
                    RecTime.Seconds.ToString("00");
                var downloadSize = Filter.FormatFileSize(DownloadBytes);
                var speed = Filter.FormatFileSize(Speed);
                var print = $@"Progress: {Finish} (REC {recTime}) -- {downloadSize} ({speed}/s) -- Retry ({Retry}/{MaxRetry}) -- Lost {Lost}";
                return print;
            }
        }
    }
}
