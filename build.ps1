$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Compilando Batuja...                   " -ForegroundColor Cyan
Write-Host "  Monitor de Dispositivos sem Fio        " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $csc)) {
    Write-Error "Compilador C# (csc.exe) não encontrado em C:\Windows\Microsoft.NET!"
    exit 1
}

$outputExe = Join-Path $PSScriptRoot "Batuja.exe"
$srcFiles = Get-ChildItem -Path (Join-Path $PSScriptRoot "src") -Filter "*.cs" -Recurse | Select-Object -ExpandProperty FullName

# ── Gera o ícone do aplicativo (mouse azul claro + cinza) ─────────────────
$iconPath = Join-Path $PSScriptRoot "app.ico"
try {
    $iconSrc = Join-Path $PSScriptRoot "src\UI\AppIconRenderer.cs"
    Add-Type -Path $iconSrc -ReferencedAssemblies System.Drawing
    [OmniBattery.UI.AppIconRenderer]::SaveIco($iconPath)
    Write-Host "Ícone gerado: app.ico" -ForegroundColor Green
} catch {
    Write-Host "Aviso: não foi possível gerar app.ico ($($_.Exception.Message))" -ForegroundColor Yellow
}

$references = @(
    "System.dll",
    "System.Core.dll",
    "System.Drawing.dll",
    "System.Windows.Forms.dll"
)

$refArgs = $references | ForEach-Object { "/r:$_" }

$manifest = Join-Path $PSScriptRoot "app.manifest"

$args = @(
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/win32manifest:$manifest",
    "/out:$outputExe"
)
if (Test-Path $iconPath) { $args += "/win32icon:$iconPath" }
$args = $args + $refArgs + $srcFiles

Write-Host "Arquivos de código-fonte encontrados: $($srcFiles.Count)" -ForegroundColor Green
Write-Host "Iniciando compilação nativa com .NET Framework..." -ForegroundColor Yellow

& $csc $args

if ($LASTEXITCODE -eq 0 -and (Test-Path $outputExe)) {
    $sizeKb = [Math]::Round((Get-Item $outputExe).Length / 1KB, 2)
    Write-Host "=========================================" -ForegroundColor Green
    Write-Host "  COMPILAÇÃO CONCLUÍDA COM SUCESSO!      " -ForegroundColor Green
    Write-Host "  Executável gerado: Batuja.exe          " -ForegroundColor Green
    Write-Host "  Tamanho do binário: $sizeKb KB         " -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Green
} else {
    Write-Host "Falha na compilação!" -ForegroundColor Red
    exit 1
}
