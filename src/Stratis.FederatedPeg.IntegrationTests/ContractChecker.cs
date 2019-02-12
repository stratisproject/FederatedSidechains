using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DBreeze;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Utilities;
using Stratis.Sidechains.Networks;
using Stratis.SmartContracts.CLR.Decompilation;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Xunit;

namespace Stratis.FederatedPeg.IntegrationTests
{
    public class ContractChecker
    {

        [Fact]
        public async Task CheckContracts()
        {
            var nodeSettings = new NodeSettings(networksSelector: FederatedPegNetwork.NetworksSelector, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION);

            IFullNode node = GetFederatedPegFullNode(nodeSettings);

            node.Start();

            Thread.Sleep(5000);

            var testReceiptRepo = node.NodeService<TestReceiptRepo>();
            var stateRepo = node.NodeService<IStateRepositoryRoot>();

            var addresses = testReceiptRepo.GetAllContractAddresses();

            foreach (var contractAddress in addresses)
            {
                var code = stateRepo.GetCode(contractAddress);
                var decompiled = new CSharpContractDecompiler().GetSource(code);
                if (decompiled.IsFailure)
                    Console.WriteLine("Boo");
                File.WriteAllText(contractAddress.ToString(), decompiled.Value);
            }
        }



        private static IFullNode GetFederatedPegFullNode(NodeSettings nodeSettings)
        {
            IFullNode node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseBlockStore()
                .UseMempool()
                .AddSmartContracts()
                .UseSmartContractPoAConsensus()
                .UseSmartContractPoAMining()
                .UseSmartContractWallet()
                .UseReflectionExecutor()
                .UseApi()
                .AddRPC()
                .InjectTestReceiptRepo()
                .Build();

            return node;
        }
    }

    public static class TestExtensions
    {
        public static IFullNodeBuilder InjectTestReceiptRepo(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                {
                    feature.FeatureServices(services =>
                    {
                        ServiceDescriptor defaultReceiptRepo = services.FirstOrDefault(x => x.ServiceType == typeof(IReceiptRepository));

                        services.Remove(defaultReceiptRepo);
                        services.AddSingleton<IReceiptRepository>(provider => new DudReceiptRepository());
                        services.AddSingleton<TestReceiptRepo>();
                    });
                }
            });

            return fullNodeBuilder;
        }
    }

    public class DudReceiptRepository : IReceiptRepository
    {
        public void Store(IEnumerable<Receipt> receipts)
        {
            throw new NotImplementedException();
        }

        public Receipt Retrieve(uint256 txHash)
        {
            throw new NotImplementedException();
        }
    }


    public class TestReceiptRepo
    {
        private const string TableName = "receipts";
        private readonly DBreezeEngine engine;

        public TestReceiptRepo(DataFolder dataFolder)
        {
            string folder = dataFolder.SmartContractStatePath + TableName;
            Directory.CreateDirectory(folder);
            this.engine = new DBreezeEngine(folder);
        }

        public List<uint160> GetAllContractAddresses()
        {
            List<uint160> addresses = new List<uint160>();

            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                var dic = t.SelectDictionary<byte[], byte[]>(TableName);
                foreach (var item in dic)
                {
                    var receipt = Receipt.FromStorageBytesRlp(item.Value);
                    if (receipt.NewContractAddress != null)
                        addresses.Add(receipt.NewContractAddress);
                }
            }

            return addresses;
        }

        // TODO: Handle pruning old data in case of reorg.

        /// <inheritdoc />
        public void Store(IEnumerable<Receipt> receipts)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                foreach (Receipt receipt in receipts)
                {
                    t.Insert<byte[], byte[]>(TableName, receipt.TransactionHash.ToBytes(), receipt.ToStorageBytesRlp());
                }
                t.Commit();
            }
        }

        /// <inheritdoc />
        public Receipt Retrieve(uint256 hash)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                byte[] result = t.Select<byte[], byte[]>(TableName, hash.ToBytes()).Value;

                if (result == null)
                    return null;

                return Receipt.FromStorageBytesRlp(result);
            }
        }
    }
}
