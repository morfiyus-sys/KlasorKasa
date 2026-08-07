[CmdletBinding()]
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseRoot = Join-Path $projectRoot "artifacts\release"
$publishRoot = Join-Path $releaseRoot "KlasorKasa-$Version-win-x64"
$archivePath = Join-Path $releaseRoot "KlasorKasa-$Version-win-x64.zip"
$checksumPath = Join-Path $releaseRoot "KlasorKasa-$Version-win-x64.sha256"

if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

Push-Location $projectRoot
try {
    dotnet restore KlasorKasa.sln
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore başarısız." }

    dotnet build KlasorKasa.sln -c Release -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet build başarısız." }

    dotnet run --project KlasorKasa.Tests\KlasorKasa.Tests.csproj -c Release -p:Platform=x64 --no-build
    if ($LASTEXITCODE -ne 0) { throw "Otomatik testler başarısız." }

    dotnet publish KlasorKasa.csproj -c Release -r win-x64 --self-contained true `
        /p:PublishSingleFile=true -p:Platform=x64 -p:PublishTrimmed=false `
        -p:DebugType=none -p:DebugSymbols=false -o $publishRoot
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız." }

    Copy-Item -LiteralPath README.md -Destination (Join-Path $publishRoot "README.md")
    Copy-Item -LiteralPath RELEASE_NOTES.md -Destination (Join-Path $publishRoot "RELEASE_NOTES.md")
    Copy-Item -LiteralPath LICENSE -Destination (Join-Path $publishRoot "LICENSE")

    if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
    Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $archivePath -CompressionLevel Optimal

    $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $(Split-Path -Leaf $archivePath)" | Set-Content -LiteralPath $checksumPath -Encoding ascii
    Write-Host "Paket: $archivePath"
    Write-Host "SHA-256: $hash"
}
finally {
    Pop-Location
}
