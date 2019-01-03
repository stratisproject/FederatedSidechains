using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IChangeTracker
    {
        void RecordValue(IBitcoinSerializable obj, object value);
        void RecordDbValue(IBitcoinSerializable obj);
        object GetDbValue(IBitcoinSerializable obj);
        void SetDbValue(IBitcoinSerializable obj);
    }
}
