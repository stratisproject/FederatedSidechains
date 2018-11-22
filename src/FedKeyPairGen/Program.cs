using System;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Networks;
using Stratis.Sidechains.Networks;

namespace FederationSetup
{
    /*
        Stratis Federation set up v1.0.0.0 - Set-up genesis block, multisig addresses and generates cryptographic key pairs for Sidechain Federation Members.
        Copyright(c) 2018 Stratis Group Limited

        usage:  federationsetup [-h]
         -h        This help message.

        Example:  federationsetup -g -a -p
    */

    // The Stratis Federation set-up is a console app that can be sent to Federation Members
    // in order to set-up the network and generate their Private (and Public) keys without a need to run a Node at this stage.
    // See the "Use Case - Generate Federation Member Key Pairs" located in the Requirements folder in the
    // project repository.

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Start with the banner.
                FederationSetup.OutputHeader();

                // Help command output the usage and examples text.
                if (args.Contains("-h"))
                {
                    FederationSetup.OutputUsage();
                }

                if (args.Contains("-g"))
                {
                    Console.WriteLine(new GenesisMiner().MineGenesisBlocks(
                        new PoAConsensusFactory(),
                        "https://www.coindesk.com/apple-co-founder-backs-dorsey-bitcoin-become-webs-currency/"));
                }

                if (args.Contains("-a"))
                {
                    Console.WriteLine(new MultisigAddressCreator().CreateMultisigAddresses(
                        Networks.Stratis.Testnet(),
                        FederatedPegNetwork.NetworksSelector.Testnet()));
                }

                if (args.Contains("-p"))
                {
                    var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                    var pubKey = mnemonic.DeriveExtKey().PrivateKey.PubKey;

                    Console.WriteLine($"-- Mnemonic --");
                    Console.WriteLine($"Please keep the following 12 words for yourself and note them down in a secure place:");
                    Console.WriteLine($"{string.Join(" ", mnemonic.Words)}");
                    Console.WriteLine();
                    Console.WriteLine($"-- To share with the sidechain generator --");
                    Console.WriteLine($"1. Your pubkey: {Encoders.Hex.EncodeData((pubKey).ToBytes(false))}");
                    Console.WriteLine($"2. Your ip address: if you're willing to. This is required to help the nodes connect when bootstrapping the network.");
                    Console.WriteLine();

                    // Write success message including warnings to keep secret private keys safe.
                    FederationSetup.OutputSuccess();
                }

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                FederationSetup.OutputErrorLine($"An error occurred: {ex.Message}");
                Console.WriteLine();
                FederationSetup.OutputUsage();
            }
        }
    }
}
