using System.Collections.Generic;
using System.Linq;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// This class determines which federated member to select as the next leader based on a change in block hieght.
    /// <para>
    /// Each federated member is selected in a round robin fashion.
    /// </para>
    /// <remarks>
    /// On construction the provider will order the federated members' public keys - which live in <see cref="IFederationGatewaySettings"/> - before it determines the next leader.
    /// </remarks>
    /// </summary>
    public class LeaderProvider : ILeaderProvider
    {
        /// <summary>
        /// Ordered list of federated members' public keys.
        /// </summary>
        private readonly List<string> orderedFederationPublicKeys;

        /// <summary>
        /// A lock object that protects access when determining the next federated leader.  Locks <see cref="orderedFederationPublicKeys"/>
        /// </summary>
        private readonly object lockObject;

        public LeaderProvider(IFederationGatewaySettings federationGatewaySettings)
        {
            this.lockObject = new object();

            this.orderedFederationPublicKeys = federationGatewaySettings.FederationPublicKeys.
                Select(k => k.ToString()).
                OrderBy(j => j).
                ToList();
        }

        public NBitcoin.PubKey Update(int height)
        {
            lock (this.lockObject)
            {
                return new NBitcoin.PubKey(this.orderedFederationPublicKeys[height % this.orderedFederationPublicKeys.Count]);
            }
        }
    }
}
