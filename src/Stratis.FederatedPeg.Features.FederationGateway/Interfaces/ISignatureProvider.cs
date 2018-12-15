namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface ISignatureProvider
    {
        /// <summary>
        /// Signs an externally provided withdrawal transaction if it is deemed valid. This method
        /// is used to sign a transaction in response to signature requests from the federation
        /// leader.
        /// </summary>
        /// <remarks>
        /// This method requires federation to be active as the wallet password is supplied during
        /// activation. Transaction's are validated to ensure that they are expected as per
        /// the deposits received on the source chain.
        /// </remarks>
        /// <param name="transactionHex">The hexadecimal representations of transactions to sign.</param>
        /// <returns>An array of signed transactions (in hex) or <c>null</c> for transactions that can't be signed.</returns>
        string SignTransaction(string transactionHex);
    }
}
