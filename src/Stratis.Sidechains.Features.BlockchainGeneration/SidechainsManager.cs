using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Sidechains.Features.BlockchainGeneration.Network;

namespace Stratis.Sidechains.Features.BlockchainGeneration
{
    internal class SidechainsManager : ISidechainsManager
    {
        private JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Include,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };
        private CoinDetails coinDetails;
        private readonly string folder;
        private readonly NBitcoin.Network network;
        private readonly string sidechainName;

        public SidechainsManager(NodeSettings nodeSettings)
        {
            //TODO: probably add the infornations about the sidechains in the nodeSettings 
            var directoryInfo = new DirectoryInfo(nodeSettings.DataDir);
            folder = directoryInfo.Parent.Parent.FullName;
            network = nodeSettings.Network;
            sidechainName = directoryInfo.Parent.Name;
        }

        public async Task<Dictionary<string, SidechainInfo>> ListSidechains()
        {
            return await this.GetSidechains();
        }

        public async Task<CoinDetails> GetCoinDetails()
        {
            if (coinDetails != null) return coinDetails;
            coinDetails = BuildCoinDetailsFromNetwork();
            return coinDetails;

        }

        private CoinDetails BuildCoinDetailsFromNetwork()
        {
            var networkUpperName = network.Name.ToUpper();
            var shortNetworkName = networkUpperName.Substring(0, 3);
            string coinSymbol, coinName;
            if (networkUpperName.EndsWith("MAIN"))
            {
                coinSymbol = shortNetworkName;
                coinName = network.Name.Replace("Main", "Coin");
            }
            else if (networkUpperName.EndsWith("REGTEST"))
            {
                coinSymbol = "r" + shortNetworkName;
                coinName = network.Name.Replace("RegTest", "Coin");
            }
            else
            {
                coinSymbol = "t" + shortNetworkName;
                coinName = network.Name.Replace("Test", "Coin");
            }

            coinDetails = new CoinDetails(coinSymbol, coinName, network.Consensus.CoinType);
            return coinDetails;
        }

        public async Task NewSidechain(SidechainInfoRequest sidechainInfoRequest)
        {
            var dictionaryOut = await this.GetSidechains();
            if (dictionaryOut.Keys.Contains(sidechainInfoRequest.ChainName))
                throw new ArgumentException($"A sidechain with the name ${sidechainInfoRequest.ChainName} already exists.");

            var sidechainInfo = new SidechainInfo(sidechainInfoRequest.ChainName, 
                sidechainInfoRequest.CoinName, 
                sidechainInfoRequest.CoinType,
                sidechainInfoRequest.MainNet, 
                sidechainInfoRequest.TestNet, 
                sidechainInfoRequest.RegTest);
            dictionaryOut.Add(sidechainInfo.ChainName, sidechainInfo);
            await this.SaveSidechains(dictionaryOut);
        }

        private async Task SaveSidechains(Dictionary<string, SidechainInfo> dictionary)
        {
            string filename = Path.Combine(folder, "sidechains.json");

            string json = JsonConvert.SerializeObject(dictionary, Formatting.Indented, this.jsonSerializerSettings);
            using (var fileStream = File.OpenWrite(filename))
            {
                var bytesToWrite = Encoding.UTF8.GetBytes(json);
                await fileStream.WriteAsync(bytesToWrite, 0, bytesToWrite.Length);
            }
        }

        private async Task<Dictionary<string, SidechainInfo>> GetSidechains()
        {
            string filename = Path.Combine(folder, "sidechains.json");
            if (System.IO.File.Exists(filename) == false)
                return new Dictionary<string, SidechainInfo>();
            else
            {
                using (var fileStream = File.OpenText(filename))
                {
                    var content = await fileStream.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<Dictionary<string, SidechainInfo>>(content);
                }
            }
        }
    }
}
