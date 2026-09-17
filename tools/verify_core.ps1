param([string]$EditorPath = 'D:/软件/unity/2022.3.62f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$editor = Split-Path $EditorPath
$mono = Join-Path $editor 'Data/MonoBleedingEdge/bin/mono.exe'
$compiler = Join-Path $editor 'Data/MonoBleedingEdge/lib/mono/4.5/csc.exe'
$output = Join-Path $root '.work/validation'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$framework = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319'
$sources = Get-ChildItem (Join-Path $root 'unity/KnightChronicles/Assets/KnightChronicles/Runtime/Core') -Filter '*.cs' | ForEach-Object FullName
& $mono $compiler /nologo /nostdlib "/r:$framework/mscorlib.dll" "/r:$framework/System.dll" "/r:$framework/System.Core.dll" "/r:$framework/System.Runtime.Serialization.dll" "/out:$output/CoreRegression.exe" $sources (Join-Path $PSScriptRoot 'CoreRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed' }
& "$output/CoreRegression.exe" (Join-Path $root 'unity/KnightChronicles/Assets/Resources/Data/items.txt') | Tee-Object -FilePath "$output/core-results.txt"
if ($LASTEXITCODE -ne 0) { throw 'Core regression failed' }
