# Publica o Draw the Odds no itch.io.
#
# Uso (no PowerShell, na pasta do projeto):
#   .\Tools\publicar_itch.ps1 -SoZip                       -> só gera os zips corretos em Builds\
#   .\Tools\publicar_itch.ps1 -Alvo "usuario/draw-the-odds" -> gera os zips e envia pelo butler
#
# Por que este script existe: o Compress-Archive do Windows PowerShell 5.1 grava os
# caminhos do zip com "\" (ex.: Build\WebGL.loader.js). O itch.io extrai no Linux, onde
# "\" vira parte do nome do arquivo, e o index.html não acha a pasta Build -> o jogo não abre.
# Aqui os zips são montados à mão com "/" e o butler envia as pastas direto (sem zip).

param(
    [string]$Alvo = "",       # "seu-usuario-itch/slug-do-jogo" (o final da URL da página)
    [switch]$SoZip,           # só gera os zips, não envia
    [switch]$SemWindows       # ignora a build de Windows
)

$ErrorActionPreference = "Stop"
$raiz   = Split-Path -Parent $PSScriptRoot
$builds = Join-Path $raiz "Builds"
$web    = Join-Path $builds "WebGL"
$win    = Join-Path $builds "Windows"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Criar-Zip([string]$pasta, [string]$destino) {
    if (Test-Path $destino) { Remove-Item $destino -Force }
    $zip = [IO.Compression.ZipFile]::Open($destino, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $base = (Resolve-Path $pasta).Path.TrimEnd('\') + '\'
        Get-ChildItem $pasta -Recurse -File |
            Where-Object { $_.FullName -notmatch 'DoNotShip' } |
            ForEach-Object {
                # caminho relativo com "/" -> é isso que o itch precisa
                $nome = $_.FullName.Substring($base.Length).Replace('\', '/')
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $nome, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
    } finally { $zip.Dispose() }
    $mb = [math]::Round((Get-Item $destino).Length / 1MB, 1)
    Write-Host "Zip criado: $destino ($mb MB)" -ForegroundColor Green
}

# ---------- 1) Checagens ----------
if (-not (Test-Path (Join-Path $web "index.html"))) {
    throw "Não achei Builds\WebGL\index.html. Rode no Unity: Draw the Odds > Build WebGL."
}
$temWin = (-not $SemWindows) -and (Test-Path $win) -and (Get-ChildItem $win -Filter *.exe -ErrorAction SilentlyContinue)

# ---------- 2) Zips (para upload manual pelo site, se preferir) ----------
Criar-Zip $web (Join-Path $builds "DrawTheOdds_Web.zip")
if ($temWin) { Criar-Zip $win (Join-Path $builds "DrawTheOdds_Windows.zip") }

if ($SoZip) { Write-Host "Pronto (sem envio)."; return }
if (-not $Alvo) { throw "Informe -Alvo 'usuario/slug-do-jogo' para enviar pelo butler (ou use -SoZip)." }

# ---------- 3) butler (baixa da fonte oficial do itch na primeira vez) ----------
$butlerDir = Join-Path $env:LOCALAPPDATA "butler"
$butler    = Join-Path $butlerDir "butler.exe"
if (-not (Test-Path $butler)) {
    Write-Host "Baixando o butler (broth.itch.zone, oficial do itch.io)..."
    New-Item -ItemType Directory -Force $butlerDir | Out-Null
    $tmp = Join-Path $env:TEMP "butler.zip"
    Invoke-WebRequest "https://broth.itch.zone/butler/windows-amd64/LATEST/archive/default" -OutFile $tmp
    Expand-Archive $tmp -DestinationPath $butlerDir -Force
    Remove-Item $tmp
}
& $butler -V

# login: na primeira vez abre o navegador para você autorizar (fica salvo depois)
& $butler login
if ($LASTEXITCODE -ne 0) { throw "Login do butler falhou." }

# canal "html5" = jogável no navegador; "windows" = download
& $butler push $web "$($Alvo):html5"
if ($LASTEXITCODE -ne 0) { throw "Envio da build web falhou." }
if ($temWin) {
    & $butler push $win "$($Alvo):windows" --ignore "*DoNotShip*"
    if ($LASTEXITCODE -ne 0) { throw "Envio da build Windows falhou." }
}

& $butler status $Alvo
Write-Host ""
Write-Host "Enviado! Agora no site do itch (Edit game):" -ForegroundColor Cyan
Write-Host " - Kind of project: HTML"
Write-Host " - No upload 'html5', marque 'This file will be played in the browser'"
Write-Host " - Embed 960x600 + Fullscreen button, salve e teste a página"
