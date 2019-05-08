using AElf.Automation.Common.Contracts;
using AElf.Automation.Common.Helpers;

namespace AElf.Automation.EconomicSystem.Tests
{
    public class ContractServices
    {
        public readonly IApiHelper ApiHelper;
        public GenesisContract GenesisService { get; set; }
        public TokenContract TokenService { get; set; }
        public TokenConverterContract TokenConverterService { get; set; }
        public VoteContract VoteService { get; set; }
        public ProfitContract ProfitService { get; set; }
        public ElectionContract ElectionService { get; set; }
        public ConsensusContract ConsensusService { get; set; }
        
        public string CallAddress { get; set; }
        public Address CallAccount { get; set; }
        
        public ContractServices(IApiHelper apiHelper, string callAddress)
        {
            ApiHelper = apiHelper;
            CallAddress = callAddress;
            CallAccount = Address.Parse(callAddress);
            
            //connect chain
            ConnectionChain();
            
            //get all contract services
            GetAllContractServices();
        }

        public void GetAllContractServices()
        {
            GenesisService = GenesisContract.GetGenesisContract(ApiHelper, CallAddress);
            
            //TokenService contract
            var tokenAddress = GenesisService.GetContractAddressByName(NameProvider.TokenName);
            TokenService = new TokenContract(ApiHelper, CallAddress, tokenAddress.GetFormatted());

            //TokenConverter contract
            //var converterAddress = GenesisService.GetContractAddressByName(NameProvider.TokenConverterName);
            //TokenConverterService = new TokenConverterContract(ApiHelper, CallAddress, converterAddress.GetFormatted());

            //ProfitService contract
            var profitAddress = GenesisService.GetContractAddressByName(NameProvider.ProfitName);
            ProfitService = new ProfitContract(ApiHelper, CallAddress, profitAddress.GetFormatted());

            //VoteService contract
            var voteAddress = GenesisService.GetContractAddressByName(NameProvider.VoteSystemName);
            VoteService = new VoteContract(ApiHelper, CallAddress, voteAddress.GetFormatted());

            //ElectionService contract
            var electionAddress = GenesisService.GetContractAddressByName(NameProvider.ElectionName);
            ElectionService = new ElectionContract(ApiHelper, CallAddress, electionAddress.GetFormatted());

            //Consensus contract
            var consensusAddress = GenesisService.GetContractAddressByName(NameProvider.ElectionName);
            ConsensusService = new ConsensusContract(ApiHelper, CallAddress, consensusAddress.GetFormatted());
        }
        private void ConnectionChain()
        {
            var ci = new CommandInfo(ApiMethods.GetChainInformation);
            ApiHelper.GetChainInformation(ci);
        }
    }
}