param([ValidateSet('Debug','Release')][string]$Configuration='Release')
$ErrorActionPreference='Stop'
$workspacePath=Split-Path -Parent $PSScriptRoot
$packagePath=Join-Path $workspacePath 'artifacts\AdmitOne'
New-Item -ItemType Directory -Force -Path $packagePath | Out-Null
foreach($projectName in @('AdmitOne.Core','AdmitOne.Desktop','AdmitOne.Installer')) {
    $outputPath=Join-Path $workspacePath "src\$projectName\bin\$Configuration"
    if(-not (Test-Path -LiteralPath $outputPath)) { throw "Compila $Configuration antes de generar el paquete." }
    Get-ChildItem -LiteralPath $outputPath -File | Where-Object { $_.Extension -in @('.exe','.dll','.config','.pdb') } | Copy-Item -Destination $packagePath
}
Copy-Item -LiteralPath (Join-Path $workspacePath 'database') -Destination $packagePath -Recurse -Force
foreach($document in @('README.md','VALIDATION.md')) { Copy-Item -LiteralPath (Join-Path $workspacePath $document) -Destination $packagePath -Force }
Compress-Archive -Path (Join-Path $packagePath '*') -DestinationPath (Join-Path $workspacePath 'artifacts\AdmitOne-Desktop-net48.zip') -Force
Write-Output "Paquete generado: $packagePath"
