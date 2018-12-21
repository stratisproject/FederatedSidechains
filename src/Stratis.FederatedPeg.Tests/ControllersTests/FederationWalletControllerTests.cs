using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Xunit;

namespace Stratis.FederatedPeg.Tests.ControllersTests
{
    public class FederationWalletControllerTests
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IFederationWalletManager walletManager;
        private readonly IFederationWalletSyncManager walletSyncManager;
        private readonly IConnectionManager connectionManager;
        private readonly Network network;
        private readonly ConcurrentChain chain;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly IWithdrawalHistoryProvider withdrawalHistoryProvider;

        private readonly FederationWalletController controller;

        public FederationWalletControllerTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.walletManager = Substitute.For<IFederationWalletManager>();
            this.walletSyncManager = Substitute.For<IFederationWalletSyncManager>();
            this.connectionManager = Substitute.For<IConnectionManager>();
            this.network = new StratisTest();
            this.chain = Substitute.For<ConcurrentChain>();
            this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
            this.withdrawalHistoryProvider = Substitute.For<IWithdrawalHistoryProvider>();

            this.controller = new FederationWalletController(this.loggerFactory, this.walletManager, this.walletSyncManager,
                this.connectionManager, this.network, this.chain, this.dateTimeProvider, this.withdrawalHistoryProvider);
        }

        [Fact]
        public void QWE()
        {

        }
    }
}
