using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Client.Dto;
using AElf.Standards.ACS0;
using AElf.Standards.ACS3;
using AElf.Contracts.MultiToken;
using AElf.Types;
using AElfChain.Common.Contracts;
using AElfChain.Common.DtoExtension;
using AElfChain.Common.Helpers;
using AElfChain.Common.Managers;
using AElfChain.Common;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using log4net;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Shouldly;
using Volo.Abp.Threading;

namespace AElf.Automation.Contracts.ScenarioTest
{
    [TestClass]
    public class DeployContractTest
    {
        protected static readonly ILog Logger = Log4NetHelper.GetLogger();
        private readonly bool isOrganization = false;

        protected ContractTester Tester;

        public INodeManager NM { get; set; }
        public ContractManager MainManager { get; set; }
        public static string InitAccount { get; } = GetEnv("InitAccount");
        public static string Creator { get; } = GetEnv("Creator");
        public static string Member { get; } = GetEnv("Member");
        public static string OtherAccount { get; } = GetEnv("OtherAccount");

        public static string Author = GetEnv("Author");
        public static string NodesConfig = GetEnv("NodesConfig");

        private static string MainRpcUrl { get; } = GetEnv("MainRpcUrl");
        private static string SideRpcUrl { get; } = GetEnv("SideRpcUrl");
        private static string SideRpcUrl2 { get; } = GetEnv("SideRpcUrl2");
        private string Type { get; } = GetEnv("Type");

        public List<string> Members;

        public static string GetEnv(string key)
        {
            return Environment.GetEnvironmentVariable(key);
        }

        [TestInitialize]
        public void Initialize()
        {
            #region Basic Preparation

            //Init Logger
            Log4NetHelper.LogInit("ContractTest_");
            NodeInfoHelper.SetConfig(NodesConfig);

            #endregion

            NM = new NodeManager(MainRpcUrl);
            var services = new ContractServices(NM, InitAccount, Type);
            MainManager = new ContractManager(NM, InitAccount);

            Tester = new ContractTester(services);
            if (Type == "Side2" && !isOrganization)
            {
                Tester.IssueTokenToMiner(Creator);
                Tester.IssueToken(Creator, Author);
            }
            else if (isOrganization)
            {
                Tester.TokenService.TransferBalance(OtherAccount, Member, 100_00000000,
                    Tester.TokenService.GetPrimaryTokenSymbol());
                Tester.TokenService.TransferBalance(OtherAccount, InitAccount, 100_00000000,
                    Tester.TokenService.GetPrimaryTokenSymbol());
                var creator = Tester.AuthorityManager.CreateAssociationOrganization(Members);
                IssueTokenToMinerThroughOrganization(Tester, OtherAccount, creator);
            }
            else
            {
                Tester.TransferTokenToMiner(InitAccount);
                Tester.TransferToken(Author);
            }

            Members = new List<string> { InitAccount, Member, OtherAccount };
        }

        #region Proposal Deploy/Update

        [TestMethod]
        public void ProposalDeploy_MinerProposalContract_Success()
        {
            var input = ContractDeploymentInput(GetEnv("contractFileName"));
            var contractProposalInfo = ProposalNewContract(Tester, Creator, input);
            ApproveByMiner(Tester, contractProposalInfo.ProposalId);
            var release = Tester.GenesisService.ReleaseApprovedContract(contractProposalInfo, Creator);
            release.Status.ShouldBe("MINED");
            var byteString =
                ByteString.FromBase64(release.Logs.First(l => l.Name.Contains(nameof(ProposalCreated))).NonIndexed);
            var deployProposal = ProposalCreated.Parser.ParseFrom(byteString).ProposalId;

            Logger.Info($"deployProposal={deployProposal}\n ProposedContractInputHash={contractProposalInfo.ProposedContractInputHash}");
        }

        [TestMethod]
        public void ReleaseDeployCodeCheck()
        {
            string proposal = GetEnv("proposalId");
            string hash = GetEnv("proposalHash");
            var proposalId = Hash.LoadFromHex(proposal);
            var proposalHash = Hash.LoadFromHex(hash);
            Logger.Info($"[ReleaseDeployCodeCheck] Start : proposal={proposal}, hash={hash}");
            var releaseApprovedContractInput = new ReleaseContractInput
            {
                ProposedContractInputHash = proposalHash,
                ProposalId = proposalId
            };

            var release = Tester.GenesisService.ReleaseCodeCheckedContract(releaseApprovedContractInput, Creator);

            Logger.Info($"[ReleaseDeployCodeCheck] ReleaseCodeCheckedContract Result : {JsonConvert.SerializeObject(release)}");
            release.Status.ShouldBe("MINED");
            
            var byteString =
                ByteString.FromBase64(release.Logs.First(l => l.Name.Contains(nameof(ContractDeployed))).NonIndexed);
            var byteStringIndexed =
                ByteString.FromBase64(
                    release.Logs.First(l => l.Name.Contains(nameof(ContractDeployed))).Indexed.First());
            var contractDeployed = ContractDeployed.Parser.ParseFrom(byteString);
            var deployAddress = contractDeployed.Address;
            var contractVersion = contractDeployed.ContractVersion;
            var author = ContractDeployed.Parser.ParseFrom(byteStringIndexed).Author;
            Logger.Info($"deployAddress={deployAddress}, author={author}, BlockNumber={release.BlockNumber}");

            var contractInfo =
                Tester.GenesisService.CallViewMethod<ContractInfo>(GenesisMethod.GetContractInfo,
                    deployAddress);
            Logger.Info(contractInfo);
            contractInfo.ContractVersion.ShouldBe(contractVersion);
        }


        [TestMethod]
        public void ProposalUpdate_MinerProposalUpdateContract_Success()
        {
            var input = ContractUpdateInput(
                GetEnv("contractFileName"), 
                GetEnv("updateContractAddress"));
            var contractProposalInfo = ProposalUpdateContract(Tester, InitAccount, input);
            ApproveByMiner(Tester, contractProposalInfo.ProposalId);
            Logger.Info($"{contractProposalInfo.ProposalId}\n {contractProposalInfo.ProposedContractInputHash}");

            var release = Tester.GenesisService.ReleaseApprovedContract(contractProposalInfo, Creator);
            release.Status.ShouldBe("MINED");
            var byteString =
                ByteString.FromBase64(release.Logs.First(l => l.Name.Contains(nameof(ProposalCreated))).NonIndexed);
            var deployProposal = ProposalCreated.Parser.ParseFrom(byteString).ProposalId;
            Logger.Info($"{deployProposal}\n {contractProposalInfo.ProposedContractInputHash}");
        }

        [TestMethod]
        public void ReleaseUpdateCodeCheck()
        {
            string proposal = GetEnv("proposalId");
            string hash = GetEnv("proposalHash");
            var proposalId = Hash.LoadFromHex(proposal);
            var proposalHash = Hash.LoadFromHex(hash);
            var releaseApprovedContractInput = new ReleaseContractInput
            {
                ProposedContractInputHash = proposalHash,
                ProposalId = proposalId
            };

            var release = Tester.GenesisService.ReleaseCodeCheckedContract(releaseApprovedContractInput, Creator);
            release.Status.ShouldBe("MINED");
            var byteString =
                ByteString.FromBase64(release.Logs.First(l => l.Name.Contains(nameof(CodeUpdated))).Indexed.First());
            var updateAddress = CodeUpdated.Parser.ParseFrom(byteString).Address;
            var nonIndexed =
                ByteString.FromBase64(release.Logs.First(l => l.Name.Contains(nameof(CodeUpdated))).NonIndexed);
            var contractVersion = CodeUpdated.Parser.ParseFrom(nonIndexed).ContractVersion;
            Logger.Info($"{updateAddress}, {contractVersion}, {release.BlockNumber}");

            var contractInfo =
                Tester.GenesisService.CallViewMethod<ContractInfo>(GenesisMethod.GetContractInfo,
                    updateAddress);
            Logger.Info(contractInfo);
            contractInfo.ContractVersion.ShouldBe(contractVersion);
        }
        #endregion

        #region Controller

        [TestMethod]
        public void ParliamentChangeWhiteList()
        {
            var parliament = Tester.ParliamentService;
            var proposalWhiteList =
                parliament.CallViewMethod<ProposerWhiteList>(
                    ParliamentMethod.GetProposerWhiteList, new Empty());
            Logger.Info(proposalWhiteList);

            var defaultAddress = parliament.GetGenesisOwnerAddress();
            var existResult =
                parliament.CallViewMethod<BoolValue>(ParliamentMethod.ValidateOrganizationExist, defaultAddress);
            existResult.Value.ShouldBeTrue();
            if (proposalWhiteList.Proposers.Contains(Tester.GenesisService.Contract))
            {
                Logger.Info($"proposer {Tester.GenesisService.Contract} exists");
                return;
            }

            var addList = new List<Address>
            {
                Tester.GenesisService.Contract
            };
            proposalWhiteList.Proposers.AddRange(addList);
            var miners = Tester.GetMiners();

            var changeInput = new ProposerWhiteList
            {
                Proposers = { proposalWhiteList.Proposers }
            };

            var proposalId = parliament.CreateProposal(parliament.ContractAddress,
                nameof(ParliamentMethod.ChangeOrganizationProposerWhiteList), changeInput, defaultAddress,
                miners.First());
            parliament.MinersApproveProposal(proposalId, miners);

            Thread.Sleep(10000);
            parliament.SetAccount(miners.First());
            var release = parliament.ReleaseProposal(proposalId, miners.First());
            release.Status.ShouldBe(TransactionResultStatus.Mined);

            proposalWhiteList =
                parliament.CallViewMethod<ProposerWhiteList>(
                    ParliamentMethod.GetProposerWhiteList, new Empty());
            Logger.Info(proposalWhiteList);
        }

        #endregion

        #region DeployUserSmartContract/UpdateUserSmartContract

        [TestMethod]
        public void DeployUserSmartContract()
        {
            var contractFileName = GetEnv("contractFileName");
            var result = Tester.GenesisService.DeployUserSmartContract(contractFileName, Author);
            Logger.Info($"deploy {contractFileName}, result={JsonConvert.SerializeObject(result)}");
            result.Status.ConvertTransactionResultStatus().ShouldBe(TransactionResultStatus.Mined);

            var logEvent = result.Logs.First(l => l.Name.Equals(nameof(CodeCheckRequired))).NonIndexed;
            var codeCheckRequired = CodeCheckRequired.Parser.ParseFrom(ByteString.FromBase64(logEvent));
            codeCheckRequired.Category.ShouldBe(0);
            codeCheckRequired.IsSystemContract.ShouldBeFalse();
            codeCheckRequired.IsUserContract.ShouldBeTrue();
            var proposalLogEvent = result.Logs.First(l => l.Name.Equals(nameof(ProposalCreated))).NonIndexed;
            var proposalId = ProposalCreated.Parser.ParseFrom(ByteString.FromBase64(proposalLogEvent)).ProposalId;

            var returnValue =
                DeployUserSmartContractOutput.Parser.ParseFrom(
                    ByteArrayHelper.HexStringToByteArray(result.ReturnValue));
            var codeHash = returnValue.CodeHash;
            Logger.Info(
                $"Code hash: {codeHash.ToHex()}\n ProposalInput: {codeCheckRequired.ProposedContractInputHash.ToHex()}\n Proposal Id: {proposalId.ToHex()}");

            Thread.Sleep(20000);

            var currentHeight = AsyncHelper.RunSync(Tester.NodeManager.ApiClient.GetBlockHeightAsync);
            var smartContractRegistration = Tester.GenesisService.GetSmartContractRegistrationByCodeHash(codeHash);
            smartContractRegistration.ShouldNotBeNull();
            Logger.Info($"Check height: {result.BlockNumber} - {currentHeight}");

            var release = FindReleaseApprovedUserSmartContractMethod(result.BlockNumber, currentHeight);
            Logger.Info(release.TransactionId);

            var releaseLogEvent = release.Logs.First(l => l.Name.Equals(nameof(ContractDeployed)));
            var indexed = releaseLogEvent.Indexed;
            var nonIndexed = releaseLogEvent.NonIndexed;
            foreach (var i in indexed)
            {
                var contractDeployedIndexed = ContractDeployed.Parser.ParseFrom(ByteString.FromBase64(i));
                Logger.Info(contractDeployedIndexed.Author == null
                    ? $"Code hash: {contractDeployedIndexed.CodeHash.ToHex()}"
                    : $"Author: {contractDeployedIndexed.Author}");
            }

            var contractDeployedNonIndexed = ContractDeployed.Parser.ParseFrom(ByteString.FromBase64(nonIndexed));
            Logger.Info($"Address: {contractDeployedNonIndexed.Address}\n" +
                        $"{contractDeployedNonIndexed.Name}\n" +
                        $"{contractDeployedNonIndexed.Version}\n" +
                        $"{contractDeployedNonIndexed.ContractVersion}");
        }

        [TestMethod]
        public void UpdateUserSmartContract()
        {
            var contractFileName = GetEnv("contractFileName");
            var contractAddress = GetEnv("updateContractAddress");

            Logger.Info($"update contract ${contractAddress}, contractFileName=${contractFileName}");
            
            var author = Tester.GenesisService.GetContractAuthor(Address.FromBase58(contractAddress));
            Tester.TokenService.TransferBalance(InitAccount, author.ToBase58(), 10000_00000000, "STA");
            // var author = Address.FromBase58(InitAccount);
            var result =
                Tester.GenesisService.UpdateUserSmartContract(contractFileName, contractAddress, author.ToBase58());
            result.Status.ConvertTransactionResultStatus().ShouldBe(TransactionResultStatus.Mined);

            var logEvent = result.Logs.First(l => l.Name.Equals(nameof(CodeCheckRequired))).NonIndexed;
            var codeCheckRequired = CodeCheckRequired.Parser.ParseFrom(ByteString.FromBase64(logEvent));
            codeCheckRequired.Category.ShouldBe(0);
            codeCheckRequired.IsSystemContract.ShouldBeFalse();
            codeCheckRequired.IsUserContract.ShouldBeTrue();
            var proposalLogEvent = result.Logs.First(l => l.Name.Equals(nameof(ProposalCreated))).NonIndexed;
            var proposalId = ProposalCreated.Parser.ParseFrom(ByteString.FromBase64(proposalLogEvent)).ProposalId;

            Logger.Info(
                $"ProposalInput: {codeCheckRequired.ProposedContractInputHash.ToHex()}");

            // var check = CheckProposal(proposalId);
            // check.ShouldBeTrue();
            Thread.Sleep(20000);

            var currentHeight = AsyncHelper.RunSync(Tester.NodeManager.ApiClient.GetBlockHeightAsync);

            var release = FindReleaseApprovedUserSmartContractMethod(result.BlockNumber, currentHeight);
            Logger.Info(release.TransactionId);

            var releaseLogEvent = release.Logs.First(l => l.Name.Equals(nameof(CodeUpdated)));
            var indexed = releaseLogEvent.Indexed;
            var nonIndexed = releaseLogEvent.NonIndexed;
            var codeUpdatedIndexed = CodeUpdated.Parser.ParseFrom(ByteString.FromBase64(indexed.First()));
            Logger.Info($"Address: {codeUpdatedIndexed.Address}");

            var codeUpdatedNonIndexed = CodeUpdated.Parser.ParseFrom(ByteString.FromBase64(nonIndexed));
            Logger.Info($"NewCodeHash: {codeUpdatedNonIndexed.NewCodeHash}\n" +
                        $"{codeUpdatedNonIndexed.OldCodeHash}\n" +
                        $"{codeUpdatedNonIndexed.Version}\n" +
                        $"{codeUpdatedNonIndexed.ContractVersion}");

            var smartContractRegistration =
                Tester.GenesisService.GetSmartContractRegistrationByCodeHash(codeUpdatedNonIndexed.NewCodeHash);
            smartContractRegistration.ShouldNotBeNull();
            var contractInfo = Tester.GenesisService.CallViewMethod<ContractInfo>(GenesisMethod.GetContractInfo,
                contractAddress.ConvertAddress());
            Logger.Info(contractInfo);

            contractInfo.CodeHash.ShouldBe(codeUpdatedNonIndexed.NewCodeHash);
            contractInfo.Version.ShouldBe(codeUpdatedNonIndexed.Version);
            contractInfo.ContractVersion.ShouldBe(codeUpdatedNonIndexed.ContractVersion);
        }

        #endregion

        #region private method

        private ReleaseContractInput ProposalNewContract(ContractTester tester, string account,
            ContractDeploymentInput input)
        {
            var result = tester.GenesisService.ProposeNewContract(input, account);
            result.Status.ShouldBe(TransactionResultStatus.Mined);
            var proposalId = ProposalCreated.Parser
                .ParseFrom(result.Logs.First(l => l.Name.Contains(nameof(ProposalCreated))).NonIndexed).ProposalId;
            var proposalHash = ContractProposed.Parser
                .ParseFrom(result.Logs.First(l => l.Name.Contains(nameof(ContractProposed))).NonIndexed)
                .ProposedContractInputHash;
            return new ReleaseContractInput
            {
                ProposalId = proposalId,
                ProposedContractInputHash = proposalHash
            };
        }

        private ReleaseContractInput ProposalUpdateContract(ContractTester tester, string account,
            ContractUpdateInput input)
        {
            var result = tester.GenesisService.ProposeUpdateContract(input, account);
            result.Status.ShouldBe(TransactionResultStatus.Mined);
            var proposalId = ProposalCreated.Parser
                .ParseFrom(result.Logs.First(l => l.Name.Contains(nameof(ProposalCreated))).NonIndexed).ProposalId;
            var proposalHash = ContractProposed.Parser
                .ParseFrom(result.Logs.First(l => l.Name.Contains(nameof(ContractProposed))).NonIndexed)
                .ProposedContractInputHash;
            return new ReleaseContractInput
            {
                ProposalId = proposalId,
                ProposedContractInputHash = proposalHash
            };
        }

        private void ApproveByMiner(ContractTester tester, Hash proposalId)
        {
            var miners = tester.GetMiners();
            Logger.Info($"[ApproveByMiner] miners = {miners}");

            foreach (var miner in miners)
            {
                tester.ParliamentService.SetAccount(miner);
                var approve =
                    tester.ParliamentService.ExecuteMethodWithResult(ParliamentMethod.Approve, proposalId);
                Logger.Info($"[ApproveByMiner] approveResult = {JsonConvert.SerializeObject(approve)}");
                approve.Status.ShouldBe("MINED");
                if (tester.ParliamentService.CheckProposal(proposalId).ToBeReleased) return;
            }
        }

        private ContractDeploymentInput ContractDeploymentInput(string name)
        {
            var contractReader = new SmartContractReader();
            var codeArray = contractReader.Read(name);

            var input = new ContractDeploymentInput
            {
                Category = KernelHelper.DefaultRunnerCategory,
                Code = ByteString.CopyFrom(codeArray)
            };
            return input;
        }

        private ContractUpdateInput ContractUpdateInput(string name, string contractAddress)
        {
            var contractReader = new SmartContractReader();
            var codeArray = contractReader.Read(name);

            var input = new ContractUpdateInput
            {
                Address = contractAddress.ConvertAddress(),
                Code = ByteString.CopyFrom(codeArray)
            };

            return input;
        }

        private void IssueTokenToMinerThroughOrganization(ContractTester tester, string account, Address organization)
        {
            var symbol = tester.TokenService.GetPrimaryTokenSymbol();
            var miners = tester.GetMiners();
            foreach (var miner in miners)
            {
                var balance = tester.TokenService.GetUserBalance(miner, symbol);
                if (account == miner || balance > 1000_00000000) continue;
                var input = new IssueInput
                {
                    Amount = 1000_00000000,
                    Symbol = symbol,
                    To = miner.ConvertAddress()
                };
                var createProposal = tester.AssociationService.CreateProposal(tester.TokenService.ContractAddress,
                    nameof(TokenMethod.Issue), input, organization, account);
                tester.AssociationService.ApproveWithAssociation(createProposal, organization);
                tester.AssociationService.ReleaseProposal(createProposal, account);
            }
        }

        private TransactionResultDto FindReleaseApprovedUserSmartContractMethod(long startBlock, long currentHeight)
        {
            var releaseTransaction = new TransactionResultDto();
            for (var i = startBlock; i < currentHeight; i++)
            {
                var block = AsyncHelper.RunSync(() => Tester.NodeManager.ApiClient.GetBlockByHeightAsync(i));
                var transactionList = AsyncHelper.RunSync(() =>
                    Tester.NodeManager.ApiClient.GetTransactionResultsAsync(block.BlockHash));
                var find = transactionList.Find(
                    t => t.Transaction.MethodName.Equals("ReleaseApprovedUserSmartContract"));
                releaseTransaction = find ?? releaseTransaction;
            }

            return releaseTransaction;
        }

        private bool CheckProposal(Hash proposalId)
        {
            var proposalInfo = Tester.ParliamentService.CheckProposal(proposalId);
            var checkTimes = 20;
            while (!proposalInfo.ToBeReleased && checkTimes > 0)
            {
                Thread.Sleep(1000);
                proposalInfo = Tester.ParliamentService.CheckProposal(proposalId);
                checkTimes--;
            }

            return proposalInfo.ToBeReleased;
        }

        #endregion
    }
}