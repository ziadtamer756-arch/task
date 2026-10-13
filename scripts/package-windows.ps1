$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet run --project tests/SenderPro.Checks/SenderPro.Checks.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Application checks failed.' }
    dotnet publish src/SenderPro.Windows/SenderPro.Windows.csproj -c Release -r win-x64 --self-contained true -o artifacts/windows-x64
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
    Copy-Item docs/windows-implementation.md artifacts/windows-x64/README-ar.md
    Compress-Archive -Path artifacts/windows-x64/* -DestinationPath artifacts/SenderPro-Windows-x64.zip -Force
    Write-Host 'Created artifacts/SenderPro-Windows-x64.zip'
}
finally { Pop-Location }
