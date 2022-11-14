using VideoDL_m3u8.Utils;

namespace VideoDL_m3u8.Events
{
    public class ProgressEventArgs
    {
        public int Total { get; set; }
        public int Finish { get; set; }
        public double Percentage { get; set; }
        public long TotalBytes { get; set; }
        public long DownloadBytes { get; set; }
        public int MaxRetry { get; set; }
        public int Retry { get; set; }
        public long Speed { get; set; }
        public int Eta { get; set; }
        public string Format 
        { 
            get
            {
                var percentage = (Percentage * 100).ToString("0.00");
                var totalSize = Filter.FormatFileSize(TotalBytes);
                var downloadSize = Filter.FormatFileSize(DownloadBytes);
                var speed = Filter.FormatFileSize(Speed);
                var eta = Filter.FormatTime(Eta);
                var print = $@"Progress: {Finish}/{Total} ({percentage} %) -- {downloadSize}/{totalSize} ({speed}/s @ {eta}) -- Retry ({Retry}/{MaxRetry})";
                return print;
            }
        }
    }
}
