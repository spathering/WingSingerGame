$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsRoot = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsRoot) { throw 'Install Visual Studio C++ build tools and the Windows 10/11 SDK first.' }
$toolset = (Get-ChildItem -LiteralPath (Join-Path $vsRoot 'VC/Tools/MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
$sdk = (Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'Include') -Directory | Sort-Object Name -Descending | Select-Object -First 1).Name
$compiler = Join-Path $toolset 'bin/Hostx64/x64/cl.exe'
$output = Join-Path $projectRoot 'Assets/Plugins/WingSinger/Bluetooth/x86_64'
$intermediate = Join-Path $projectRoot 'Logs/BluetoothNativeBuild'
New-Item -ItemType Directory -Path $output,$intermediate -Force | Out-Null
$arguments = @('/nologo','/std:c++20','/EHsc','/O2','/MT','/LD','/W4',
    "/I$toolset/include", "/I$sdkRoot/Include/$sdk/ucrt", "/I$sdkRoot/Include/$sdk/shared",
    "/I$sdkRoot/Include/$sdk/um", "/I$sdkRoot/Include/$sdk/winrt", "/I$sdkRoot/Include/$sdk/cppwinrt",
    (Join-Path $PSScriptRoot 'WingSingerBle.cpp'), "/Fo$intermediate/WingSingerBle.obj",
    '/link', "/LIBPATH:$toolset/lib/x64", "/LIBPATH:$sdkRoot/Lib/$sdk/um/x64", "/LIBPATH:$sdkRoot/Lib/$sdk/ucrt/x64",
    'windowsapp.lib','ole32.lib', "/OUT:$output/WingSingerBle.dll", "/IMPLIB:$intermediate/WingSingerBle.lib")
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'BLE native build failed.' }
Write-Output "Built $output/WingSingerBle.dll (Windows x64, static CRT)"
