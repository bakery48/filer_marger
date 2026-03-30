# FilerMerger ビルドスクリプト
# 使い方: PowerShell で .\build.ps1 を実行

param(
    [switch]$SelfContained,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "FilerMerger ビルド開始..." -ForegroundColor Cyan

if ($SelfContained) {
    Write-Host "自己完結型 (self-contained) でビルドします"
    dotnet publish -r win-x64 -c $Configuration `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false
} else {
    Write-Host "フレームワーク依存型でビルドします (.NET 6 ランタイムが必要)"
    dotnet publish -r win-x64 -c $Configuration `
        --self-contained false `
        -p:PublishSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false
}

if ($LASTEXITCODE -eq 0) {
    $outDir = "bin\$Configuration\net6.0-windows\win-x64\publish"
    Write-Host ""
    Write-Host "ビルド成功!" -ForegroundColor Green
    Write-Host "出力先: $outDir\FilerMerger.exe"
    Write-Host ""
    Write-Host "スタートアップへの登録 (任意):" -ForegroundColor Yellow
    Write-Host '  $exe = (Resolve-Path "' + $outDir + '\FilerMerger.exe").Path'
    Write-Host '  $regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"'
    Write-Host '  Set-ItemProperty -Path $regKey -Name "FilerMerger" -Value $exe'
} else {
    Write-Host "ビルド失敗" -ForegroundColor Red
    exit 1
}
