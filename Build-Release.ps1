param([Parameter(Mandatory=$true)][string]$IsccPath)
$ErrorActionPreference='Stop'
$version=(Get-Content (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid VERSION'}
$payload=Join-Path $PSScriptRoot 'dist'
$release=Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $payload,$release -Force | Out-Null
foreach($project in @('src\AudioSwitch.csproj','tray\AudioSwitchTray.csproj')){
    & dotnet publish (Join-Path $PSScriptRoot $project) -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true "-p:Version=$version" -o $payload
    if($LASTEXITCODE -ne 0){throw "Publish failed: $project"}
}
$names=@('AudioSwitch.exe','AudioSwitchTray.exe','README.txt','THIRD-PARTY-LICENSE.txt')
Copy-Item (Join-Path $PSScriptRoot 'README.txt'),(Join-Path $PSScriptRoot 'THIRD-PARTY-LICENSE.txt') $payload -Force
$zip=Join-Path $release "AudioSwitch-v$version-win-x64.zip"
Compress-Archive -Path ($names | ForEach-Object {Join-Path $payload $_}) -DestinationPath $zip -Force
& $IsccPath "/DAppVersion=$version" "/DPayloadDir=$payload" "/DReleaseDir=$release" (Join-Path $PSScriptRoot 'installer\Setup.iss')
if($LASTEXITCODE -ne 0){throw 'Setup compilation failed'}
$setup=Join-Path $release "AudioSwitch-Setup-$version-win-x64.exe"
@($setup,$zip) | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_ -Algorithm SHA256).Hash.ToLowerInvariant(),[IO.Path]::GetFileName($_) } | Set-Content (Join-Path $release 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Release artifacts: $release"
