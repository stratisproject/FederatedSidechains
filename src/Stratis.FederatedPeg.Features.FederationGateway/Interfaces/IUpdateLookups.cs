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
        /// Creates trackers for recording information on how to update the lookups.
        /// </summary>
        /// <returns>Trackers for recording information on how to update the lookups.</returns>
        Dictionary<Type, IChangeTracker> CreateTrackers();

        /// <summary>
        /// Updates lookups based on the changes recorded in the tracker object.
        /// </summary>
        /// <param name="trackers">Trackers recording information about how to update the lookups.</param>
        /// <remarks>This method is intended be called by the <see cref="CrossChainDBTransaction"/> class.</remarks>
        void UpdateLookups(Dictionary<Type, IChangeTracker> trackers);
    }
}
