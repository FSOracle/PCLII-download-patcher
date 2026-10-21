# 用法：powershell -NoProfile -ExecutionPolicy Bypass -File run.ps1 -In <原 exe> -Out <输出 exe> -Dnlib <dnlib.dll>
param(
    [Parameter(Mandatory = $true)][string]$In,
    [Parameter(Mandatory = $true)][string]$Out,
    [Parameter(Mandatory = $true)][string]$Dnlib
)
$ErrorActionPreference = 'Stop'
$Dnlib = (Resolve-Path $Dnlib).Path
Add-Type -Path $Dnlib
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml
$wpf = @([System.Windows.Controls.Grid], [System.Windows.UIElement], [System.Windows.DependencyObject], [System.Windows.Markup.XamlReader], [System.Xaml.XamlType]) | ForEach-Object { $_.Assembly.Location }
$src = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'PclPatcher.cs')
Add-Type -TypeDefinition $src -Language CSharp -ReferencedAssemblies (@($Dnlib, 'System.Core') + $wpf | Select-Object -Unique)
[PclPatcher]::Run((Resolve-Path $In).Path, [IO.Path]::GetFullPath($Out))
