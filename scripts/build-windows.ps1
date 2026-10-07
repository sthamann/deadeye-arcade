$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    Push-Location ui
    try { npm ci; if ($LASTEXITCODE -ne 0) { throw 'Abhängigkeiten konnten nicht geladen werden.' }; npm run build; if ($LASTEXITCODE -ne 0) { throw 'Oberfläche konnte nicht gebaut werden.' } }
    finally { Pop-Location }
    dotnet run --project src/Reaper.Checks -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Kernprüfungen fehlgeschlagen.' }
    dotnet publish src/Reaper.Windows -c Release -r win-x64 --self-contained true -o release/windows-x64
    if ($LASTEXITCODE -ne 0) { throw 'Windows-Build fehlgeschlagen.' }
    Copy-Item README.md release/windows-x64/START-HIER.md -Force
    Copy-Item LICENSE release/windows-x64/REAPER-LICENSE.txt -Force
    Copy-Item THIRD-PARTY.md release/windows-x64/THIRD-PARTY.md -Force
    New-Item -ItemType Directory -Path release/windows-x64/licenses -Force | Out-Null
    Copy-Item licenses/* release/windows-x64/licenses -Force
    if(Test-Path release/windows-x64/docs) { Remove-Item release/windows-x64/docs -Recurse -Force }
    Copy-Item docs release/windows-x64/docs -Recurse -Force
    Copy-Item scripts/install-collection.ps1 release/windows-x64/install-collection.ps1 -Force
    $staging=Join-Path $projectRoot 'release/package-0.3'
    if(Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Copy-Item release/windows-x64 -Destination (Join-Path $staging 'Reaper-Arcade') -Recurse
    Compress-Archive -Path (Join-Path $staging 'Reaper-Arcade') -DestinationPath release/Reaper-Arcade-0.3.1-Windows-x64.zip -Force
}
finally { Pop-Location }
