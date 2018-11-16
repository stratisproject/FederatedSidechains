using System.Threading.Tasks;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface ISignedMultisigTransactionBroadcaster
    {
        Task BroadcastTransactionsAsync(ILeaderProvider leaderProvider);
    }
}
