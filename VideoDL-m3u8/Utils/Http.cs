using System;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;

namespace VideoDL_m3u8.Utils
{
    internal static class Http
    {
        private static readonly Lazy<HttpClient> HttpClientLazy = new(() =>
        {
            var handler = new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false
            };

            if (handler.SupportsAutomaticDecompression)
                handler.AutomaticDecompression = 
                DecompressionMethods.GZip | 
                DecompressionMethods.Deflate |
                DecompressionMethods.Brotli;

            return new HttpClient(handler, true);
        });

        public static HttpClient Client => HttpClientLazy.Value;
    }
}
