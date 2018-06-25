﻿using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    [Payload("partial")]
    public class RequestPartialTransactionPayload : Payload
    {
        private uint256 sessionId = uint256.Zero;
        private Transaction template;
        private Transaction transactionPartial;
        private uint256 bossCard = uint256.Zero;
        private int blockHeight = 0;

        public Transaction PartialTransaction => this.transactionPartial;

        public Transaction TemplateTransaction => this.template;

        public uint256 SessionId => this.sessionId;

        public uint256 BossCard => this.bossCard;
        public int BlockHeight => this.blockHeight;

        // Needed for deserialization.
        public RequestPartialTransactionPayload()
        {
        }

        public RequestPartialTransactionPayload(uint256 sessionId, Transaction template, int blockHeight)
        {
            this.sessionId = sessionId;
            this.template = template;
            this.blockHeight = blockHeight;
        }

        public void AddPartial(Transaction partialTransaction, uint256 bossCard)
        {
            this.transactionPartial = partialTransaction;
            this.bossCard = bossCard;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.sessionId);
            stream.ReadWrite(ref this.bossCard);
            stream.ReadWrite(ref this.transactionPartial);
            stream.ReadWrite(ref this.template);
            stream.ReadWrite(ref this.blockHeight);
        }
    }
}
