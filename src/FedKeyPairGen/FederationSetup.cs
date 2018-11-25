using System;
using System.Reflection;

namespace FederationSetup
{
    // The FedKeyPairGenManager handles console output and input.
    internal static class FederationSetup
    {
        // Standard information header.
        public static void OutputHeader()
        {
            Console.WriteLine($"Stratis Federation Set up v{Assembly.GetEntryAssembly().GetName().Version.ToString()}");
            Console.WriteLine("* Creates genesis blocks for Main/Test/Reg networks.");
            Console.WriteLine("* Generates cryptographic private and public keys for Sidechain Federation Members.");
            Console.WriteLine("* Create multisig address.");
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine("For help : federationsetup -h");
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine("Copyright (c) 2018 Stratis Group Limited");
            Console.WriteLine(Environment.NewLine);
        }

        // A standard usage message with examples.  This is output on -h command and also in some cases if validation fails.
        public static void OutputUsage()
        {
            Console.WriteLine("usage: federationsetup [-h] [-g] [-p] [-m]");
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine("Show help message:                   -h");
            Console.WriteLine("Create genesis blocks:               -g");
            Console.WriteLine("Create private and public keys:      -p");  // ask members to create public and private -p (for the specfic network)  - 1 pubpriv for signing transactions and 1 for pubpriv key for mining
            Console.WriteLine("Create multi signature addresses:    -m");
            Console.WriteLine("                                     [network] [-fedpubkeys] [-quorum]");
            Console.WriteLine("                                     network:    TestNet or RegTest (default MainNet).");
            Console.WriteLine("                                     quorum:     The minimum odd number of federated members.");  // fed admin will do -m and number (3 qurom + the public keys for the signing of transactions
            Console.WriteLine("                                     fedpubkeys: Federation members' public keys - must have an odd number of up to fifteen members.");  // fed admin will do -m and number (3 qurom + the public keys for the signing of transactions
            Console.WriteLine("                                     Example:    federationsetup -m testnet -keys=");
            Console.WriteLine("                                                     03f1cfdd3f10fd6d399bd768db7bd989a9df3bae48b96e28c96644cab6585a0c34,");
            Console.WriteLine("                                                     03b45ff90d88f50bee1523724d22befa79ae5438e2e90813cfc88dc3f921b95cf0,");
            Console.WriteLine("                                                     03f1cfdd3f10fd6d399bd768db7bd989a9df3bae48b96e28c96644cab6585a0c34,");
            Console.WriteLine("                                                     03421dd8b11d718b598066f0cd3cda16bfb445d3f40fad364291cd92b2b296df0a,");
            Console.WriteLine("                                                     03b4a8a70890cbb89d203ea236df0760f60ef977d3cafb1395ee01db45b3529129,");
            Console.WriteLine("                                                     02ab983ada09640c2259e54822958129af61fea2589c36d9a9aae414fddc4bed70");
            Console.WriteLine("                                                     -quorum=2");
            Console.WriteLine(Environment.NewLine);
        }

        // Output completion message and secret warning.
        public static void OutputSuccess()
        {
            Console.WriteLine();
            Console.WriteLine("Done!");
        }

        // On error we output in red.
        public static void OutputErrorLine(string message)
        {
            var colorSaved = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = colorSaved;
        }
    }
}
