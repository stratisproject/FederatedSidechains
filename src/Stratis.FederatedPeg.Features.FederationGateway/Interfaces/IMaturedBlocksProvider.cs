using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IMaturedBlocksProvider
    {
        Task<List<IMaturedBlockDeposits>> GetMaturedDepositsAsync(int blockHeight, int maxBlocks);
    }
}