using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Flurl;
using Flurl.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.IntegrationTests.Utils;
using Stratis.Sidechains.Networks;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Core;
using Xunit;

namespace Stratis.FederatedPeg.IntegrationTests
{
    public class ContractExecutionTests
    {
        private const string WalletName = "mywallet";
        private const string WalletPassword = "password";
        private const string WalletPassphrase = "passphrase";
        private const string WalletAccount = "account 0";

        private FederatedPegRegTest network;
        private (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress) scriptAndAddresses;

        [Fact]
        public async Task BasicTransferTest()
        {
            // TODO: Override the FedPeg and always send to us.

            using (SidechainNodeBuilder nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                this.network = (FederatedPegRegTest)FederatedPegNetwork.NetworksSelector.Regtest();
                IList<Mnemonic> mnemonics = network.FederationMnemonics;
                var pubKeysByMnemonic = mnemonics.ToDictionary(m => m, m => m.DeriveExtKey().PrivateKey.PubKey);
                this.scriptAndAddresses = FederationTestHelper.GenerateScriptAndAddresses(new StratisMain(), network, 2, pubKeysByMnemonic);

                CoreNode user1 = nodeBuilder.CreateSidechainNode(network).WithWallet();
                CoreNode fed1 = nodeBuilder.CreateSidechainFederationNode(network, network.FederationKeys[0]).WithWallet();
                CoreNode fed2 = nodeBuilder.CreateSidechainFederationNode(network, network.FederationKeys[1]).WithWallet();
                fed1.AppendToConfig("sidechain=1");
                fed1.AppendToConfig($"{FederationGatewaySettings.RedeemScriptParam}={this.scriptAndAddresses.payToMultiSig.ToString()}");
                fed1.AppendToConfig($"{FederationGatewaySettings.PublicKeyParam}={pubKeysByMnemonic[mnemonics[0]].ToString()}");
                fed2.AppendToConfig("sidechain=1");
                fed2.AppendToConfig($"{FederationGatewaySettings.RedeemScriptParam}={this.scriptAndAddresses.payToMultiSig.ToString()}");
                fed2.AppendToConfig($"{FederationGatewaySettings.PublicKeyParam}={pubKeysByMnemonic[mnemonics[1]].ToString()}");

                user1.Start();
                fed1.Start();

                TestHelper.Connect(user1, fed1);

                // Let fed1 get the premine
                TestHelper.WaitLoop(() => user1.FullNode.Chain.Height > network.Consensus.PremineHeight + network.Consensus.CoinbaseMaturity);

                fed2.Start();
                TestHelper.Connect(fed1, fed2);
                TestHelper.Connect(user1, fed2);

                // Send funds from fed1 to user1
                string user1Address = user1.GetUnusedAddress();
                Script scriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new BitcoinPubKeyAddress(user1Address, this.network));
                Result<WalletSendTransactionModel> result = SendTransaction(fed1, scriptPubKey, new Money(100_000, MoneyUnit.BTC));
                Assert.True(result.IsSuccess);
                int currentHeight = user1.FullNode.Chain.Height;
                TestHelper.WaitLoop(() => user1.FullNode.Chain.Height > currentHeight + 2);

                // Send new SC tx from user
                Assert.Equal(new Money(100_000, MoneyUnit.BTC), user1.GetBalance());
                byte[] contractCode = ContractCompiler.CompileFile("SmartContracts/BasicTransfer.cs").Compilation;
                string newContractAddress = await SendCreateContractTransaction(user1, contractCode, 1, user1Address);
                TestHelper.WaitLoop(() => fed1.CreateRPCClient().GetRawMempool().Length == 1);
                TestHelper.WaitLoop(() => fed2.CreateRPCClient().GetRawMempool().Length == 1);
                currentHeight = user1.FullNode.Chain.Height;
                TestHelper.WaitLoop(() => user1.FullNode.Chain.Height > currentHeight + 2);

                // Did code save?
                Assert.NotNull(user1.QueryContractCode(newContractAddress, this.network));
                Assert.NotNull(fed1.QueryContractCode(newContractAddress, this.network));
                Assert.NotNull(fed2.QueryContractCode(newContractAddress, this.network));
            }
        }

        public async Task<string> SendCreateContractTransaction(CoreNode node,
            byte[] contractCode,
            double amount,
            string sender,
            string[] parameters = null,
            ulong gasLimit = SmartContractFormatRule.GasLimitMaximum / 2, // half of maximum
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice,
            double feeAmount = 0.01)
        {
            HttpResponseMessage createContractResponse = await $"http://localhost:{node.ApiPort}/api"
                .AppendPathSegment("SmartContracts/build-and-send-create")
                .PostJsonAsync(new
                {
                    amount = amount.ToString(),
                    accountName = WalletAccount,
                    contractCode = contractCode.ToHexString(),
                    feeAmount = feeAmount.ToString(),
                    gasLimit = gasLimit,
                    gasPrice = gasPrice,
                    parameters = parameters,
                    password = WalletPassword,
                    Sender = sender,
                    walletName = WalletName
                });

            string result = await createContractResponse.Content.ReadAsStringAsync();
            return JObject.Parse(result)["newContractAddress"].ToString();
        }

        public Result<WalletSendTransactionModel> SendTransaction(CoreNode coreNode, Script scriptPubKey, Money amount)
        {
            var txBuildContext = new TransactionBuildContext(coreNode.FullNode.Network)
            {
                AccountReference = new WalletAccountReference(WalletName, WalletAccount),
                MinConfirmations = 1,
                FeeType = FeeType.Medium,
                WalletPassword = WalletPassword,
                Recipients = new[] { new Recipient { Amount = amount, ScriptPubKey = scriptPubKey } }.ToList(),
            };

            Transaction trx = (coreNode.FullNode.NodeService<IWalletTransactionHandler>() as SmartContractWalletTransactionHandler).BuildTransaction(txBuildContext);

            // Broadcast to the other node.

            IActionResult result = coreNode.FullNode.NodeService<SmartContractWalletController>().SendTransaction(new SendTransactionRequest(trx.ToHex()));
            if (result is ErrorResult errorResult)
            {
                var errorResponse = (ErrorResponse)errorResult.Value;
                return Result.Fail<WalletSendTransactionModel>(errorResponse.Errors[0].Message);
            }

            JsonResult response = (JsonResult)result;
            return Result.Ok((WalletSendTransactionModel)response.Value);
        }

    }
}
