# on-key deploy your contract with do-deploy-contract.sh

Put your keys `.json `file to `AElf.Automation.FeatureVerification/aelf/keys/`.

Copy `src/AElfChain.Common/config.nodes.json` to `AElf.Automation.FeatureVerification/config/`.

All files with `.dev.json` or `.dev.sh` suffix and `aelf/keys/*` was added in `.gitignore` .

The file tree may like this:

```shell
AElf.Automation.FeatureVerification
   |- aelf
   |    |- keys
   |        |-your-init-account-address.json
   |        |-your-creator-account-address.json
   |        |-your-member-account-address.json
   |        |-your-author-account-address.json
   |        |-your-outherAccount-account-address.json
   |- config
        |- node.local.dev.json

```

usage:

```shell
> sh do-deploy-contract.sh 
Usage: do-deploy-contract.sh [-a automationDir] [-c contractDir] [-v contractVer] [-t type] [-f contractFileName] [-u updateContractAddress] [-g testClassName]

 -a automationDir: The path to the automation Solution project directory.
 -c contractDir: The path to the contract Solution project directory.
 -v contractVer: The contract version.
 -t type: The type of operation, can be either package/deploy/update/proposalDeploy/proposalUpdate/deployCodeCheck/updateCodeCheck .
 -f contractFileName: The name of the contract file. If not provided, the script will look for a DLL in the 'automationDir/bin/Debug/net6.0' directory.
 -u updateContractAddress: (Optional) The contract address to update. This parameter is required when the type is 'update'.
 -p proposalId: (Optional) Param is required when type is 'DeployCodeCheck' or 'UpdateCodeCheck'.
 -h proposalHash: (Optional) Param is required when type is 'DeployCodeCheck' or 'UpdateCodeCheck'.
 -g testClassName: (Optional) The name of the test class to run. If not provided, the script will use 'DeployContractTest' as the default.
 -w skipParliamentChangeWhiteList: (Optional) Set "skip" to skip ParliamentChangeWhiteList, the script will use 'run' as the default.

```

Write a new shell script `deploy2mainChain-testNode-YourContract.dev.sh` in `./script` forder that sets environment variables 
and invokes `do-deploy-contract.sh` to deploy your contract to aelf block chain.

```shell
#!/bin/bash

export MainRpcUrl="http://127.0.0.1:8001"
export SideRpcUrl="http://127.0.0.1:8001"
export SideRpcUrl2="http://127.0.0.1:8001"
export Type="Main"
export NodesConfig="nodes.local.dev"

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
	# set "skip" to skip ParliamentChangeWhiteList
	-w run \
	# contract version
	-v 1.0.0 \
	# "deploy" or "update"
	-t deploy
```

deploy-new-contract example:

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
	-c /Users/yourname/github/AElf-contract-project/AElf.Solution.YourTestContract \
	-f AElf.YourTestContract \
	-w run \
	-v 1.0.0 \
	-t deploy
	
```

update-old-contract example:

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
	-w run \
	# set type "update"
	-t update \
	# contractAddress required
	-u 2LUmicHyH4RXrMjG4beDwuDsiWJESyLkgkwPdGTR8kahRzq5XS \
	# set a new version
	-v 1.0.1
	
```
