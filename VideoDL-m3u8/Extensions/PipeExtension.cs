using System;

namespace VideoDL_m3u8.Extensions
{
    internal static class PipeExtension
    {
        public static TResult Pipe<TSource, TResult>(this TSource source, Func<TSource, TResult> selector)
        {
            return selector(source);
        }
    }
}
