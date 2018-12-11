using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.IntegrationTests.Utils;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.FederatedPeg.IntegrationTests
{
    public class MiningTestsWithSmartContracts : TestBase
    {

        [Fact]
        public void PremineIsReceived()
        {
            using (SidechainNodeBuilder builder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                builder.ConfigParameters.Add("sidechain", "true");
                builder.ConfigParameters.Add("redeemscript", this.scriptAndAddresses.payToMultiSig.ToString());
                builder.ConfigParameters.Add("publickey", this.pubKeysByMnemonic[this.mnemonics[0]].ToString());

                CoreNode node = builder.CreateSidechainNodeWithSmartContracts(this.sidechainNetwork, this.sidechainNetwork.FederationKeys[0]);
                node.Start();

                TestHelper.WaitLoop(() => node.FullNode.Chain.Height > 1);
            }
        }

    }
}
