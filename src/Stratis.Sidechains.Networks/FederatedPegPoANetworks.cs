using NBitcoin;

namespace Stratis.Sidechains.Networks
{
    public static class FederatedPegPoANetworks
    {
        public static NetworksSelector FederatedPegPoA
        {
            get
            {
                return new NetworksSelector(() => new FederatedPegPoAMain(), () => new FederatedPegPoATest(), () => new FederatedPegPoARegTest());
            }
        }
    }
}
