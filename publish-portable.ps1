# =====================================================================
#  Programació Docent — Script de compilació portable (Windows x64)
# =====================================================================
#  Genera una carpeta portable AUTOCONTINGUDA (no cal .NET instal·lat)
#  i l'empaqueta en un ZIP llest per copiar a l'equip del professor.
#
#  IMPORTANT (equips capats):
#   - NO fem servir PublishSingleFile: així les DLL natives (SkiaSharp,
#     e_sqlite3) queden AL COSTAT de l'exe i no s'extreuen a %TEMP%,
#     cosa que AppLocker sol bloquejar.
#   - NO fem servir PublishTrimmed: Avalonia i SQLite usen reflexió i el
#     trimming pot trencar l'arrencada.
#
#  Ús:  pwsh ./publish-portable.ps1
# =====================================================================

$ErrorActionPreference = "Stop"

$Projecte   = Join-Path $PSScriptRoot "src/ProgramacioDocent.csproj"
$Runtime    = "win-x64"
$Config     = "Release"
$SortidaDir = Join-Path $PSScriptRoot "publish/ProgramacioDocent"
$ZipFinal   = Join-Path $PSScriptRoot "publish/ProgramacioDocent-win-x64.zip"

Write-Host "==> Netejant sortida anterior..." -ForegroundColor Cyan
if (Test-Path $SortidaDir) { Remove-Item $SortidaDir -Recurse -Force }
if (Test-Path $ZipFinal)   { Remove-Item $ZipFinal -Force }

Write-Host "==> Compilant ($Runtime, autocontingut)..." -ForegroundColor Cyan
dotnet publish $Projecte `
    -c $Config `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:PublishReadyToRun=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $SortidaDir

if ($LASTEXITCODE -ne 0) { throw "La compilació ha fallat." }

# Nota d'ús ràpid dins la carpeta portable.
$Llegeix = @"
PROGRAMACIÓ DOCENT — Aplicació portable

1. Copia TOTA aquesta carpeta a l'equip (per exemple, a l'Escriptori o a una unitat USB).
2. Executa 'ProgramacioDocent.exe'.
3. No cal instal·lar res. Les dades es desen a la subcarpeta 'dades'
   (o, si la carpeta és de només lectura, a %LOCALAPPDATA%\ProgramacioDocent).

Si Windows mostra un avís de SmartScreen: 'Més informació' > 'Executa igualment'.
Si l'antivirus el bloqueja, l'equip d'informàtica del centre ha d'afegir-lo a la
llista blanca (l'aplicació no fa cap connexió a Internet).
"@
Set-Content -Path (Join-Path $SortidaDir "LLEGEIX-ME.txt") -Value $Llegeix -Encoding UTF8

Write-Host "==> Empaquetant en ZIP..." -ForegroundColor Cyan
Compress-Archive -Path $SortidaDir -DestinationPath $ZipFinal -Force

$mida = [math]::Round((Get-Item $ZipFinal).Length / 1MB, 1)
Write-Host "==> Fet. ZIP generat: $ZipFinal ($mida MB)" -ForegroundColor Green
Write-Host "    Executable: $SortidaDir/ProgramacioDocent.exe" -ForegroundColor Green
