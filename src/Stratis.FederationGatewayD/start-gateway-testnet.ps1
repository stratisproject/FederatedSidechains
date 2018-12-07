
#############################
#    UPDATE THESE VALUES    #
#############################
$git_repos_path = "C:\Users\matthieu\source\repos"
$root_datadir = "C:\Users\matthieu\AppData\Roaming\StratisNode"
$path_to_federationgatewayd = "$git_repos_path\FederatedSidechains\src\Stratis.FederationGatewayD"
$path_to_mining_key_dat_file = "$git_repos_path\secrets\federationKey.dat"
$multisig_public_key = "03b824a9500f17c9fe7a4e3bb660e38b97b66ed3c78749146f2f31c06569cf905c"
$mining_public_key = "0248de019680c6f18e434547c8c9d48965b656b8e5e70c5a5564cfb1270db79a11"
$nickname = "matthieu"
######################################
#    UPDATE THIS BUT DO NOT SHARE    #
######################################
$multisig_mnemonic = "please change that to the keywords generated when using federation setup tool"
$multisig_password = "dis_is hard2 guess INNIT?"
$mining_wallet_password = "dis_is quite Tricky 2 honestly..."

# Create the folders in case they don't exist.
New-Item -ItemType directory -Force -Path $root_datadir
New-Item -ItemType directory -Force -Path $root_datadir\gateway\stratis\StratisTest
New-Item -ItemType directory -Force -Path $root_datadir\gateway\poa\FederatedPegTest


# Copy the blockchain data from a current, ideally up-to-date, Stratis Testnet folder.
If ((Test-Path $env:APPDATA\StratisNode\stratis\StratisTest) -And -Not (Test-Path $root_datadir\gateway\stratis\StratisTest\blocks)) {
    $destination = "$root_datadir\gateway\stratis\StratisTest"
    Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\blocks -Recurse -Destination $destination
    Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\chain -Recurse -Destination $destination
    Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\coinview -Recurse -Destination $destination
    Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\finalizedBlock -Recurse -Destination $destination
    Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\provenheaders -Recurse -Destination $destination
}

Copy-Item $path_to_mining_key_dat_file -Destination $root_datadir\gateway\poa\FederatedPegTest\

# FEDERATION DETAILS
# Redeem script: 3 03eaec65a70a9164579dcf0ab9d66a821eeb1a597412aa7d28c48d7bb708deebc3 026b7b9092828f3bf9e73995bfa3547c3bcd3814f8101fac626b8349d9a6f0e534 0396f7825142a906191cf394c3b4f2fd66e1244f850eb77aff3923ef125c234ffa 03b824a9500f17c9fe7a4e3bb660e38b97b66ed3c78749146f2f31c06569cf905c 0319a589292010a61ab6da4926f1a91be7cd3791e81e5a71cd7beac157c55ff9f4 5 OP_CHECKMULTISIG
# Sidechan P2SH: OP_HASH160 812231abd79e116f5c6ff7455a047e6bccd480f7 OP_EQUAL
# Sidechain Multisig address: pHKNLJ2eoeR8owR4hMpvzXTU5zF7BG4ALC
# Mainchain P2SH: OP_HASH160 812231abd79e116f5c6ff7455a047e6bccd480f7 OP_EQUAL
# Mainchain Multisig address: 2N5227saQtu2MRBRudW97JRxcoqbdJ9UPtA

$mainchain_federationips = "13.70.81.5:26178,109.150.17.24:26178,80.200.67.186:26178,104.211.178.243:26178,51.145.3.121:26178"
$sidechain_federationips = "13.70.81.5:26179,109.150.17.24:26179,80.200.67.186:26179,104.211.178.243:26179,51.145.3.121:26179"
$redeemscript = "3 03eaec65a70a9164579dcf0ab9d66a821eeb1a597412aa7d28c48d7bb708deebc3 026b7b9092828f3bf9e73995bfa3547c3bcd3814f8101fac626b8349d9a6f0e534 0396f7825142a906191cf394c3b4f2fd66e1244f850eb77aff3923ef125c234ffa 03b824a9500f17c9fe7a4e3bb660e38b97b66ed3c78749146f2f31c06569cf905c 0319a589292010a61ab6da4926f1a91be7cd3791e81e5a71cd7beac157c55ff9f4 5 OP_CHECKMULTISIG"

# The interval between starting the networks run, in seconds.
$interval_time = 5
$long_interval_time = 10

$agent_prefix = $nickname + "-" + $mining_public_key.Substring(0,5)

cd $path_to_federationgatewayd
# Federation member main and side
Write-Host "Starting mainchain gateway node"
start-process cmd -ArgumentList "/k color 0E && dotnet run --no-build -mainchain -testnet -agentprefix=gtway-main-$agent_prefix -datadir=$root_datadir\gateway -port=26178 -apiport=38221 -counterchainapiport=38222 -federationips=$mainchain_federationips -redeemscript=""$redeemscript"" -publickey=$gateway1_public_key -mincoinmaturity=1 -mindepositconfirmations=1 -addnode=13.70.81.5 -addnode=52.151.76.252 -whitelist=52.151.76.252 -gateway=1"
timeout $long_interval_time
Write-Host "Starting sidechain gateway node"
start-process cmd -ArgumentList "/k color 0E && dotnet run --no-build -sidechain -testnet -agentprefix=gtway-side-$agent_prefix -datadir=$root_datadir\gateway -port=26179 -apiport=38222 -counterchainapiport=38221 -federationips=$sidechain_federationips -redeemscript=""$redeemscript"" -publickey=$gateway1_public_key -mincoinmaturity=1 -mindepositconfirmations=1 -txindex=1"
timeout $long_interval_time


######### API Queries to enable federation wallets ###########
# mainchain
Write-Host "Enabling multisig wallet on main chain"
$params = @{ "mnemonic" = $multisig_mnemonic; "password" = $multisig_password }
Invoke-WebRequest -Uri http://localhost:38221/api/FederationWallet/import-key -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"
timeout $interval_time
$params = @{ "password" = $multisig_password }
Invoke-WebRequest -Uri http://localhost:38221/api/FederationWallet/enable-federation -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"
timeout $interval_time

# sidechain
Write-Host "Enabling multisig wallet on side chain"
$params = @{ "mnemonic" = $multisig_mnemonic; "password" = $multisig_password }
Invoke-WebRequest -Uri http://localhost:38222/api/FederationWallet/import-key -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"
timeout $interval_time
$params = @{ "password" = $multisig_password }
Invoke-WebRequest -Uri http://localhost:38222/api/FederationWallet/enable-federation -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"
timeout $interval_time

# create POA wallet if needed
$params = @{ "name" = "poa-rewards"; "password" = $mining_wallet_password }
Try{
    Write-Host "Loading wallet for poa-rewards"
    Invoke-WebRequest -Uri http://localhost:38222/api/Wallet/load -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"
}
Catch {

    $ErrorMessage = $_.Exception.Message
    If ($ErrorMessage.Contains("404")) 
    {
        Write-Host "Creating wallet for poa-rewards"
        $params = @{ "name" = "poa-rewards"; "password" = $mining_wallet_password; "passphrase" = $mining_wallet_password; "mnemonic" = $multisig_mnemonic;  }
        Invoke-WebRequest -Uri http://localhost:38222/api/Wallet/create -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"
    }
}