# on-key deploy your contract with do-deploy-contract.sh


usage: 

```shell
> sh do-deploy.sh 
Usage: do-deploy.sh [-a automationDir] [-c contractDir] [-v contractVer] [-t type] [-f contractFileName] [-u updateContractAddress] [-g testClassName]

 -a automationDir: The path to the automation directory.
 -c contractDir: The path to the contract directory.
 -v contractVer: The contract version.
 -t type: The type of operation, can be either 'deploy' or 'update'.
 -f contractFileName: The name of the contract file. If not provided, the script will look for a DLL in the 'automationDir/bin/Debug/net6.0' directory.
 -u updateContractAddress: (Optional) The contract address to update. This parameter is required when the type is 'update'.
 -g testClassName: (Optional) The name of the test class to run. If not provided, the script will use 'GenesisContractTest' as the default.
```

Write a new shell script `deploy-testContract-mainChain-test1.sh` that sets environment variables 
and invokes `do-deploy-contract.sh` to deploy your contract to aelf block chain.

```shell
#!/bin/bash

export MainRpcUrl="http://127.0.0.1:8001"
export SideRpcUrl="http://127.0.0.1:8001"
export SideRpcUrl2="http://127.0.0.1:8001"
export Type="Main"

export InitAccount="your-init-account-address"
export Creator="your-creator-account-address"
export Member="your-member-account-address"
export OtherAccount="your-outherAccount-account-address"
export Author="your-author-account-address"

./do-deploy-contract.sh \
    # this aelf-automation-test solution project path
	-a /path/to/your/aelf-automation-test/test/AElf.Automation.FeatureVerification \
	# your contract solution project path
	-c /path/to/your/contract/project/path \
	# contract publish file name without dll suffix
	-f YourContractBuildName-WithOut-dll-suffix \
	# contract version
	-v 1.0.0 \
	# "deploy" or "update"
	-t deploy
```

e.g:

```shell
#!/bin/bash

export MainRpcUrl="http://127.0.0.1:8001"
export SideRpcUrl="http://127.0.0.1:8001"
export SideRpcUrl2="http://127.0.0.1:8001"
export Type="Main"

export InitAccount="your-init-account-address"
export Creator="your-creator-account-address"
export Member="your-member-account-address"
export OtherAccount="your-outherAccount-account-address"
export Author="your-author-account-address"

./do-deploy-contract.sh \
	-a /Users/yourname/github/aelf-automation-test/test/AElf.Automation.FeatureVerification \
	-c /Users/yourname/github/AElf-test/AElf.MyTestContract \
	-f AElf.MyTestContract \
	-v 1.0.0 \
	-t deploy
```
