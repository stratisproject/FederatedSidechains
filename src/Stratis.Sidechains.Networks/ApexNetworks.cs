using System;

using NBitcoin;

namespace Stratis.Sidechains.Networks
{
    [Obsolete("Please use FederatedPegNetwork")]
    public static class ApexNetworks
    {
        public static NetworksSelector Apex
        {
            get
            {
                return new NetworksSelector(() => new ApexMain(), () => new ApexTest(), () => new ApexRegTest());
            }
        }
    }
}
