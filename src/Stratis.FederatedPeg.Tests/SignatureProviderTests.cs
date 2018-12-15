using System.Linq;
using System.Reactive.Linq;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Configuration;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class SignatureProviderTests : CrossChainTestBase
    {
        public SignatureProviderTests() : base()
        {
        }

        [Fact]
        public void OtherMembersCanAddSignaturesToMyTransaction()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(5);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address = (new Key()).PubKey.Hash.GetAddress(this.network);

                var deposit = new Deposit(0, new Money(160m, MoneyUnit.BTC), address.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                IMaturedBlockDeposits[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockModel() {
                        BlockHash = 1,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit })
                };

                crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits).GetAwaiter().GetResult();

                ICrossChainTransfer crossChainTransfer = crossChainTransferStore.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                Assert.NotNull(crossChainTransfer);

                Transaction transaction = crossChainTransfer.PartialTransaction;

                Assert.True(crossChainTransferStore.ValidateTransaction(transaction));

                // Create a separate instance to generate another transaction.
                Transaction transaction2;
                var newTest = new SignatureProviderTests();
                var dataFolder2 = new DataFolder(CreateTestDir(this));

                newTest.federationKeys = this.federationKeys;
                newTest.SetExtendedKey(1);
                newTest.Init(dataFolder2);
                newTest.AddFunding();
                newTest.AppendBlocks(3);

                using (ICrossChainTransferStore crossChainTransferStore2 = newTest.CreateStore())
                {
                    crossChainTransferStore2.Initialize();
                    crossChainTransferStore2.Start();

                    Assert.Equal(newTest.chain.Tip.HashBlock, crossChainTransferStore2.TipHashAndHeight.HashBlock);
                    Assert.Equal(newTest.chain.Tip.Height, crossChainTransferStore2.TipHashAndHeight.Height);

                    crossChainTransferStore2.RecordLatestMatureDepositsAsync(blockDeposits).GetAwaiter().GetResult();

                    ICrossChainTransfer crossChainTransfer2 = crossChainTransferStore2.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                    Assert.NotNull(crossChainTransfer2);

                    transaction2 = crossChainTransfer2.PartialTransaction;

                    Assert.True(crossChainTransferStore2.ValidateTransaction(transaction2));

                    // The first instance acts as signatory for the transaction coming from the second instance.
                    ISignatureProvider signatureProvider = new SignatureProvider(
                        this.federationWalletManager,
                        crossChainTransferStore,
                        this.federationGatewaySettings,
                        this.network,
                        this.loggerFactory);

                    string signedTransactionHex = signatureProvider.SignTransactions(new[] { transaction2.ToHex(this.network) }).FirstOrDefault();
                    Assert.NotNull(signedTransactionHex);

                    // The second instance parses the hex.
                    Transaction signedTransaction = newTest.network.CreateTransaction(signedTransactionHex);
                    Assert.NotNull(signedTransaction);

                    // The second instance validates the transaction and signature.
                    var outpointLookup = newTest.wallet.MultiSigAddress.Transactions.ToDictionary(t => new OutPoint(t.Id, t.Index));
                    Coin[] coins = signedTransaction.Inputs
                        .Select(input => outpointLookup[input.PrevOut])
                        .Select(td => new Coin(td.Id, (uint)td.Index, td.Amount, td.ScriptPubKey))
                        .ToArray();

                    TransactionBuilder builder = new TransactionBuilder(newTest.wallet.Network).AddCoins(coins);
                    Assert.True(builder.Verify(signedTransaction, this.federationGatewaySettings.TransactionFee, out TransactionPolicyError[] errors));
                }
            }
        }
    }
}
