param(
    [Parameter(Mandatory=$true)][string]$Package,
    [Parameter(Mandatory=$true)][string]$Handoff,
    [string]$Root='C:\Lightgun',
    [switch]$StartAfter
)
$ErrorActionPreference='Stop'
if($Root -ne 'C:\Lightgun') { throw 'Diese Einrichtungsfassung verwendet C:\Lightgun als feste Medienwurzel.' }
if(-not (Test-Path -LiteralPath (Join-Path $Handoff 'spiele.json'))) { throw 'spiele.json fehlt im Übergabeordner.' }
if(-not (Test-Path -LiteralPath $Package)) { throw 'Windows-Paket fehlt.' }
# Existing migration workers own game/configuration writes until their journals finish.
$logs=Join-Path $Root 'Setup\Logs'
$primary=Join-Path $logs 'copy-progress.json'
if(Test-Path -LiteralPath $primary) {
    $copy=Get-Content -LiteralPath $primary -Raw | ConvertFrom-Json
    if($copy.finished -lt $copy.total) { throw "Die Hauptkopie läuft noch: $($copy.finished) / $($copy.total). Bestehende Jobs weiterlaufen lassen." }
    $nasPath=Join-Path $logs 'nas-progress.json'
    if(-not (Test-Path -LiteralPath $nasPath)) { throw 'NAS-Abschluss noch nicht vorhanden.' }
    $nas=Get-Content -LiteralPath $nasPath -Raw | ConvertFrom-Json
    if($nas.phase -ne 'Finished') { throw 'Die vorhandene NAS-Prüfung läuft noch.' }
    if(-not (Test-Path -LiteralPath (Join-Path $logs 'launcher-status.json'))) { throw 'Die bestehende Starter-Konfiguration ist noch nicht abgeschlossen.' }
}
$data=Join-Path $env:LOCALAPPDATA 'ReaperArcade'
$install=Join-Path $Root 'Frontend\ReaperArcade'
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$backup=Join-Path $Root ('Setup\Backups\ReaperArcade-'+$stamp)
New-Item -ItemType Directory -Path $backup -Force | Out-Null
if(Get-Process -Name ReaperArcade -ErrorAction SilentlyContinue) { throw 'Bitte Reaper Arcade vor der Installation schließen.' }
if(Test-Path -LiteralPath $data) { Copy-Item -LiteralPath $data -Destination (Join-Path $backup 'UserData') -Recurse }
$staging=Join-Path $backup 'Extracted'
Expand-Archive -LiteralPath $Package -DestinationPath $staging
$packageRoot=Join-Path $staging 'Reaper-Arcade'
if(-not (Test-Path -LiteralPath (Join-Path $packageRoot 'ReaperArcade.exe'))) { throw 'Das Paket enthält keine ReaperArcade.exe.' }
if(Test-Path -LiteralPath $install) { Move-Item -LiteralPath $install -Destination (Join-Path $backup 'Application') }
New-Item -ItemType Directory -Path (Split-Path $install) -Force | Out-Null
Move-Item -LiteralPath $packageRoot -Destination $install
New-Item -ItemType Directory -Path $logs,(Join-Path $Root 'Media') -Force | Out-Null
$media=Get-Content -LiteralPath (Join-Path $Handoff 'medien-kopierplan.json') -Raw | ConvertFrom-Json
$mediaRows=New-Object Collections.Generic.List[object]
$mediaBytes=0L
foreach($entry in $media.files) {
    if(-not $entry.target.StartsWith((Join-Path $Root 'Media')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Medienziel liegt außerhalb des vorgesehenen Medienordners.' }
    $source=$null
    foreach($candidate in @($entry.windows_source)+@($entry.alternative_sources)) {
        if($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) { $source=$candidate; break }
    }
    try {
        if(-not $source) { throw 'Keine Medienquelle auf Windows erreichbar.' }
        $size=(Get-Item -LiteralPath $source).Length
        New-Item -ItemType Directory -Path (Split-Path $entry.target) -Force | Out-Null
        # Partial copies never become visible as a completed preview.
        $partial=$entry.target+'.reaper-partial'
        Copy-Item -LiteralPath $source -Destination $partial -Force
        if((Get-Item -LiteralPath $partial).Length -ne $size) { throw 'Mediengröße stimmt nach Kopie nicht überein.' }
        Move-Item -LiteralPath $partial -Destination $entry.target -Force
        $mediaBytes+=$size
        $mediaRows.Add(@{id=$entry.game_id;role=$entry.role;target=$entry.target;source=$source;bytes=$size;status='Copied'})
    } catch { $mediaRows.Add(@{id=$entry.game_id;role=$entry.role;target=$entry.target;status='Missing';error=$_.Exception.Message}) }
}
@{time=(Get-Date).ToString('o');bytes=$mediaBytes;rows=@($mediaRows.ToArray())} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $logs 'reaper-media.json') -Encoding UTF8
$exe=Join-Path $install 'ReaperArcade.exe'
$json=Join-Path $Handoff 'spiele.json'
$import=Start-Process -FilePath $exe -ArgumentList @('--import-collection',('"'+$json+'"')) -Wait -PassThru
if($import.ExitCode -ne 0) { throw 'Bibliotheksimport fehlgeschlagen; library-import-error.txt im Reaper-Benutzerordner prüfen. Sicherung ist vorhanden.' }
$shell=New-Object -ComObject WScript.Shell
foreach($directory in @([Environment]::GetFolderPath('Desktop'),(Join-Path ([Environment]::GetFolderPath('Programs')) 'Reaper Arcade'))) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $shortcut=$shell.CreateShortcut((Join-Path $directory 'Reaper Arcade.lnk'));$shortcut.TargetPath=$exe;$shortcut.WorkingDirectory=$install;$shortcut.Save()
}
Copy-Item -LiteralPath (Join-Path $data 'library-import-report.json') -Destination (Join-Path $logs 'reaper-games.json') -Force
$report=Get-Content -LiteralPath (Join-Path $data 'library-import-report.json') -Raw | ConvertFrom-Json
$report.rows | Select-Object @{n='Datum';e={$report.time}},id,title,platform,players,filesPresent,launchObserved,player1Verified,player2Verified,recoilVerified,returnVerified,status,@{n='OffenePunkte';e={$_.setupIssues -join ' | '}},setupNotes |
    Export-Csv -LiteralPath (Join-Path $logs 'reaper-games.csv') -Delimiter ';' -NoTypeInformation -Encoding UTF8
$result=@{time=(Get-Date).ToString('o');computer=$env:COMPUTERNAME;user=$env:USERNAME;install=$install;backup=$backup;games=$report.games;filesAvailable=$report.available;mediaCopied=@($mediaRows.ToArray() | Where-Object {$_.status -eq 'Copied'}).Count;mediaBytes=$mediaBytes;hardwareVerified=$false;gameplayVerified=$false}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logs 'reaper-install.json') -Encoding UTF8
$result
if($StartAfter) { Start-Process -FilePath $exe -WorkingDirectory $install }
