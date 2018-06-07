using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Newtonsoft.Json;
using Xunit;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedSidechains.IntegrationTests.Common;
using Stratis.Sidechains.Features.BlockchainGeneration;
using Stratis.Sidechains.Features.BlockchainGeneration.Network;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Tests
{
    [Collection("SidechainIdentifierTests")]
    public class Sidechain_Api_Shall : IDisposable
    {
        public class JsonContent : StringContent
        {
            public JsonContent(object obj) :
                base(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json")
            {

            }
        }

        private readonly HttpClient client;
        public Sidechain_Api_Shall()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }


        [Fact]
        public async Task be_able_to_give_CoinDetails()
        {
            using (var nodeBuilder = NodeBuilder.Create(this))
            {
                var node = nodeBuilder.CreatePowPosMiningNode(SidechainNetwork.SidechainRegTest, start: true);

                var coinDetails = await this.GetCoinDetailsAsync(node.ApiPort());
                coinDetails.Name.Should().Be("enigmaCoin");
                coinDetails.Symbol.Should().Be("EGA2");
                coinDetails.Type.Should().Be(12345);
            }
        }

        private async Task<CoinDetails> GetCoinDetailsAsync(int apiPort)
        {

            var uri = new Uri($"http://localhost:{apiPort}/api/Sidechains/get-coindetails");
            var httpResponseMessage = await client.GetAsync(uri);
            httpResponseMessage.IsSuccessStatusCode.Should().BeTrue(httpResponseMessage.ReasonPhrase);
            var coinDetails = JsonConvert.DeserializeObject<CoinDetails>(await httpResponseMessage.Content.ReadAsStringAsync());
            return coinDetails;
        }

        public void Dispose()
        {
            if (client != null) client.Dispose();
        }
    }
}
