using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class TemplateTransactionStoreTests : TestBase
    {
        public TemplateTransactionStoreTests() : base(KnownNetworks.StratisRegTest)
        {
        }

        [Fact]
        public async Task TestDBOperationsAsync()
        {
            var dir = CreateTestDir(this);

            var store = new TemplateTransactionStore(new StratisRegTest(), dir, new DateTimeProvider(), new LoggerFactory());

            await store.InitializeAsync();

            var key = new uint256(0);
            var template = new TemplateTransaction(key);

            Assert.False(await store.ExistAsync(key));

            await store.PutAsync(template);

            Assert.True(await store.ExistAsync(key));
            Assert.Equal(template, await store.GetAsync(key));

            await store.DeleteAsync(key);

            Assert.False(await store.ExistAsync(key));

            store?.Dispose();
        }
    }
}
