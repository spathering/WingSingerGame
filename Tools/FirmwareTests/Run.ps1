$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$vsRoot = & (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe') -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$toolset = (Get-ChildItem -LiteralPath (Join-Path $vsRoot 'VC/Tools/MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
$sdk = (Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'Include') -Directory | Sort-Object Name -Descending | Select-Object -First 1).Name
$output = Join-Path $root 'Logs/FirmwareTests'
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($name in @('WingSingerBreathSensorTests','WingSingerBleFirmwareTests')) {
    & (Join-Path $toolset 'bin/Hostx64/x64/cl.exe') /nologo /std:c++17 /EHsc /MT /W4 "/I$toolset/include" "/I$sdkRoot/Include/$sdk/ucrt" "/I$PSScriptRoot" "/I$PSScriptRoot/BLE" (Join-Path $PSScriptRoot "$name.cpp") "/Fo$output/$name.obj" "/Fe$output/$name.exe" /link "/LIBPATH:$toolset/lib/x64" "/LIBPATH:$sdkRoot/Lib/$sdk/ucrt/x64" "/LIBPATH:$sdkRoot/Lib/$sdk/um/x64"
    if ($LASTEXITCODE -ne 0) { throw "$name failed to compile" }
    & (Join-Path $output "$name.exe")
    if ($LASTEXITCODE -ne 0) { throw "$name failed" }
}
