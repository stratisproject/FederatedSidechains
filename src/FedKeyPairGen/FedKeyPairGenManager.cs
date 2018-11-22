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
            Console.WriteLine("Creates genesis blocks for Main/Test/Reg networks.");
            Console.WriteLine("Create multisig address.");
            Console.WriteLine("Generates cryptographic key pairs for Sidechain Federation Members.");
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine("For help : federationsetup -h");
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine("Copyright (c) 2018 Stratis Group Limited");
            Console.WriteLine(Environment.NewLine);
        }

        // A standard usage message with examples.  This is output on -h command and also in some cases if validation fails.
        public static void OutputUsage()
        {
            Console.WriteLine("usage: federationsetup [-h] [-g] [-a] [-p]");
            Console.WriteLine(" -h        This help message.");
            Console.WriteLine(" -g        Creates genesis blocks.");
            Console.WriteLine(" -a        Creates multi signature addresses.");
            Console.WriteLine(" -p        Creates public private keys.");
            Console.WriteLine();
            Console.WriteLine("Example:  fedsetup -g -a -p");
            Console.WriteLine();
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
