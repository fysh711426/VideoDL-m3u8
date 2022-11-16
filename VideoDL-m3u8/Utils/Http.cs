﻿using MihaZupan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

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
                DecompressionMethods.Deflate;

            return new HttpClient(handler, true);
        });

        public static HttpClient Client => HttpClientLazy.Value;

        private static readonly Dictionary<string, Lazy<HttpClient>>
            HttpClientLazyDict = new();

        public static HttpClient GetClient(string proxy)
        {
            if (HttpClientLazyDict.TryGetValue(proxy, out var client))
                return client.Value;

            HttpClientLazyDict.Add(proxy, new (() =>
            {
                var webProxy = null as IWebProxy;

                try
                {
                    if (proxy.StartsWith("socks5"))
                    {
                        var hostnamePort = proxy.Split(new char[] { '/' },
                            StringSplitOptions.RemoveEmptyEntries).Last();
                        var split = hostnamePort.Split(new char[] { ':' }, 2, 
                            StringSplitOptions.RemoveEmptyEntries);
                        var hostname = split[0];
                        var ip = int.Parse(split[1]);
                        webProxy = new HttpToSocks5Proxy(hostname, ip);
                    }
                    else
                    {
                        webProxy = new WebProxy(proxy)
                        {
                            BypassProxyOnLocal = false
                        };
                    }
                }
                catch(Exception ex)
                {
                    throw new Exception("Proxy url error.", ex);
                }

                var handler = new HttpClientHandler
                {
                    UseCookies = false,
                    AllowAutoRedirect = false,
                    Proxy = webProxy
                };

                if (handler.SupportsAutomaticDecompression)
                    handler.AutomaticDecompression =
                    DecompressionMethods.GZip |
                    DecompressionMethods.Deflate;

                return new HttpClient(handler, true);
            }));
            return HttpClientLazyDict[proxy].Value;
        }
    }
}
