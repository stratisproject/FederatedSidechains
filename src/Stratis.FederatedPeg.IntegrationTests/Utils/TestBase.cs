using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.FederatedPeg.IntegrationTests.Utils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using FluentAssertions;
    using Flurl;
    using Flurl.Http;
    using NBitcoin;
    using Stratis.Bitcoin.IntegrationTests.Common;
    using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
    using Stratis.Bitcoin.Networks;
    using Stratis.FederatedPeg.Features.FederationGateway;
    using Stratis.FederatedPeg.Features.FederationGateway.Models;
    using Stratis.Sidechains.Networks;

    public class TestBase : IDisposable
    {
        private const string WalletName = "mywallet";
        private const string WalletPassword = "password";
        private const string WalletPassphrase = "passphrase";

        protected readonly Network mainchainNetwork;
        protected readonly FederatedPegRegTest sidechainNetwork;
        protected readonly IList<Mnemonic> mnemonics;
        protected readonly Dictionary<Mnemonic, PubKey> pubKeysByMnemonic;
        protected readonly (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress) scriptAndAddresses;
        protected readonly List<int> federationMemberIndexes;
        protected readonly List<string> chains;

        protected readonly IReadOnlyDictionary<string, NodeChain> MainAndSideChainNodeMap;

        private readonly NodeBuilder nodeBuilder;
        private readonly CoreNode mainUser;
        private readonly CoreNode fedMain1;
        private readonly CoreNode fedMain2;
        private readonly CoreNode fedMain3;

        private readonly SidechainNodeBuilder sidechainNodeBuilder;
        private readonly CoreNode sideUser;
        private readonly CoreNode fedSide1;
        private readonly CoreNode fedSide2;
        private readonly CoreNode fedSide3;

        private const string ConfigSideChain = "sidechain";
        private const string ConfigAgentPrefix = "agentprefix";

        protected enum Chain
        {
            Main,
            Side
        }

        protected class NodeChain
        {
            public CoreNode Node { get; private set; }
            public Chain ChainType { get; private set; }

            public NodeChain(CoreNode node, Chain chainType)
            {
                this.Node = node;
                this.ChainType = chainType;
            }
        }

        public TestBase()
        {
            this.mainchainNetwork = Networks.Stratis.Regtest();
            this.sidechainNetwork = (FederatedPegRegTest)FederatedPegNetwork.NetworksSelector.Regtest();

            this.mnemonics = this.sidechainNetwork.FederationMnemonics;
            this.pubKeysByMnemonic = this.mnemonics.ToDictionary(m => m, m => m.DeriveExtKey().PrivateKey.PubKey);

            this.scriptAndAddresses = this.GenerateScriptAndAddresses(this.mainchainNetwork, this.sidechainNetwork, 2, this.pubKeysByMnemonic);

            this.federationMemberIndexes = Enumerable.Range(0, this.pubKeysByMnemonic.Count).ToList();
            this.chains = new[] { "mainchain", "sidechain" }.ToList();

            this.nodeBuilder = NodeBuilder.Create(this);
            this.mainUser = this.nodeBuilder.CreateStratisPosNode(this.mainchainNetwork, nameof(this.mainUser)).WithWallet(); // TODO: Do we need wallets like this on every node?
            this.fedMain1 = this.nodeBuilder.CreateStratisPosNode(this.mainchainNetwork, nameof(this.fedMain1));
            this.fedMain2 = this.nodeBuilder.CreateStratisPosNode(this.mainchainNetwork, nameof(this.fedMain2));
            this.fedMain3 = this.nodeBuilder.CreateStratisPosNode(this.mainchainNetwork, nameof(this.fedMain3));

            this.sidechainNodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this);
            this.sideUser = this.sidechainNodeBuilder.CreateSidechainNode(this.sidechainNetwork);
            this.fedSide1 = this.sidechainNodeBuilder.CreateSidechainFederationNode(this.sidechainNetwork, this.sidechainNetwork.FederationKeys[0]);
            this.fedSide2 = this.sidechainNodeBuilder.CreateSidechainFederationNode(this.sidechainNetwork, this.sidechainNetwork.FederationKeys[1]);
            this.fedSide3 = this.sidechainNodeBuilder.CreateSidechainFederationNode(this.sidechainNetwork, this.sidechainNetwork.FederationKeys[2]);

            this.MainAndSideChainNodeMap = new Dictionary<string, NodeChain>()
            {
                { nameof(this.mainUser), new NodeChain(this.mainUser, Chain.Main) },
                { nameof(this.fedMain1), new NodeChain(this.fedMain1, Chain.Main) },
                { nameof(this.fedMain2), new NodeChain(this.fedMain2, Chain.Main) },
                { nameof(this.fedMain3), new NodeChain(this.fedMain3, Chain.Main) },
                { nameof(this.sideUser), new NodeChain(this.sideUser, Chain.Side) },
                { nameof(this.fedSide1), new NodeChain(this.fedSide1, Chain.Side) },
                { nameof(this.fedSide2), new NodeChain(this.fedSide2, Chain.Side) },
                { nameof(this.fedSide3), new NodeChain(this.fedSide3, Chain.Side) }
            };

            this.ApplyConfigParametersToNodes();
        }

        protected (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress)
            GenerateScriptAndAddresses(Network mainchainNetwork, Network sidechainNetwork, int quorum, Dictionary<Mnemonic, PubKey> pubKeysByMnemonic)
        {
            Script payToMultiSig = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(quorum, pubKeysByMnemonic.Values.ToArray());
            BitcoinAddress sidechainMultisigAddress = payToMultiSig.Hash.GetAddress(sidechainNetwork);
            BitcoinAddress mainchainMultisigAddress = payToMultiSig.Hash.GetAddress(mainchainNetwork);
            return (payToMultiSig, sidechainMultisigAddress, mainchainMultisigAddress);
        }

        protected void StartAndConnectNodes()
        {
            this.StartNodes(Chain.Main);
            this.StartNodes(Chain.Side);

            TestHelper.WaitLoop(() =>
            {
                return this.fedMain3.State == CoreNodeState.Running &&
                        this.fedSide3.State == CoreNodeState.Running;
            });

            this.ConnectMainChainNodes();
            this.ConnectSideChainNodes();
        }

        protected void StartNodes(Chain chainType)
        {
            try
            {
                this.MainAndSideChainNodeMap.
                    Where(m => m.Value.ChainType == chainType).
                    Select(x => x.Value.Node).ToList().
                    ForEach(m => m.Start());
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected void ConnectMainChainNodes()
        {
            try
            {
                TestHelper.Connect(this.mainUser, this.fedMain1);
                TestHelper.Connect(this.mainUser, this.fedMain2);
                TestHelper.Connect(this.mainUser, this.fedMain3);
                TestHelper.Connect(this.fedMain1, this.fedMain2);
                TestHelper.Connect(this.fedMain1, this.fedMain3);
                TestHelper.Connect(this.fedMain2, this.fedMain1);
                TestHelper.Connect(this.fedMain2, this.fedMain3);
                TestHelper.Connect(this.fedMain3, this.fedMain1);
                TestHelper.Connect(this.fedMain3, this.fedMain2);
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected void ConnectSideChainNodes()
        {
            try
            {
                TestHelper.Connect(this.sideUser, this.fedSide1);
                TestHelper.Connect(this.sideUser, this.fedSide2);
                TestHelper.Connect(this.sideUser, this.fedSide3);
                TestHelper.Connect(this.fedSide1, this.fedSide2);
                TestHelper.Connect(this.fedSide1, this.fedSide3);
                TestHelper.Connect(this.fedSide2, this.fedSide1);
                TestHelper.Connect(this.fedSide2, this.fedSide3);
                TestHelper.Connect(this.fedSide3, this.fedSide1);
                TestHelper.Connect(this.fedSide3, this.fedSide2);

            }
            catch (Exception)
            {
                throw;
            }
        }

        protected void EnableWallets(List<CoreNode> nodes)
        {
            this.MainAndSideChainNodeMap["fedMain3"].Node.State.Should().Be(CoreNodeState.Running);
            this.MainAndSideChainNodeMap["fedSide3"].Node.State.Should().Be(CoreNodeState.Running);

            nodes.ForEach(node =>
            {
                this.federationMemberIndexes.ForEach(i =>
                {
                    $"http://localhost:{node.ApiPort}/api".AppendPathSegment("FederationWallet/import-key").PostJsonAsync(new ImportMemberKeyRequest
                    {
                        Mnemonic = this.mnemonics[i].ToString(),
                        Password = "password"
                    }).Result.StatusCode.Should().Be(HttpStatusCode.OK);

                    $"http://localhost:{node.ApiPort}/api".AppendPathSegment("FederationWallet/enable-federation").PostJsonAsync(new EnableFederationRequest
                    {
                        Password = "password"
                    }).Result.StatusCode.Should().Be(HttpStatusCode.OK);
                });
            });
        }

        /// <summary>
        /// Get balance of the local wallet.
        /// </summary>
        protected Money GetBalance(CoreNode node)
        {
            IEnumerable<Bitcoin.Features.Wallet.UnspentOutputReference> spendableOutputs = node.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName);
            return spendableOutputs.Sum(x => x.Transaction.Amount);
        }

        /// <summary>
        /// Helper method to build and send a deposit transaction to the federation on the main chain.
        /// </summary>
        protected async Task DepositToSideChain(CoreNode node, decimal amount, string sidechainDepositAddress)
        {
            HttpResponseMessage depositTransaction = await $"http://localhost:{node.ApiPort}/api"
                .AppendPathSegment("wallet/build-transaction")
                .PostJsonAsync(new
                {
                    walletName = WalletName,
                    accountName = "account 0",
                    password =  WalletPassphrase,
                    opReturnData = sidechainDepositAddress,
                    feeAmount = "0.01",
                    recipients = new[]
                    {
                        new
                        {
                            destinationAddress = this.scriptAndAddresses.mainchainMultisigAddress.ToString(),
                            amount = amount
                        }
                    }
                });

            string result = await depositTransaction.Content.ReadAsStringAsync();
            WalletBuildTransactionModel walletBuildTxModel = JsonConvert.DeserializeObject<WalletBuildTransactionModel>(result);

            HttpResponseMessage sendTransaction = await $"http://localhost:{node.ApiPort}/api"
                .AppendPathSegment("wallet/send-transaction")
                .PostJsonAsync(new
                {
                    hex = walletBuildTxModel.Hex
                });

            // TODO: Check transaction sent without errors
        }

        private void ApplyFederationIPs(CoreNode fed1, CoreNode fed2, CoreNode fed3)
        {
            string fedIps = $"{fed1.Endpoint},{fed2.Endpoint},{fed3.Endpoint}";

            this.AppendToConfig(fed1, $"{FederationGatewaySettings.FederationIpsParam}={fedIps}");
            this.AppendToConfig(fed2, $"{FederationGatewaySettings.FederationIpsParam}={fedIps}");
            this.AppendToConfig(fed3, $"{FederationGatewaySettings.FederationIpsParam}={fedIps}");
        }


        private void ApplyCounterChainAPIPort(CoreNode fromNode, CoreNode toNode)
        {
            this.AppendToConfig(fromNode, $"{FederationGatewaySettings.CounterChainApiPortParam}={toNode.ApiPort.ToString()}");
            this.AppendToConfig(toNode, $"{FederationGatewaySettings.CounterChainApiPortParam}={fromNode.ApiPort.ToString()}");
        }

        private void AppendToConfig(CoreNode node, string configKeyValueIten)
        {
            using (StreamWriter sw = File.AppendText(node.Config))
            {
                sw.WriteLine(configKeyValueIten);
            }
        }

        private void ApplyConfigParametersToNodes()
        {
            this.AppendToConfig(this.fedSide1, $"{ConfigSideChain}=1");
            this.AppendToConfig(this.fedSide2, $"{ConfigSideChain}=1");
            this.AppendToConfig(this.fedSide3, $"{ConfigSideChain}=1");

            this.AppendToConfig(this.fedSide1, $"{FederationGatewaySettings.RedeemScriptParam}={this.scriptAndAddresses.payToMultiSig.ToString()}");
            this.AppendToConfig(this.fedSide2, $"{FederationGatewaySettings.RedeemScriptParam}={this.scriptAndAddresses.payToMultiSig.ToString()}");
            this.AppendToConfig(this.fedSide3, $"{FederationGatewaySettings.RedeemScriptParam}={this.scriptAndAddresses.payToMultiSig.ToString()}");

            this.AppendToConfig(this.fedSide1, $"{FederationGatewaySettings.PublicKeyParam}={this.pubKeysByMnemonic[this.mnemonics[0]].ToString()}");
            this.AppendToConfig(this.fedSide2, $"{FederationGatewaySettings.PublicKeyParam}={this.pubKeysByMnemonic[this.mnemonics[1]].ToString()}");
            this.AppendToConfig(this.fedSide3, $"{FederationGatewaySettings.PublicKeyParam}={this.pubKeysByMnemonic[this.mnemonics[2]].ToString()}");

            this.ApplyFederationIPs(this.fedMain1, this.fedMain2, this.fedMain3);
            this.ApplyFederationIPs(this.fedSide1, this.fedSide2, this.fedSide3);

            this.ApplyCounterChainAPIPort(this.fedMain1, this.fedSide1);
            this.ApplyCounterChainAPIPort(this.fedMain2, this.fedSide2);
            this.ApplyCounterChainAPIPort(this.fedMain3, this.fedSide3);

            this.MainAndSideChainNodeMap.ToList().ForEach(n =>
            {
                this.AppendToConfig(n.Value.Node, $"{ConfigAgentPrefix}={n.Key}");
            });
        }

        public void Dispose()
        {
            this.nodeBuilder?.Dispose();
            this.sidechainNodeBuilder?.Dispose();
        }
    }
}
