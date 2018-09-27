# Integration test scenarios

These are a few scenarios related to sidechains that we would like to automate as integration tests, please feel free to add to this list or review the existing. Once we are happy about a scenario, we can proceed to create corresponding issues and implement them. 

## Routine federation members

### RFM-1 - One federation member disconnect and the sidechain keeps on progressing 
1) All federation members are connected and at the top of both chains.
2) One gets disconnected and other federation members keep on as usual. POA based sidechains progress without the disconnected member.
3) The fact that the member is offline triggers a warning on all other members (to be used in dashboard).

### RFM-2 - One federation member disconnect and comes back online 
1) Federation members is connected and at the top of both chains.
2) It then gets disconnected and other federation members keep on as usual. Both chains progress without the disconnected member.
3) The federation member comes back online and resync.
4) The federation member has the correct multisig balance on both chains.

## Cross chain transfers

### XCT-1 - With all members online during the transfer
1) All federation members are online and a deposit is made to their multisig address
2) After maxReorg a prefered leader is elected and all members starts creating partial transactions
3) All members should get the signatures of other members as they get broadcast, and persist them in store
4) The leader has enough signatures and puts a fully signed transaction in the mempool
5) All members notice the transaction in the mempool and update the status of the corresponding session in their individual stores
6) The transaction matching the transfer gets mined and all members update its status in their individual stores
7) After maxReorg on target chain, all members update the status of the session to completed and stop monitoring it.

### XCT-2 With leader offline
1) 1)2)3) Same as XCT-1
2) The leader goes offline before getting a quorum of signatures.
3) Other members get a quorum but no other member send the signed transaction to the mempool (it is not their turn yet).
4) One more block gets created on the source chain, and the next leader is selected.
5) The new leader pushes the fully signed transaction to the mempool.
6) 5)6)7) same as XCT-1

### XCT-3 With all members online and the target chain gets reorged
1) 1)-6) Same as XCT-1.
2) One block passes on source and target chains.
3) Another deposit happens one or two blocks later in the source chain and triggers another cross chain transfer.
3) Somehow a reorg happens _(Something probably needs to be done create a fork in the previous steps)_
4) Transactions matching deposits 1 and 2 come back to the mempool are dealt with in order by the current leader.
5) Transactions go back on chain and all members update their databases
6) After maxReorg on target chain, all members update the status of the session to completed and stop monitoring it.