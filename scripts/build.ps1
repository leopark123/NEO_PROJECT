# build.ps1 - NEO项目构建脚本
# 使用方法: .\scripts\build.ps1

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  NEO EEG+NIRS 项目构建脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 检查 dotnet 是否可用
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "错误: 未找到 dotnet CLI" -ForegroundColor Red
    exit 1
}

# 显示 dotnet 版本
Write-Host "`n[1/3] 检查环境..." -ForegroundColor Yellow
dotnet --version

# 还原依赖
Write-Host "`n[2/3] 还原依赖包..." -ForegroundColor Yellow
dotnet restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "错误: 依赖还原失败" -ForegroundColor Red
    exit $LASTEXITCODE
}

# 构建项目
Write-Host "`n[3/3] 构建项目..." -ForegroundColor Yellow
dotnet build --configuration Release --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "错误: 构建失败" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  构建成功!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
