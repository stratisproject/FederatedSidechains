using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// Tracks changes to made to objects.
    /// </summary>
    public interface IChangeTracker
    {
        /// <summary>
        /// Records the object (or part of the object) that was originally read from the database.
        /// </summary>
        /// <param name="obj">The object to record the original value of.</param>
        void RecordOldValue(IBitcoinSerializable obj);

        /// <summary>
        /// Instructs the object to record its original value (or part thereof). Typically called after reading it from the database.
        /// </summary>
        /// <param name="obj">The object being instructed to record its original value.</param>
        void SetOldValue(IBitcoinSerializable obj);
    }
}
