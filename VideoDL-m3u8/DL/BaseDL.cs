using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace VideoDL_m3u8.DL
{
    public class BaseDL
    {
        protected static readonly string userAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/97.0.4692.99 Safari/537.36";

        protected void SetRequestHeader(HttpRequestMessage request, string header)
        {
            var attrs = header.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var attr in attrs)
            {
                var split = attr.Split(new char[] { ':' }, 2);
                var key = split[0].Trim();
                var val = split[1].Trim();
                request.Headers.Add(key, val);
            }
            if (!request.Headers.Contains("User-Agent"))
                request.Headers.Add("User-Agent", userAgent);
            request.Headers.ConnectionClose = true;
        }

        protected async Task<(string data, string url)> GetStringAsync(HttpClient httpClient,
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

        protected async Task<byte[]> GetBytesAsync(HttpClient httpClient,
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

        protected async Task LoadStreamAsync(HttpClient httpClient, 
            string url, string header, Func<Stream, Task> callback,
            long? rangeFrom = null, long? rangeTo = null, 
            CancellationToken token = default)
        {
            async Task load(string url)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    if (rangeFrom != null || rangeTo != null)
                        request.Headers.Range = new RangeHeaderValue(rangeFrom, rangeTo);
                    SetRequestHeader(request, header);
                    token.ThrowIfCancellationRequested();
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
