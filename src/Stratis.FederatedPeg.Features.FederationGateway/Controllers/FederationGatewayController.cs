﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Controllers
{
    public static class FederationGatewayRouteEndPoint
    {
        public const string PushMaturedBlocks = "push_matured_blocks";
        public const string PushCurrentBlockTip = "push_current_block_tip";
        public const string GetMaturedBlockDeposits = "get_matured_block_deposits";
        public const string AuthorizeWithdrawals = "authorize_withdrawals";

        // TODO commented out since those constants are unused. Remove them later or start using.
        //public const string CreateSessionOnCounterChain = "create-session-oncounterchain";
        //public const string ProcessSessionOnCounterChain = "process-session-oncounterchain";
    }

    /// <summary>
    /// API used to communicate across to the counter chain.
    /// </summary>
    [Route("api/[controller]")]
    public class FederationGatewayController : Controller
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IMaturedBlockReceiver maturedBlockReceiver;

        private readonly ILeaderProvider leaderProvider;

        private readonly IMaturedBlocksProvider maturedBlocksProvider;

        private readonly ILeaderReceiver leaderReceiver;

        private readonly ISignatureProvider signatureProvider;

        public FederationGatewayController(
            ILoggerFactory loggerFactory,
            IMaturedBlockReceiver maturedBlockReceiver,
            ILeaderProvider leaderProvider,
            IMaturedBlocksProvider maturedBlocksProvider,
            ILeaderReceiver leaderReceiver,
            ISignatureProvider signatureProvider)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.maturedBlockReceiver = maturedBlockReceiver;
            this.leaderProvider = leaderProvider;
            this.maturedBlocksProvider = maturedBlocksProvider;
            this.leaderReceiver = leaderReceiver;
            this.signatureProvider = signatureProvider;
        }

        [Route(FederationGatewayRouteEndPoint.PushMaturedBlocks)]
        [HttpPost]
        public void PushMaturedBlock([FromBody] MaturedBlockDepositsModel maturedBlockDeposits)
        {
            this.maturedBlockReceiver.PushMaturedBlockDeposits(new[] { maturedBlockDeposits });
        }

        /// <summary>Pushes the current block tip to be used for updating the federated leader in a round robin fashion.</summary>
        /// <param name="blockTip"><see cref="BlockTipModel"/>Block tip Hash and Height received.</param>
        /// <returns><see cref="IActionResult"/>OK on success.</returns>
        [Route(FederationGatewayRouteEndPoint.PushCurrentBlockTip)]
        [HttpPost]
        public IActionResult PushCurrentBlockTip([FromBody] BlockTipModel blockTip)
        {
            Guard.NotNull(blockTip, nameof(blockTip));

            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                this.leaderProvider.Update(new BlockTipModel(blockTip.Hash, blockTip.Height, blockTip.MatureConfirmations));

                this.leaderReceiver.PushLeader(this.leaderProvider);

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception thrown calling /api/FederationGateway/{0}: {1}.", FederationGatewayRouteEndPoint.PushCurrentBlockTip, e.Message);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not select the next federated leader: {e.Message}", e.ToString());
            }
        }

        /// <summary>
        /// Authorizes withdrawals.
        /// </summary>
        /// <param name="authRequest">A structure containing one or more transactions to authorize.</param>
        /// <returns>An array containing one or more signed transaction or <c>null</c> for transaction that could not be authorized.</returns>
        [Route(FederationGatewayRouteEndPoint.AuthorizeWithdrawals)]
        [HttpPost]
        public IActionResult AuthorizeWithdrawals([FromBody] AuthorizeWithdrawalsModel authRequest)
        {
            Guard.NotNull(authRequest, nameof(authRequest));

            if (!this.ModelState.IsValid)
            {
                IEnumerable<string> errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                string result = this.signatureProvider.SignTransaction(authRequest.TransactionHex);

                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogTrace("Exception thrown calling /api/FederationGateway/{0}: {1}.", FederationGatewayRouteEndPoint.AuthorizeWithdrawals, e.Message);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not authorize withdrawals: {e.Message}", e.ToString());
            }
        }

        /// <summary>
        /// Retrieves blocks deposits.
        /// </summary>
        /// <param name="blockRequest">Last known block height and the maximum number of blocks to send.</param>
        /// <returns><see cref="IActionResult"/>OK on success.</returns>
        [Route(FederationGatewayRouteEndPoint.GetMaturedBlockDeposits)]
        [HttpPost]
        public async Task<IActionResult> GetMaturedBlockDepositsAsync([FromBody] MaturedBlockRequestModel blockRequest)
        {
            Guard.NotNull(blockRequest, nameof(blockRequest));

            if (!this.ModelState.IsValid)
            {
                IEnumerable<string> errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                List<IMaturedBlockDeposits> deposits = await this.maturedBlocksProvider.GetMaturedDepositsAsync(
                    blockRequest.BlockHeight, blockRequest.MaxBlocksToSend).ConfigureAwait(false);

                return this.Json(deposits);
            }
            catch (Exception e)
            {
                this.logger.LogTrace("Exception thrown calling /api/FederationGateway/{0}: {1}.", FederationGatewayRouteEndPoint.GetMaturedBlockDeposits, e.Message);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not re-sync matured block deposits: {e.Message}", e.ToString());
            }
        }

        /// <summary>
        /// Builds an <see cref="IActionResult"/> containing errors contained in the <see cref="ControllerBase.ModelState"/>.
        /// </summary>
        /// <returns>A result containing the errors.</returns>
        private static IActionResult BuildErrorResponse(ModelStateDictionary modelState)
        {
            List<ModelError> errors = modelState.Values.SelectMany(e => e.Errors).ToList();
            return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                string.Join(Environment.NewLine, errors.Select(m => m.ErrorMessage)),
                string.Join(Environment.NewLine, errors.Select(m => m.Exception?.Message)));
        }
    }
}
