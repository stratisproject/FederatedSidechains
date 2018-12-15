namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface ISignatureProvider
    {
        /// <summary>
        /// Signs a transaction if it can be authorized.
        /// </summary>
        /// <param name="transactionHex">The hexadecimal representations of transactions to sign.</param>
        /// <returns>An array of signed transactions (in hex) or <c>null</c> for transactions that can't be signed.</returns>
        string SignTransaction(string transactionHex);
    }
}
