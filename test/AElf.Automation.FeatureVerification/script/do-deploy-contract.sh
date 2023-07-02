#!/bin/bash

# init params
automationDir=""
contractDir=""
contractVer=""
type=""
contractFileName=""
updateContractAddress=""
testClassName="CommonContractTest"

# print help info and EXIT
usage() {
    echo "Usage: $0 [-a automationDir] [-c contractDir] [-v contractVer] [-t type] [-f contractFileName] [-u updateContractAddress] [-g testClassName]"
    echo
    echo " -a automationDir: The path to the automation directory."
    echo " -c contractDir: The path to the contract directory."
    echo " -v contractVer: The contract version."
    echo " -t type: The type of operation, can be either 'deploy' or 'update'."
    echo " -f contractFileName: The name of the contract file. If not provided, the script will look for a DLL in the 'automationDir/bin/Debug/net6.0' directory."
    echo " -u updateContractAddress: (Optional) The contract address to update. This parameter is required when the type is 'update'."
    echo " -g testClassName: (Optional) The name of the test class to run. If not provided, the script will use 'CommonContractTest' as the default."
    exit 1
}

# show help info
if [[ $# -eq 0 || "$1" == "help" ]]; then
    usage
fi

# get params via getopts
while getopts a:c:v:t:f:u:g: flag
do
    case "${flag}" in
        a) automationDir=${OPTARG};;
        c) contractDir=${OPTARG};;
        v) contractVer=${OPTARG};;
        t) type=${OPTARG};;
        f) contractFileName=${OPTARG};;
        u) updateContractAddress=${OPTARG};;
        g) testClassName=${OPTARG};;
        *) usage ;;
    esac
done

# check params
if [[ -z $automationDir || -z $contractDir || -z $contractVer || -z $type || -z $contractFileName ]]; then
    echo "PARAM INVALID !!"
    usage
fi
if [[ "$type" == "update" && -z "$updateContractAddress" ]]; then
    echo "params -u (updateContractAddress) IS REQUIRED when the type is 'update'."
    usage
fi

# set environment variable used by test methods
deployFileName=${contractFileName}-${contractVer}
export contractFileName="$deployFileName"
export updateContractAddress="$updateContractAddress"

# compile contract project
echo ">>> start build contract..."
echo ">>> deploy to ${publishDir} ..."
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

echo ">>> copy build dll file to $publishDir"
cp ${contractDir}/bin/Release/net6.0/${contractFileName}.dll.patched ${automationDir}/bin/Debug/net6.0/${deployFileName}.dll

echo ">>> copy build dll file to ~/.local/share/aelf/contracts"
cp ${contractDir}/bin/Release/net6.0/${contractFileName}.dll.patched ~/.local/share/aelf/contracts/${deployFileName}.dll

echo ">>> build success"

# Enter the automationDir project directory and execute different unit tests based on the input variable type.
echo ""
echo ""
echo ">>> deploy to node via automation-test ..."
echo ">>> automationDir is $automationDir"

cd "$automationDir" || exit

if [[ "$type" == "deploy" ]]; then
    # new deploy
    dotnet test \
            --logger:"console;verbosity=detailed" \
            --filter FullyQualifiedName=AElf.Automation.Contracts.ScenarioTest.${testClassName}.DeployUserSmartContract
elif [[ "$type" == "update" ]]; then
    # update
    dotnet test \
            --filter FullyQualifiedName=AElf.Automation.Contracts.ScenarioTest.${testClassName}.DeployUserSmartContract
else
    echo "UNKNOWN type: $type"
    usage
fi