# 一键打补丁：自动备份原版 → 打补丁 → 替换。由 一键打补丁.bat 调用，也可以直接运行。
param([string]$Target)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$tmp = $null

Write-Host '==== PCL2 下载功能恢复补丁（补丁版创作者：FS）====' -ForegroundColor Cyan
Write-Host ''

try {
    if (-not $Target) {
        Write-Host '请把【原版】Plain Craft Launcher 2.exe 拖进本窗口，然后按回车：'
        $Target = Read-Host
    }
    $Target = $Target.Trim().Trim('"')
    if (-not $Target -or -not (Test-Path -LiteralPath $Target -PathType Leaf)) { throw "找不到文件：$Target" }
    $Target = (Resolve-Path -LiteralPath $Target).Path
    Write-Host "目标文件：$Target"

    $running = Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -eq $Target } catch { $false }
    }
    if ($running) { throw 'PCL 正在运行，请先关闭 PCL 再打补丁。' }

    $tmp = "$Target.patched.tmp"
    Write-Host ''
    Write-Host '正在打补丁……'
    & (Join-Path $root 'run.ps1') -In $Target -Out $tmp -Dnlib (Join-Path $root 'lib\dnlib.dll')

    $bak = "$Target.bak"
    if (Test-Path -LiteralPath $bak) { $bak = "$Target.$(Get-Date -Format yyyyMMdd-HHmmss).bak" }
    Copy-Item -LiteralPath $Target -Destination $bak
    Move-Item -LiteralPath $tmp -Destination $Target -Force
    $tmp = $null

    Write-Host ''
    Write-Host '补丁完成！' -ForegroundColor Green
    Write-Host "原版已备份为：$bak"
    Write-Host '现在可以打开 PCL，进入 下载 → 自动安装 查看版本列表。'
}
catch {
    $msg = $_.Exception.Message
    if ($_.Exception.InnerException) { $msg = $_.Exception.InnerException.Message }
    Write-Host ''
    Write-Host "失败：$msg" -ForegroundColor Red
    if ($msg -like '*未找到预期的 IL 指令*' -or $msg -like '*形态与预期不符*') {
        Write-Host '可能原因：① 这个 exe 已经打过补丁（无需再打）；② PCL 版本不是 2.99.0.0（413），拦截代码已变化。' -ForegroundColor Yellow
    }
    Write-Host '原文件没有被修改。'
    if ($tmp -and (Test-Path -LiteralPath $tmp)) { Remove-Item -LiteralPath $tmp -Force }
    exit 1
}
