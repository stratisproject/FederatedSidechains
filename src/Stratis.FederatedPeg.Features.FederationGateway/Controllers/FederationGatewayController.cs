using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.FederatedPeg.Features.FederationGateway.CounterChain;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Controllers
{
    [Route("api/[controller]")]
    public class FederationGatewayController : Controller, IFederationGatewayController
    {
        private readonly ILogger logger;

        private readonly ICounterChainSessionManager counterChainSessionManager;

        public FederationGatewayController(
            ILoggerFactory loggerFactory,
            ICounterChainSessionManager counterChainSessionManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.counterChainSessionManager = counterChainSessionManager;
        }

        [Route("create-session-oncounterchain")]
        [HttpPost]
        public IActionResult CreateSessionOnCounterChain(
            [FromBody] CreateCounterChainSessionRequest createCounterChainSessionRequest)
        {
            Guard.NotNull(createCounterChainSessionRequest, nameof(createCounterChainSessionRequest));

            this.logger.LogTrace(
                "({0}:'{1}',{2}:'{3}')",
                nameof(createCounterChainSessionRequest.BlockHeight),
                createCounterChainSessionRequest.BlockHeight,
                "Transactions Count",
                createCounterChainSessionRequest.CounterChainTransactionInfos.Count);

            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                this.counterChainSessionManager.CreateSessionOnCounterChain(
                    createCounterChainSessionRequest.BlockHeight,
                    createCounterChainSessionRequest.CounterChainTransactionInfos);
                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError(
                    "Exception thrown calling /api/FederationGateway/create-session-oncounterchain: {0}.",
                    e.Message);
                return ErrorHelpers.BuildErrorResponse(
                    HttpStatusCode.BadRequest,
                    $"Could not create session on counter chain: {e.Message}",
                    e.ToString());
            }
        }

        [Route("process-session-oncounterchain")]
        [HttpPost]
        public async Task<IActionResult> ProcessSessionOnCounterChain(
            [FromBody] CreateCounterChainSessionRequest createCounterChainSessionRequest)
        {
            Guard.NotNull(createCounterChainSessionRequest, nameof(createCounterChainSessionRequest));

            this.logger.LogTrace(
                "({0}:'{1}',{2}:'{3}')",
                nameof(createCounterChainSessionRequest.BlockHeight),
                createCounterChainSessionRequest.BlockHeight,
                "Transactions Count",
                createCounterChainSessionRequest.CounterChainTransactionInfos.Count);

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var result =
                    await this.counterChainSessionManager.ProcessCounterChainSession(
                        createCounterChainSessionRequest.BlockHeight);

                return this.Json(result);
            }
            catch (InvalidOperationException e)
            {
                this.logger.LogError(
                    "Exception thrown calling /api/FederationGateway/process-session-oncounterchain: {0}.",
                    e.Message);
                return ErrorHelpers.BuildErrorResponse(
                    HttpStatusCode.NotFound,
                    $"Could not create partial transaction session: {e.Message}",
                    e.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError(
                    "Exception thrown calling /api/FederationGateway/process-session-oncounterchain: {0}.",
                    e.Message);
                return ErrorHelpers.BuildErrorResponse(
                    HttpStatusCode.BadRequest,
                    $"Could not create partial transaction session: {e.Message}",
                    e.ToString());
            }
        }

        /// <summary>
        /// Builds an <see cref="IActionResult"/> containing errors contained in the <see cref="ControllerBase.ModelState"/>.
        /// </summary>
        /// <returns>A result containing the errors.</returns>
        private static IActionResult BuildErrorResponse(ModelStateDictionary modelState)
        {
            List<ModelError> errors = modelState.Values.SelectMany(e => e.Errors).ToList();
            return ErrorHelpers.BuildErrorResponse(
                HttpStatusCode.BadRequest,
                string.Join(Environment.NewLine, errors.Select(m => m.ErrorMessage)),
                string.Join(Environment.NewLine, errors.Select(m => m.Exception?.Message)));
        }
    }
}