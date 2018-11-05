using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.SignalR;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    /// <summary>
    /// This is the part responsible for subscribing to the source node's signalR hub to receive
    /// newly matured block information
    /// </summary>
    public class MaturedBlockClient : IDisposable
    {
        public HubConnection Connection { get; set; }

        private readonly ISignalRService signalR;
        private readonly ILogger logger;

        private readonly IDisposable startedStreamSubscription;

        private IDisposable maturedBlockSubscription;

        public MaturedBlockClient(ISignalRService signalR, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.signalR = signalR;

            this.startedStreamSubscription = this.signalR.StartedStream
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Subscribe(onNext: StartClient, onError: ex => this.logger.LogError(ex, "Failed to start signalR client connection."));
        }

        private void StartClient(string hubRoute)
        {
            this.Connection = new HubConnectionBuilder()
                .WithUrl(hubRoute)
                .Build();

            this.Connection.StartAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            this.Connection.Closed += this.OnConnectionClosed;

            this.maturedBlockSubscription = this.signalR.MessageStream.Subscribe(
                onNext: t => ReceiveMaturedBlock(t.topic, t.data),
                onError: exception => OnConnectionClosed(exception));
        }

        private async Task OnConnectionClosed(Exception exception)
        {
            if(exception == null) return;

            int secondsDelay = new Random().Next(0, 5);
            this.logger.LogWarning(exception, "Connection to {0} failed, trying to reconnect in {1}s.", this.signalR.HubRoute.AbsoluteUri, secondsDelay);
            await Task.Delay(TimeSpan.FromSeconds(secondsDelay));
            await this.Connection.StartAsync();
        }

        private void ReceiveMaturedBlock(string topic, string message)
        {
            this.logger.LogInformation("message received on topic {0}:{1}{2}", topic, Environment.NewLine, message);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.startedStreamSubscription?.Dispose();
            this.maturedBlockSubscription?.Dispose();
        }
    }
}
