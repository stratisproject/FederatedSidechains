namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface ISignatureProvider
    {
        /// <summary>
        /// Signs a transaction if it can be authorized.
        /// </summary>
        /// <param name="transactionHex">The hexadecimal representation transaction to sign.</param>
        /// <returns>The signed transaction or <c>null</c> if it can't be signed.</returns>
        string SignTransaction(string transactionHex);
    }
}
