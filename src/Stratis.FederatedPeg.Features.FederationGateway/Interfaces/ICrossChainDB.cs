using System;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface ICrossChainDB : ICrossChainLookups, IDisposable
    {
        /// <summary>Initializes the cross-chain-transfer store.</summary>
        void Initialize();

        /// <summary>
        /// Creates a <see cref="CrossChainDBTransaction"/> for either read or read/write operations.
        /// </summary>
        /// <param name="mode">The mode which is either <see cref="CrossChainDBTransactionMode.Read"/>
        /// or <see cref="CrossChainDBTransactionMode.ReadWrite"/></param>
        /// <returns>The <see cref="CrossChainDBTransaction"/> object.</returns>
        CrossChainDBTransaction GetTransaction(CrossChainDBTransactionMode mode = CrossChainDBTransactionMode.Read);
    }
}
