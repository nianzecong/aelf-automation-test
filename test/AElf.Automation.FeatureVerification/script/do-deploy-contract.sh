#!/bin/bash

# init params
automationDir=""
contractDir=""
contractVer=""
type=""
contractFileName=""
updateContractAddress=""
proposalId=""
proposalHash=""
testClassName="DeployContractTest"
skipParliamentChangeWhiteList="run"

# print help info and EXIT
usage() {
    echo "Usage: $0 [-a automationDir] [-c contractDir] [-v contractVer] [-t type] [-f contractFileName] [-u updateContractAddress] [-g testClassName]"
    echo
    echo " -a automationDir: The path to the automation Solution project directory."
    echo " -c contractDir: The path to the contract Solution project directory."
    echo " -v contractVer: The contract version."
    echo " -t type: The type of operation, can be either package/deploy/update/proposalDeploy/proposalUpdate/deployCodeCheck/updateCodeCheck ."
    echo " -f contractFileName: The name of the contract file. If not provided, the script will look for a DLL in the 'automationDir/bin/Debug/net6.0' directory."
    echo " -u updateContractAddress: (Optional) The contract address to update. This parameter is required when the type is 'update'."
    echo " -p proposalId: (Optional) Param is required when type is 'DeployCodeCheck' or 'UpdateCodeCheck'."
    echo " -h proposalHash: (Optional) Param is required when type is 'DeployCodeCheck' or 'UpdateCodeCheck'."
    echo " -g testClassName: (Optional) The name of the test class to run. If not provided, the script will use 'DeployContractTest' as the default."
    echo " -w skipParliamentChangeWhiteList: (Optional) whether skip ParliamentChangeWhiteList, the script will use 'run' as the default."
    exit 1
}

# show help info
if [[ $# -eq 0 || "$1" == "help" ]]; then
    usage
fi

# get params via getopts
while getopts a:c:v:t:f:u:g:w:p:h: flag
do
    case "${flag}" in
        a) automationDir=${OPTARG};;
        c) contractDir=${OPTARG};;
        v) contractVer=${OPTARG};;
        t) type=${OPTARG};;
        f) contractFileName=${OPTARG};;
        u) updateContractAddress=${OPTARG};;
        g) testClassName=${OPTARG};;
        w) skipParliamentChangeWhiteList=${OPTARG};;
        p) proposalId=${OPTARG};;
        h) proposalHash=${OPTARG};;
        *) usage ;;
    esac
done

echo "Type is: $type"
echo "ContractVer is: $contractVer"

# check params
if [[ -z $automationDir || -z $contractDir || -z $contractVer || -z $type || -z $contractFileName ]]; then
    echo "PARAM INVALID !!"
    echo "$automationDir, $contractDir, $contractVer, $type, $contractFileName"
    usage
fi

# Verify type
supported_types=("package" "deploy" "update" "proposalDeploy" "proposalUpdate" "deployCodeCheck" "updateCodeCheck")
is_valid_type=false
for valid_type in "${supported_types[@]}"
do
    if [[ "$type" == "$valid_type" ]]; then
        is_valid_type=true
        break
    fi
done
if [[ "$is_valid_type" == "false" ]]; then
    echo "NOT SUPPORT type: $type"
    usage
fi

# Verify params by type
if [[ "$type" == "update" && -z "$updateContractAddress" ]]; then
    echo "params -u (updateContractAddress) IS REQUIRED when the type is 'update'."
    usage
fi
if [[ "$type" == "deployCodeCheck" || "$type" == "updateCodeCheck"  ]]; then
  if [[ -z $proposalId ]]; then
    echo "params -p (proposalId) IS REQUIRED when the type is 'deployCodeCheck/updateCodeCheck'."
    usage
  fi
  if [[ -h $proposalHash ]]; then
    echo "params -h (proposalHash) IS REQUIRED when the type is 'deployCodeCheck/updateCodeCheck'."
    usage
  fi
fi

# CodeCheck
if [[ "$type" == "deployCodeCheck" || "$type" == "updateCodeCheck" ]]; then
    export proposalId="$proposalId"
    export proposalHash="$proposalHash"
    echo ">>> proposalId=$proposalId"
    echo ">>> proposalHash=$proposalHash"
    cd "$automationDir" || exit
fi 
if [[ "$type" == "deployCodeCheck" ]]; then
    echo ">>> START deployCodeCheck..."
    dotnet test \
            --logger:"console;verbosity=detailed" \
            --filter FullyQualifiedName=AElf.Automation.Contracts.ScenarioTest.${testClassName}.ReleaseDeployCodeCheck
    exit 0;
elif [[ "$type" == "updateCodeCheck" ]]; then
    # update
    cd "$automationDir" || exit
    echo ">>> START updateCodeCheck..."
    dotnet test \
            --logger:"console;verbosity=detailed" \
            --filter FullyQualifiedName=AElf.Automation.Contracts.ScenarioTest.${testClassName}.ReleaseUpdateCodeCheck
    exit 0;
fi


# Compile contract project
echo ">>> start build contract..."
echo ">>> contractFileName is $contractFileName"
cd "$contractDir" || exit
dotnet clean
dotnet publish --configuration Release -p:Version=${contractVer}
if [[ $? -ne 0 ]]; then
    echo "BUILD FAILED ！！！"
    exit 1
fi

if [ ! -f "${contractDir}/bin/Release/net6.0/${contractFileName}.dll.patched" ]; then
    echo "The build file DOES NOT EXIST : ${contractDir}/bin/Release/net6.0/${contractFileName}.dll.patched"
    exit 1
fi

deployFileName=${contractFileName}-${contractVer}

echo ">>> copy build dll file to $automationDir/bin/Debug/net6.0/"
cp "${contractDir}"/bin/Release/net6.0/"${contractFileName}".dll.patched "${automationDir}"/bin/Debug/net6.0/${deployFileName}.dll

echo ">>> copy build dll file to ~/.local/share/aelf/contracts"
cp "${contractDir}"/bin/Release/net6.0/${contractFileName}.dll.patched ~/.local/share/aelf/contracts/${deployFileName}.dll

echo ">>> build success"

if [[ "$type" == "package" ]]; then
  echo ">>> open ~/.local/share/aelf/contracts"
  open ~/.local/share/aelf/contracts
  exit 0;
fi


# set environment variable used by test methods
export contractFileName="$deployFileName"
export updateContractAddress="$updateContractAddress"


# Enter the automationDir project directory and execute different unit tests based on the input variable type.
echo ""
echo ""
echo ">>> deploy to node via aelf-automation-test ..."
echo ">>> automationDir is $automationDir"

cd "$automationDir" || exit

if [[ "$skipParliamentChangeWhiteList" != "skip" ]]; then
  echo ">>> set ParliamentChangeWhiteList"
  dotnet test \
          --logger:"console;verbosity=detailed" \
          --filter FullyQualifiedName=AElf.Automation.Contracts.ScenarioTest.${testClassName}.ParliamentChangeWhiteList
fi

# new deploy
if [[ "$type" == "deploy" ]]; then
    echo ">>> deploy new contract $contractFileName"
    dotnet test \
            --logger:"console;verbosity=detailed" \
            --filter FullyQualifiedName=AElf.Automation.Contracts.ScenarioTest.${testClassName}.DeployUserSmartContract
                
# update
elif [[ "$type" == "update" ]]; then
    echo ">>> update contract $updateContractAddress"
    dotnet test \
            --logger:"console;verbosity=detailed" \
            --filter FullyQualifiedName=AElf.Automation.Contracts.ScenarioTest.${testClassName}.UpdateUserSmartContract

# proposalDeploy
elif [[ "$type" == "proposalDeploy" ]]; then
    echo ">>> proposalDeploy new contract $contractFileName"
    dotnet test \
            --logger:"console;verbosity=detailed" \
            --filter FullyQualifiedName=AElf.Automation.Contracts.ScenarioTest.${testClassName}.ProposalDeploy_MinerProposalContract_Success

# proposalUpdate
elif [[ "$type" == "proposalUpdate" ]]; then
    echo ">>> proposalUpdate contract $updateContractAddress"
    dotnet test \
            --logger:"console;verbosity=detailed" \
            --filter FullyQualifiedName=AElf.Automation.Contracts.ScenarioTest.${testClassName}.ProposalUpdate_MinerProposalUpdateContract_Success

else
    echo "UNKNOWN type: $type"
    usage
fi