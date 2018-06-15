﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;

//using CoreNode = Stratis.Bitcoin.

namespace Stratis.FederatedPeg.IntegrationTests.Helpers
{
    internal class IntegrationTestUtils
    {
        public static void RunFedKeyPairGen(string name, string passPhrase, string folder = null, [CallerMemberName] string caller = null)
        {
            if (folder == null) folder = $"Federations\\{caller}";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"..\\..\\..\\..\\FedKeyPairGen\\bin\\Release\\PublishOutput\\FedKeyPairGen.dll -name={name} -pass={passPhrase} -folder={folder}",
                    UseShellExecute = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                }
            };

            //this call assumes you have used DotNet Publish to publish the FedKeyPairGen.
            process.Start();
        }

        public static void DistributeKeys(string memberFolder)
        {
            var memberFolderManager = new MemberFolderManager(memberFolder);
            var members = memberFolderManager.LoadMembers();

            int memberFolderName = 1;
            foreach (var member in members)
            {
                //create a folder for the member and copy his files there
                //this represents the folder on a federation gateway where a member keeps his keys
                //copy his public key
                //copy his private key
                //copy the multisig public address
                //copy the multisig scriptpubkey

                //member photo
                var mainchainDest = Directory.CreateDirectory($"{memberFolder}\\{memberFolderName}\\Mainchain");
                var sidechainDest = Directory.CreateDirectory($"{memberFolder}\\{memberFolderName}\\Sidechain");

                File.Copy(Path.Combine(memberFolder, $"PUBLIC_mainchain_{member.Name}.txt"), Path.Combine(mainchainDest.FullName, $"PUBLIC_mainchain_{member.Name}.txt"));
                File.Copy(Path.Combine(memberFolder, $"PUBLIC_sidechain_{member.Name}.txt"), Path.Combine(sidechainDest.FullName, $"PUBLIC_sidechain_{member.Name}.txt"));

                File.Copy(Path.Combine(memberFolder, $"PRIVATE_DO_NOT_SHARE_mainchain_{member.Name}.txt"), Path.Combine(mainchainDest.FullName, $"PRIVATE_DO_NOT_SHARE_mainchain_{member.Name}.txt"));
                File.Copy(Path.Combine(memberFolder, $"PRIVATE_DO_NOT_SHARE_sidechain_{member.Name}.txt"), Path.Combine(sidechainDest.FullName, $"PRIVATE_DO_NOT_SHARE_sidechain_{member.Name}.txt"));

                File.Copy(Path.Combine(memberFolder, "Mainchain_ScriptPubKey.txt"), Path.Combine(mainchainDest.FullName, "Mainchain_ScriptPubKey.txt"));
                File.Copy(Path.Combine(memberFolder, "Sidechain_ScriptPubKey.txt"), Path.Combine(sidechainDest.FullName, "Sidechain_ScriptPubKey.txt"));

                File.Copy(Path.Combine(memberFolder, "Mainchain_Address.txt"), Path.Combine(mainchainDest.FullName, "Mainchain_Address.txt"));
                File.Copy(Path.Combine(memberFolder, "Sidechain_Address.txt"), Path.Combine(sidechainDest.FullName, "Sidechain_Address.txt"));

                ++memberFolderName;
            }
        }

        public static void CreateScriptAndAddress(string memberfolder, Network network)
        {
            var memberFolderManager = new MemberFolderManager(memberfolder);
            var federation = memberFolderManager.LoadFederation(2, 3);
            memberFolderManager.OutputScriptPubKeyAndAddress(federation, network);
            memberFolderManager.OutputScriptPubKeyAndAddress(federation, network);
        }

        public static async Task WaitLoop(Func<bool> act)
        {
            var cancel = new CancellationTokenSource(Debugger.IsAttached ? 15 * 60 * 1000 : 30 * 1000);
            while (!act())
            {
                cancel.Token.ThrowIfCancellationRequested();
                await Task.Delay(50);
            }
        }

        public static bool AreNodesSynced(CoreNode node1, CoreNode node2)
        {
            if (node1.FullNode.Chain.Tip.HashBlock != node2.FullNode.Chain.Tip.HashBlock) return false;
            if (node1.FullNode.ChainBehaviorState.ConsensusTip.HashBlock != node2.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) return false;
            if (node1.FullNode.GetBlockStoreTip().HashBlock != node2.FullNode.GetBlockStoreTip().HashBlock) return false;
            if (node1.FullNode.MempoolManager().InfoAll().Count != node2.FullNode.MempoolManager().InfoAll().Count) return false;
            if (node1.FullNode.WalletManager().WalletTipHash != node2.FullNode.WalletManager().WalletTipHash) return false;
            if (node1.CreateRPCClient().GetBestBlockHash() != node2.CreateRPCClient().GetBestBlockHash()) return false;
            return true;
        }

        private static IFederationWalletManager GetGeneralWalletManager(CoreNode node)
        {
            return node.FullNode.NodeService<IFederationWalletManager>();
        }

        public static bool AreNodeGeneralWalletsSynced(CoreNode node1, CoreNode node2)
        {
            return IntegrationTestUtils.GetGeneralWalletManager(node1).WalletTipHash == IntegrationTestUtils.GetGeneralWalletManager(node2).WalletTipHash;
        }

        public static void ResyncGeneralWallet(CoreNode node)
        {
            var federationWalletSyncManager = node.FullNode.NodeService<IFederationWalletSyncManager>();
            federationWalletSyncManager.SyncFromHeight(0);
        }

        public static bool IsGeneralWalletSyncedToHeight(CoreNode node, int height)
        {
            var generalWalletManager = node.FullNode.NodeService<IFederationWalletManager>() as FederationWalletManager;
            return generalWalletManager.LastBlockHeight() >= height;
        }

        //todo: duplication
        public static void SaveGeneralWallet(CoreNode node, string walletName)
        {
            var generalWalletManager = node.FullNode.NodeService<IFederationWalletManager>() as FederationWalletManager;
            var wallet = generalWalletManager.GetWallet();
            generalWalletManager.SaveWallet();
        }

        public static bool AreConnected(CoreNode node1, CoreNode node2)
        {
            var connectionManager = node1.FullNode.NodeService<IConnectionManager>();
            foreach (INetworkPeer peer in connectionManager.ConnectedPeers)
                if (peer.PeerEndPoint.Port == node2.Endpoint.Port) return true;
            return false;
        }
    }
}