using System.Runtime.CompilerServices;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.FederatedPeg.IntegrationTests.Tools
{
    public class FederationGatewayNodeBuilder : NodeBuilder
    {
        private FederationGatewayNodeBuilder(string rootFolder) : base(rootFolder) { }

        public static FederationGatewayNodeBuilder CreateFederationGatewayNodeBuilder(
            object caller, [CallerMemberName] string callingMethod = null)
        {
            string testFolderPath = TestBase.CreateTestDir(caller, callingMethod);
            var builder = new FederationGatewayNodeBuilder(testFolderPath);
            builder.WithLogsDisabled();

            return builder;
        }

        public CoreNode CreateFederationGatewayNode(PoANetwork network)
        {
            return this.CreateNode(new FederationGatewayNodeRunner(
                this.GetNextDataFolderName(), network), "fedgateway.conf");
        }

        public CoreNode CreateFederationGatewayNode(PoANetwork network, Key key)
        {
            string dataFolder = this.GetNextDataFolderName();
            CoreNode node = this.CreateNode(new FederationGatewayNodeRunner(dataFolder, network), "fedgateway.conf");

            var settings = new NodeSettings(network, args: new string[] { "-conf=fedgateway.conf", "-datadir=" + dataFolder });
            var tool = new KeyTool(settings.DataFolder);
            tool.SavePrivateKey(key);

            return node;
        }
    }
}
