using System;
using System.Text;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Networks;
using Stratis.Sidechains.Networks;
using Xunit;
using Xunit.Abstractions;

namespace FederationSetup
{
    public class MultisigAddressCreator
    {
        private readonly ITestOutputHelper output;

        public MultisigAddressCreator(ITestOutputHelper output = null)
        {
            if (output == null) return;
            this.output = output;
        }

        //[Fact]
        [Fact(Skip = "This is not a test, it is meant to be run upon creating a network")]
        public void Run_CreateMultisigAddresses()
        {
            var mainchainNetwork = Networks.Stratis.Testnet();
            var sidechainNetwork = FederatedPegNetwork.NetworksSelector.Testnet();

            this.output.WriteLine(this.CreateMultisigAddresses(mainchainNetwork, sidechainNetwork));
        }

        public string CreateMultisigAddresses(Network mainchainNetwork, Network sidechainNetwork, int quorum = 2, int keysCount = 5)
        {
            var output = new StringBuilder();

            PubKey[] pubKeys = new PubKey[keysCount];

            for (int i = 0; i < keysCount; i++)
            {
                string password = "mypassword";

                // Create a mnemonic and get the corresponding pubKey.
                Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                var pubKey = mnemonic.DeriveExtKey().PrivateKey.PubKey;
                pubKeys[i] = pubKey;

                output.AppendLine($"Mnemonic - Please note the following 12 words down in a secure place: {string.Join(" ", mnemonic.Words)}");
                output.AppendLine($"PubKey   - Please share the following public key with the person responsible for the sidechain generation: {Encoders.Hex.EncodeData((pubKey).ToBytes(false))}");
                output.AppendLine(Environment.NewLine);
            }

            Script payToMultiSig = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(quorum, pubKeys);
            output.AppendLine("Redeem script: " + payToMultiSig.ToString());

            BitcoinAddress sidechainMultisigAddress = payToMultiSig.Hash.GetAddress(sidechainNetwork);
            output.AppendLine("Sidechan P2SH: " + sidechainMultisigAddress.ScriptPubKey);
            output.AppendLine("Sidechain Multisig address: " + sidechainMultisigAddress);

            BitcoinAddress mainchainMultisigAddress = payToMultiSig.Hash.GetAddress(mainchainNetwork);
            output.AppendLine("Mainchain P2SH: " + mainchainMultisigAddress.ScriptPubKey);
            output.AppendLine("Mainchain Multisig address: " + mainchainMultisigAddress);

            return output.ToString();
        }
    }
}