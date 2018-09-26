using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Networks;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Networks;

namespace Stratis.Sidechains.Networks
{
    public class ApexMain : StratisMain
    {
        public ApexMain()
        {
            this.Name = ApexNetwork.MainNetworkName;
            this.RootFolderName = ApexNetwork.ChainName.ToLowerInvariant();
            this.DefaultConfigFilename = $"{ApexNetwork.ChainName.ToLowerInvariant()}.conf";
            this.DefaultPort = 36000;
            this.RPCPort = 36100;
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { 23 }; // A
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { 83 }; // a
            this.Magic = 0x522357A;
            this.CoinTicker = "APX";
            
            this.Checkpoints = new Dictionary<int, CheckpointInfo>();
            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();

            var consensusFactory = new ConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1528217223;
            this.GenesisNonce = 58285;
            this.GenesisBits = this.Consensus.PowLimit.ToCompact();
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Coins(50m);
            Block genesisBlock = CreateStratisGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 0,
                [BuriedDeployments.BIP65] = 0,
                [BuriedDeployments.BIP66] = 0
            };

            var bip9Deployments = new BIP9DeploymentsArray();

            var consensusOptions = new ConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000);

            this.Consensus = new Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 3000,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"),
                ruleChangeActivationThreshold: 1916, // 95% of 2016
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 0,
                defaultAssumeValid: null,
                maxMoney: Money.Coins(20000000),
                coinbaseMaturity: 50,
                premineHeight: 2,
                premineReward: Money.Coins(20000000),
                proofOfWorkReward: Money.Zero,
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
                powAllowMinDifficultyBlocks: false,
                powNoRetargeting: false,
                powLimit: new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")),
                minimumChainWork: null,
                isProofOfStake: true,
                lastPowBlock: 12500,
                proofOfStakeLimit: new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeLimitV2: new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeReward: Money.Zero);

            this.Genesis = ApexNetwork.CreateGenesisBlock(this.Consensus.ConsensusFactory, this.GenesisTime, this.GenesisNonce, this.Consensus.PowLimit, this.GenesisVersion, this.GenesisReward);
            Assert(this.Consensus.HashGenesisBlock.ToString() == "d01e4ad8b0d0738d283598115540bf484161dd89871586b5eb7d09a7f7a86796");
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("b4ee5b5155eea267f7be1fdffc1975f04b0e1d9717dc19696619577e8ffdc70e"));
        }
    }
}