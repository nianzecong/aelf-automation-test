using System.Collections.Generic;
using Acs0;
using AElf.Automation.Common.OptionManagers;
using AElf.Kernel;
using AElf.Types;
using AElfChain.SDK;
using Google.Protobuf;
using log4net;
using Newtonsoft.Json.Linq;
using Volo.Abp.Threading;

namespace AElf.Automation.Common.Helpers
{
    public class WebApiHelper : IApiHelper
    {
        #region Properties

        private string _baseUrl;
        private string _chainId;
        private readonly AElfKeyStore _keyStore;
        private static readonly ILog Logger = Log4NetHelper.GetLogger();

        private string _genesisAddress;
        public string GenesisAddress => GetGenesisContractAddress();

        private AccountManager _accountManager;
        public AccountManager AccountManager => GetAccountManager();

        private TransactionManager _transactionManager;
        public TransactionManager TransactionManager => GetTransactionManager();

        #endregion

        public WebApiHelper(string baseUrl, string keyPath = "")
        {
            _baseUrl = baseUrl;
            _keyStore = new AElfKeyStore(keyPath == "" ? CommonHelper.GetCurrentDataDir() : keyPath);

            ApiService = AElfChainClient.GetClient(baseUrl, retryTimes: 10);
            CommandList = new List<CommandInfo>();
        }

        public string GetApiUrl()
        {
            return _baseUrl;
        }

        public void UpdateApiUrl(string url)
        {
            _baseUrl = url;
            ApiService = AElfChainClient.GetClient(url);
            Logger.Info($"Request url updated to: {url}");
        }

        public IApiService ApiService { get; set; }

        public List<CommandInfo> CommandList { get; set; }

        public string GetGenesisContractAddress()
        {
            if (_genesisAddress != null) return _genesisAddress;

            var statusDto = AsyncHelper.RunSync(ApiService.GetChainStatusAsync);
            _genesisAddress = statusDto.GenesisContractAddress;
            return _genesisAddress;
        }

        public CommandInfo ExecuteCommand(CommandInfo ci)
        {
            switch (ci.Method)
            {
                //Account request
                case ApiMethods.AccountNew:
                    ci = NewAccount(ci);
                    break;
                case ApiMethods.AccountList:
                    ci = ListAccounts();
                    break;
                case ApiMethods.AccountUnlock:
                    ci = UnlockAccount(ci);
                    break;
                case ApiMethods.GetChainInformation:
                    GetChainInformation(ci);
                    break;
                case ApiMethods.DeploySmartContract:
                    DeployContract(ci);
                    break;
                case ApiMethods.SendTransaction:
                    BroadcastTx(ci);
                    break;
                case ApiMethods.SendTransactions:
                    BroadcastTxs(ci);
                    break;
                case ApiMethods.GetTransactionResult:
                    GetTransactionResult(ci);
                    break;
                case ApiMethods.GetBlockHeight:
                    GetBlockHeight(ci);
                    break;
                case ApiMethods.GetBlockByHeight:
                    GetBlockByHeight(ci);
                    break;
                case ApiMethods.GetBlockByHash:
                    GetBlockByHash(ci);
                    break;
                case ApiMethods.QueryView:
                    QueryViewInfo(ci);
                    break;
                default:
                    Logger.Error("Invalid command.");
                    break;
            }

            ci.PrintResultMessage();

            if (!ci.Result) //analyze failed result
                CommandList.Add(ci);

            return ci;
        }

        #region Account methods

        public CommandInfo NewAccount(CommandInfo ci)
        {
            ci = AccountManager.NewAccount(ci.Parameter);
            return ci;
        }

        public CommandInfo ListAccounts()
        {
            var ci = AccountManager.ListAccount();
            return ci;
        }

        public CommandInfo UnlockAccount(CommandInfo ci)
        {
            var parameters = ci.Parameter.Split(' ');
            ci = AccountManager.UnlockAccount(parameters[0], parameters[1],
                parameters[2]);
            return ci;
        }

        #endregion

        #region Web request methods

        public void GetChainInformation(CommandInfo ci)
        {
            var statusDto = AsyncHelper.RunSync(ApiService.GetChainStatusAsync);
            _genesisAddress = statusDto.GenesisContractAddress;
            _chainId = statusDto.ChainId;

            ci.InfoMsg = statusDto;
            ci.Result = true;
        }

        public void DeployContract(CommandInfo ci)
        {
            if (!ci.CheckParameterValid(2))
                return;
            var parameterArray = ci.Parameter.Split(' ');
            var filename = parameterArray[0];
            var from = parameterArray[1];

            // Read sc bytes
            var contractReader = new SmartContractReader();
            var codeArray = contractReader.Read(filename);
            var input = new ContractDeploymentInput
            {
                Category = KernelConstants.CodeCoverageRunnerCategory,
                Code = ByteString.CopyFrom(codeArray)
            };

            var tx = TransactionManager.CreateTransaction(from, GenesisAddress,
                ci.Cmd, input.ToByteString());
            tx = tx.AddBlockReference(_baseUrl, _chainId);

            if (tx == null)
                return;
            tx = TransactionManager.SignTransaction(tx);
            if (tx == null)
                return;
            var rawTxString = TransactionManager.ConvertTransactionRawTxString(tx);

            var transactionOutput = AsyncHelper.RunSync(() => ApiService.SendTransactionAsync(rawTxString));

            ci.InfoMsg = transactionOutput;
            ci.Result = true;
        }

        public void BroadcastTx(CommandInfo ci)
        {
            var tr = TransactionManager.ConvertFromCommandInfo(ci);

            var parameter = ci.ParameterInput.ToByteString();
            tr.Params = parameter == null ? ByteString.Empty : parameter;
            tr = tr.AddBlockReference(_baseUrl, _chainId);
            TransactionManager.SignTransaction(tr);

            var rawTxString = TransactionManager.ConvertTransactionRawTxString(tr);

            ci.InfoMsg = AsyncHelper.RunSync(() => ApiService.SendTransactionAsync(rawTxString));
            ci.Result = true;
        }

        public void BroadcastWithRawTx(CommandInfo ci)
        {
            if (!ci.CheckParameterValid(1))
                return;
            ci.InfoMsg = AsyncHelper.RunSync(() => ApiService.SendTransactionAsync(ci.Parameter));
            ci.Result = true;
        }

        public string GenerateTransactionRawTx(CommandInfo ci)
        {
            var tr = TransactionManager.ConvertFromCommandInfo(ci);

            if (tr.MethodName == null)
            {
                ci.ErrorMsg = "Method not found.";
                return string.Empty;
            }

            var parameter = ci.ParameterInput.ToByteString();
            tr.Params = parameter == null ? ByteString.Empty : parameter;
            tr = tr.AddBlockReference(_baseUrl, _chainId);

            TransactionManager.SignTransaction(tr);

            return tr.ToByteArray().ToHex();
        }

        public string GenerateTransactionRawTx(string from, string to, string methodName, IMessage inputParameter)
        {
            var tr = new Transaction()
            {
                From = AddressHelper.Base58StringToAddress(from),
                To = AddressHelper.Base58StringToAddress(to),
                MethodName = methodName
            };

            if (tr.MethodName == null)
            {
                Logger.Error("Method not found.");
                return string.Empty;
            }

            tr.Params = inputParameter == null ? ByteString.Empty : inputParameter.ToByteString();
            tr = tr.AddBlockReference(_baseUrl, _chainId);

            TransactionManager.SignTransaction(tr);

            return tr.ToByteArray().ToHex();
        }

        public void BroadcastTxs(CommandInfo ci)
        {
            if (!ci.CheckParameterValid(1))
                return;

            ci.InfoMsg = AsyncHelper.RunSync(() => ApiService.SendTransactionsAsync(ci.Parameter));
            ci.Result = true;
        }

        public void GetTransactionResult(CommandInfo ci)
        {
            if (!ci.CheckParameterValid(1))
                return;

            ci.InfoMsg = AsyncHelper.RunSync(() => ApiService.GetTransactionResultAsync(ci.Parameter));
            ci.Result = true;
        }

        public void GetBlockHeight(CommandInfo ci)
        {
            ci.InfoMsg = AsyncHelper.RunSync(ApiService.GetBlockHeightAsync);
            ci.Result = true;
        }

        public void GetBlockByHeight(CommandInfo ci)
        {
            if (!ci.CheckParameterValid(2))
                return;

            var parameterArray = ci.Parameter.Split(' ');
            ci.InfoMsg = AsyncHelper.RunSync(
                () => ApiService.GetBlockByHeightAsync(long.Parse(parameterArray[0]), bool.Parse(parameterArray[1]))
            );
            ci.Result = true;
        }

        public void GetBlockByHash(CommandInfo ci)
        {
            if (!ci.CheckParameterValid(2))
                return;

            var parameterArray = ci.Parameter.Split(' ');
            ci.InfoMsg =
                AsyncHelper.RunSync(() => ApiService.GetBlockAsync(parameterArray[0], bool.Parse(parameterArray[1])));
            ci.Result = true;
        }

        public void GetTransactionPoolStatus(CommandInfo ci)
        {
            ci.InfoMsg = AsyncHelper.RunSync(ApiService.GetTransactionPoolStatusAsync);
            ci.Result = true;
        }

        public JObject QueryView(string from, string to, string methodName, IMessage inputParameter)
        {
            var transaction = new Transaction()
            {
                From = AddressHelper.Base58StringToAddress(from),
                To = AddressHelper.Base58StringToAddress(to),
                MethodName = methodName,
                Params = inputParameter == null ? ByteString.Empty : inputParameter.ToByteString()
            };
            transaction = TransactionManager.SignTransaction(transaction);

            var resp = CallTransaction(transaction);

            return resp == string.Empty ? new JObject() : JObject.Parse(resp);
        }

        public T QueryView<T>(string from, string to, string methodName, IMessage inputParameter)
            where T : IMessage<T>, new()
        {
            var transaction = new Transaction()
            {
                From = AddressHelper.Base58StringToAddress(from),
                To = AddressHelper.Base58StringToAddress(to),
                MethodName = methodName,
                Params = inputParameter == null ? ByteString.Empty : inputParameter.ToByteString()
            };
            transaction = TransactionManager.SignTransaction(transaction);

            var resp = CallTransaction(transaction);

            //deserialize response
            if (resp == null)
            {
                Logger.Error("ExecuteTransaction response is null.");
                return default(T);
            }

            var byteArray = ByteArrayHelper.HexStringToByteArray(resp);
            var messageParser = new MessageParser<T>(() => new T());

            return messageParser.ParseFrom(byteArray);
        }

        public void QueryViewInfo(CommandInfo ci)
        {
            ci.InfoMsg = AsyncHelper.RunSync(() => ApiService.ExecuteTransactionAsync(ci.Parameter));
            ci.Result = true;
        }

        public string GetPublicKeyFromAddress(string account, string password = "")
        {
            return AccountManager.GetPublicKey(account, password);
        }

        //Net Api
        public void NetGetPeers(CommandInfo ci)
        {
            ci.InfoMsg = AsyncHelper.RunSync(ApiService.GetPeersAsync);
            ci.Result = true;
        }

        public void NetAddPeer(CommandInfo ci)
        {
            if (!ci.CheckParameterValid(1))
                return;

            ci.InfoMsg = AsyncHelper.RunSync(() => ApiService.AddPeerAsync(ci.Parameter));
            ci.Result = true;
        }

        public void NetRemovePeer(CommandInfo ci)
        {
            if (!ci.CheckParameterValid(1))
                return;

            ci.InfoMsg = AsyncHelper.RunSync(() => ApiService.RemovePeerAsync(ci.Parameter));
            ci.Result = true;
        }

        #endregion

        private string CallTransaction(Transaction tx)
        {
            var rawTxString = TransactionManager.ConvertTransactionRawTxString(tx);
            return AsyncHelper.RunSync(() => ApiService.ExecuteTransactionAsync(rawTxString));
        }

        private TransactionManager GetTransactionManager()
        {
            if (_transactionManager != null) return _transactionManager;

            _transactionManager = new TransactionManager(_keyStore);
            return _transactionManager;
        }

        private AccountManager GetAccountManager()
        {
            if (_accountManager != null) return _accountManager;

            _accountManager = new AccountManager(_keyStore);
            return _accountManager;
        }
    }
}