using NBitcoin;

namespace Stratis.Sidechains.Networks
{
    public static class FederatedPegNetworks
    {
        public static NetworksSelector FederatedPeg
        {
            get
            {
                return new NetworksSelector(() => new FederatedPegMain(), () => new FederatedPegTest(), () => new FederatedPegRegTest());
            }
        }
    }
}
