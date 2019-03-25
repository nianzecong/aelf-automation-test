﻿namespace AElf.Automation.Common.Helpers
{
    public enum ApiMethods
    {
        //Wallet
        AccountNew,
        AccountList,
        AccountUnlock,

        //Chain
        ConnectChain,
        LoadContractAbi,
        DeployContract,
        BroadcastTransaction,
        BroadcastTransactions,
        GetCommands,
        GetContractAbi,
        GetIncrement,
        GetTransactionResult,
        GetTransactionsResult,
        GetBlockHeight,
        GetBlockInfo,
        GetMerklePath,
        SetBlockVolumn,
        QueryView,

        //Net
        GetPeers,
        AddPeer,
        RemovePeer
    }
}