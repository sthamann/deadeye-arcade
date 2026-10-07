param([string]$Version, [string]$Compiler)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $Version){ [xml]$project=Get-Content (Join-Path $root 'src/Reaper.Windows/Reaper.Windows.csproj'); $Version=$project.Project.PropertyGroup.Version }
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid version'}
if(-not $Compiler){$Compiler=(Get-Command makensis.exe -ErrorAction SilentlyContinue).Source}
if(-not $Compiler){$Compiler='C:\Program Files (x86)\NSIS\makensis.exe'}
$payload=Join-Path $root 'release/windows-x64'
$bootstrap=Join-Path $root 'release/MicrosoftEdgeWebview2Setup.exe'
Invoke-WebRequest -UseBasicParsing 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $bootstrap
$signature=Get-AuthenticodeSignature $bootstrap
if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation'){throw 'Invalid Microsoft bootstrapper signature'}
$manifest=Join-Path $root 'release/uninstall-files.nsh'
$files=Get-ChildItem $payload -File -Recurse
$lines=@($files | ForEach-Object { $relative=$_.FullName.Substring($payload.Length+1); 'Delete "$INSTDIR\'+$relative+'"' })
$lines+=@(Get-ChildItem $payload -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object { 'RMDir "$INSTDIR\'+$_.FullName.Substring($payload.Length+1)+'"' })
[IO.File]::WriteAllLines($manifest,$lines,[Text.UTF8Encoding]::new($false))
$output=Join-Path $root "release/Reaper-Arcade-$Version-Setup-x64.exe"
& $Compiler "/DVERSION=$Version" "/DPAYLOAD=$payload" "/DOUTPUT=$output" "/DBOOTSTRAPPER=$bootstrap" "/DUNINSTALL_FILES=$manifest" (Join-Path $root 'installer/reaper.nsi')
if($LASTEXITCODE -ne 0){throw 'Installer build failed'}
Get-ChildItem (Join-Path $root "release/Reaper-Arcade-$Version-*") -File | ForEach-Object { ((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()+'  '+$_.Name) } | Set-Content (Join-Path $root "release/SHA256SUMS-$Version.txt") -Encoding ascii
