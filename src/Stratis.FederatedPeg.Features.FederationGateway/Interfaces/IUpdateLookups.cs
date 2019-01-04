using System;
using System.Collections.Generic;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// Provides methods for creating trackers and for updating lookups based on
    /// the information contained in the trackers.
    /// </summary>
    public interface ICrossChainLookups
    {
        /// <summary>
        /// Called to create trackers for a transaction.
        /// </summary>
        /// <returns>The trackers used by a transaction.</returns>
        Dictionary<Type, IChangeTracker> CreateTrackers();

        /// <summary>
        /// Updates the lookups affected by the changes recorded by the trackers.
        /// </summary>
        /// <param name="trackers">The trackers used by a transaction.</param>
        void UpdateLookups(Dictionary<Type, IChangeTracker> trackers);
    }
}
