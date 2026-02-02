# test.ps1 - NEO项目测试脚本
# 使用方法: .\scripts\test.ps1

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  NEO EEG+NIRS 项目测试脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 运行测试
Write-Host "`n[1/1] 运行测试..." -ForegroundColor Yellow
dotnet test --configuration Release --verbosity normal

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n========================================" -ForegroundColor Red
    Write-Host "  测试失败!" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  所有测试通过!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
