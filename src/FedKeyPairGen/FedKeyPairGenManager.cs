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
            Console.WriteLine(" -h                      Show help message.");
            Console.WriteLine(" -g                      Create genesis blocks.");
            Console.WriteLine(" -p                      Create private and public keys.");  // ask members to create public and private -p (for the specfic network)  - 1 pubpriv for signing transactions and 1 for pubpriv key for mining
            Console.WriteLine(" -m [network] [-quorum]  Create multi signature addresses. ");
            Console.WriteLine("                         Network:    TestNet or RegTest (default MainNet).");
            Console.WriteLine("                         Quorum:     The minimum number of federated members.");  // fed admin will do -m and number (3 qurom + the public keys for the signing of transactions
            Console.WriteLine("                         Example:    federationsetup -m testnet -quorum=3");
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
