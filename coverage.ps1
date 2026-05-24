# =========================================================
# File: coverage.ps1
# Project: SortingMachine
# Sprint: S6 | Agent: Gemini CLI
# =========================================================

# coverage.ps1 - 一键生成测试覆盖率报告
# 使用方式：在解决方案根目录运行 .\coverage.ps1

$ErrorActionPreference = "Stop"
$ReportDir    = "coverage-report"
$CoverageOut  = "coverage-results"

Write-Host "=== 运行测试 + 收集覆盖率 ===" -ForegroundColor Cyan

if (Test-Path $CoverageOut)  { Remove-Item $CoverageOut  -Recurse -Force }
if (Test-Path $ReportDir)    { Remove-Item $ReportDir    -Recurse -Force }

dotnet test SortingMachine.sln `
    --collect:"XPlat Code Coverage" `
    --results-directory $CoverageOut `
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

if ($LASTEXITCODE -ne 0) {
    Write-Host "测试失败，停止生成报告" -ForegroundColor Red; exit 1
}

Write-Host "`n=== 生成 HTML 报告 ===" -ForegroundColor Cyan

if (-not (Get-Command reportgenerator -ErrorAction SilentlyContinue)) {
    Write-Host "安装 ReportGenerator..." -ForegroundColor Yellow
    dotnet tool install -g dotnet-reportgenerator-globaltool
}

$xml = Get-ChildItem -Path $CoverageOut -Filter "coverage.cobertura.xml" -Recurse |
       Select-Object -First 1 -ExpandProperty FullName

if (-not $xml) { Write-Host "未找到覆盖率文件" -ForegroundColor Red; exit 1 }

reportgenerator `
    -reports:"$xml" `
    -targetdir:"$ReportDir" `
    -reporttypes:"Html;HtmlSummary;Badges" `
    -assemblyfilters:"+SortingMachine" `
    -classfilters:"-*Mock*;-*Tests*;-*Fixture*" `
    -title:"SortingMachine 覆盖率报告"

Write-Host "`n=== 完成 ===" -ForegroundColor Green
Write-Host "报告位置：$ReportDir\index.html" -ForegroundColor Green
Start-Process "$ReportDir\index.html"
