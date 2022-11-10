using System;

namespace VideoDL_m3u8.Exceptions
{
    public class OverRetryException : Exception
    {
        public OverRetryException(Exception innerException)
            : base("Over retry times.", innerException)
        {
        }
    }
}
