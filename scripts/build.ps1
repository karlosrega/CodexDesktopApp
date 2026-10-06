param([ValidateSet('Debug','Release')][string]$Configuration='Debug')
$ErrorActionPreference='Stop'
$workspacePath=Split-Path -Parent $PSScriptRoot
$vswherePath=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if(-not (Test-Path -LiteralPath $vswherePath)) { throw 'Instala Visual Studio 2022 o sus Build Tools con desarrollo de escritorio de .NET y targeting pack 4.8.' }
$msbuildPath=& $vswherePath -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if(-not $msbuildPath) { throw 'No se encontró MSBuild.' }
& $msbuildPath (Join-Path $workspacePath 'AdmitOne.sln') /t:Build "/p:Configuration=$Configuration" /verbosity:minimal /nologo
exit $LASTEXITCODE
