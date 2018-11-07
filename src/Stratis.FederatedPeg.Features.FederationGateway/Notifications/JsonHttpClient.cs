using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Net.Http;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Notifications
{
    public class JsonHttpClient : IHttpClient, IDisposable
    {
        private HttpClient httpClient;

        public JsonHttpClient()
        {
            this.httpClient = new HttpClient();
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.httpClient?.Dispose();
        }

        /// <inheritdoc />
        public async Task<HttpResponseMessage> PostAsync(Uri uri, HttpContent content)
        {
            return await this.httpClient.PostAsync(uri, content);
        }
    }
}
