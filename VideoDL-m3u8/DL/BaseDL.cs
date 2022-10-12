using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace VideoDL_m3u8.DL
{
    public class BaseDL
    {
        public static readonly string userAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/97.0.4692.99 Safari/537.36";

        public void SetRequestHeader(HttpRequestMessage request, string header)
        {
            var splits = header.Split('|', StringSplitOptions.RemoveEmptyEntries);
            foreach (var attr in splits)
            {
                var split = attr.Split(':', 2);
                var key = split[0].Trim();
                var val = split[1].Trim();
                request.Headers.Add(key, val);
            }
            if (!request.Headers.Contains("User-Agent"))
                request.Headers.Add("User-Agent", userAgent);
            request.Headers.ConnectionClose = true;
        }

        public async Task<(string data, string url)> GetStringAsync(HttpClient httpClient,
            string url, string header, CancellationToken token = default)
        {
            var requestUrl = "";
            async Task<string> get(string url)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    SetRequestHeader(request, header);
                    using (var response = await httpClient.SendAsync(request, token))
                    {
                        if (response.Headers.Location != null)
                            return await get(response.Headers.Location.AbsoluteUri);
                        response.EnsureSuccessStatusCode();
                        requestUrl = response.RequestMessage?.RequestUri?.ToString() ?? "";
                        return await response.Content.ReadAsStringAsync(token);
                    }
                }
            }
            var data = await get(url);
            return (data, requestUrl != "" ? requestUrl : url);
        }

        public async Task<byte[]> GetBytesAsync(HttpClient httpClient,
            string url, string header, CancellationToken token = default)
        {
            async Task<byte[]> get(string url)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    SetRequestHeader(request, header);
                    using (var response = await httpClient.SendAsync(request, token))
                    {
                        if (response.Headers.Location != null)
                            return await get(response.Headers.Location.AbsoluteUri);
                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsByteArrayAsync(token);
                    }
                }
            }
            return await get(url);
        }

        public async Task LoadStreamAsync(HttpClient httpClient,
            string url, string header, Func<Stream, Task> callback, 
            CancellationToken token = default)
        {
            async Task load(string url)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    SetRequestHeader(request, header);
                    using (var response = await httpClient.SendAsync(request,
                        HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        if (response.Headers.Location != null)
                        {
                            await load(response.Headers.Location.AbsoluteUri);
                            return;
                        }
                        response.EnsureSuccessStatusCode();
                        using (var stream = await response.Content.ReadAsStreamAsync(token))
                        {
                            await callback(stream);
                        }
                    }
                }
            }
            await load(url);
        }
    }
}
