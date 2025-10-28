using Shared.Json;

namespace Shared.Http
{
    public class HttpHelpers
    {
        public static async ValueTask<T?> GetJsonFromHttpAsync<T>(string url, Dictionary<string, string> headers, IHttpClientFactory httpClientFactory, TimeSpan timeout)
        {
            var httpRequestMessage = new HttpRequestMessage(
                HttpMethod.Get,
                url);

            foreach (var header in headers)
            {
                httpRequestMessage.Headers.Add(header.Key, header.Value);
            }

            var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = timeout;
            var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

            var stream = await httpResponseMessage.Content.ReadAsStreamAsync();

            return await JsonHelpers.DeserializeAsync<T>(stream);
        }

        public static async Task<string> GetHttpAsync(string url, Dictionary<string, string> headers, IHttpClientFactory httpClientFactory, TimeSpan timeout)
        {
            var httpRequestMessage = new HttpRequestMessage(
                HttpMethod.Get,
                url);

            foreach (var header in headers)
            {
                httpRequestMessage.Headers.Add(header.Key, header.Value);
            }

            var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = timeout;
            var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

            var stream = await httpResponseMessage.Content.ReadAsStreamAsync();

            StreamReader reader = new(stream);
            return await reader.ReadToEndAsync();
        }

        public static async ValueTask<T?> PostAsync<T>(string url, Dictionary<string, string> headers, HttpContent content, IHttpClientFactory httpClientFactory, TimeSpan timeout)
        {
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            foreach (var header in headers)
            {
                httpRequestMessage.Headers.Add(header.Key, header.Value);
            }

            var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = timeout;

            var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

            var stream = await httpResponseMessage.Content.ReadAsStreamAsync();

            return await JsonHelpers.DeserializeAsync<T>(stream);
        }
    }
}
