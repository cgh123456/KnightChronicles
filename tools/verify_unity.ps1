param(
    [string]$EditorPath = 'D:/软件/unity/2022.3.62f1/Editor/Unity.exe',
    [ValidateSet('configure','edit','play','build')][string]$Stage = 'edit',
    [string]$ProjectPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'unity/KnightChronicles')
)
$ErrorActionPreference = 'Stop'
$resultDir = Join-Path (Split-Path $PSScriptRoot -Parent) '.work/validation'
New-Item -ItemType Directory -Path $resultDir -Force | Out-Null
$arguments = @('-batchmode','-projectPath', ('"' + (Resolve-Path $ProjectPath).Path + '"'), '-logFile', ('"' + $resultDir + '/unity-' + $Stage + '.log"'))
switch ($Stage) {
    'configure' { $arguments += @('-quit','-executeMethod','KnightChronicles.Editor.BuildTools.Configure') }
    'edit' { $arguments += @('-runTests','-testPlatform','EditMode','-testResults',('"' + $resultDir + '/edit-results.xml"')) }
    'play' { $arguments += @('-runTests','-testPlatform','PlayMode','-testResults',('"' + $resultDir + '/play-results.xml"'),'-screen-width','1600','-screen-height','900') }
    'build' { $arguments += @('-quit','-executeMethod','KnightChronicles.Editor.BuildTools.Windows') }
}
$process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Unity $Stage started: PID $($process.Id), log $resultDir/unity-$Stage.log"
$process.Id | Set-Content (Join-Path $resultDir "unity-$Stage.pid")
