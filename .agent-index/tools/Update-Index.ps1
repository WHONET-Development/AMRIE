<#
.SYNOPSIS
Regenerates or verifies the solution-wide agent index for AMRIE.
.DESCRIPTION
Evaluates every project in projects.json with MSBuild (no build, no restore), then runs the Roslyn indexer
over all of them together so cross-project references resolve to source. Writes manifest.json,
files.jsonl, symbols.jsonl, relationships.jsonl, state.jsonl and routes.jsonl next to this tools folder.
.PARAMETER Verify
Read-only freshness check against the current sources. Fails when generated output is stale or missing.
.PARAMETER WorkspaceRoot
AMRIE solution root containing the indexed project folders. Defaults to the AMRIE repository root.
.PARAMETER OutDir
Where generated output is written or verified. Defaults to the .agent-index folder (used by Test-Index.ps1 for scratch runs).
.EXAMPLE
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .agent-index\tools\Update-Index.ps1
.EXAMPLE
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .agent-index\tools\Update-Index.ps1 -Verify
#>
[CmdletBinding()]
param(
	[switch]$Verify,
	[string]$WorkspaceRoot,
	[string]$OutDir
)
$ErrorActionPreference = 'Stop'
$index = Split-Path $PSScriptRoot -Parent
$repo = Split-Path $index -Parent
if (-not $WorkspaceRoot) { $WorkspaceRoot = $repo }
$WorkspaceRoot = (Resolve-Path -LiteralPath $WorkspaceRoot).ProviderPath
if (-not $WorkspaceRoot.EndsWith('\')) { $WorkspaceRoot += '\' }
if (-not $OutDir) { $OutDir = $index }

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
if ($LASTEXITCODE -ne 0 -or -not $vs) { throw 'Visual Studio MSBuild/Roslyn is required.' }
$msbuild = Join-Path $vs 'MSBuild\Current\Bin\amd64\MSBuild.exe'
$roslyn = Join-Path $vs 'MSBuild\Current\Bin\Roslyn'

# 1. Evaluate every project. Output is written with -getResultOutputFile so NuGet/MSBuild warnings cannot corrupt the JSON.
$local = Join-Path $index 'local'
$evalDir = Join-Path $local 'eval'
if (Test-Path -LiteralPath $evalDir) { Remove-Item -LiteralPath $evalDir -Recurse -Force }
New-Item -ItemType Directory -Path $evalDir -Force | Out-Null
$config = Get-Content -LiteralPath (Join-Path $index 'projects.json') -Raw | ConvertFrom-Json
function Invoke-Evaluation($project, $targetFramework, $outputName) {
	$csproj = Join-Path $WorkspaceRoot $project.path
	if (-not (Test-Path -LiteralPath $csproj)) { throw "Project '$($project.id)' not found: $csproj." }
	$resultFile = Join-Path $evalDir ($outputName + '.json')
	if ($project.filesOnly) {
		$projDir = Split-Path $csproj -Parent
		$files = Get-ChildItem -Path $projDir -Recurse -File | Where-Object {
			$_.FullName -notmatch '\\bin\\' -and $_.FullName -notmatch '\\obj\\' -and ($_.Extension -in '.cs', '.xaml', '.wxs')
		}
		$compileList = @()
		foreach ($f in $files) {
			$compileList += @{ FullPath = $f.FullName }
		}
		$evalObj = @{
			Properties = @{
				AssemblyName = $project.id
				TargetFramework = if ($targetFramework) { $targetFramework } else { 'none' }
				TargetFrameworkVersion = ''
				OutputType = 'Library'
				DefineConstants = ''
				AllowUnsafeBlocks = 'false'
				IntermediateOutputPath = ''
			}
			Items = @{
				Compile = $compileList
				ReferencePath = @()
				ProjectReference = @()
				EmbeddedResource = @()
				Page = @()
				Analyzer = @()
				InternalsVisibleTo = @()
			}
		}
		$evalObj | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultFile -Encoding UTF8
		return
	}
	$workDir = $repo
	# ResolveAssemblyReferences (not ResolveReferences) deliberately skips COM interop resolution, which can hang and is not needed for binding.
	$arguments = @("`"$csproj`"", '/nologo', '/nr:false', '/t:ResolveAssemblyReferences', '/p:Configuration=Debug', "/p:Platform=$($project.platform)", '/p:BuildProjectReferences=false',
		'/p:PreBuildEvent=', '/p:PostBuildEvent=', '/getItem:Compile,EmbeddedResource,ReferencePath,ProjectReference,Page,Analyzer,InternalsVisibleTo',
		'/getProperty:DefineConstants,TargetFramework,TargetFrameworkVersion,AssemblyName,OutputType,AllowUnsafeBlocks,IntermediateOutputPath',
		"`"/getResultOutputFile:$resultFile`"", '/v:quiet')
	if ($targetFramework) { $arguments += "/p:TargetFramework=$targetFramework" }
	$process = Start-Process -FilePath $msbuild -ArgumentList $arguments -WorkingDirectory $workDir -NoNewWindow -PassThru -RedirectStandardOutput (Join-Path $evalDir ($outputName + '.log'))
	$null = $process.Handle   # caches the process handle so ExitCode is available after exit
	if (-not $process.WaitForExit(180000)) {
		Stop-Process -Id $process.Id -Force
		throw "Project evaluation timed out after 180 s for $($project.id) ($targetFramework)."
	}
	$process.WaitForExit()   # a timed wait leaves ExitCode unset; the parameterless wait populates it
	if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $resultFile)) { throw "Project evaluation failed for $($project.id) ($targetFramework); see $(Join-Path $evalDir ($outputName + '.log')). Restore packages once with Build-Isolated.ps1 -Project $($project.id); no build or restore is performed by this command." }
}
foreach ($project in $config.projects) {
	Invoke-Evaluation $project $project.tf $project.id
	foreach ($tf in @($project.shadowTfs)) { if ($tf) { Invoke-Evaluation $project $tf ($project.id + '@' + $tf) } }
}

# 2. Build the indexer (64-bit, so all compilations fit in memory) beside copies of the Roslyn binaries.
$bin = Join-Path $local 'indexer'
New-Item -ItemType Directory -Path $bin -Force | Out-Null
Copy-Item -Path (Join-Path $roslyn '*.dll') -Destination $bin -Force
Copy-Item -LiteralPath (Join-Path $roslyn 'csc.exe.config') -Destination (Join-Path $bin 'Indexer.exe.config') -Force
$exe = Join-Path $bin 'Indexer.exe'
$facades = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\netstandard.dll'
if (-not (Test-Path -LiteralPath $facades)) {
	$facades = (Get-ChildItem -Path (Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework') -Filter 'netstandard.dll' -Recurse | Select-Object -First 1).FullName
}
& (Join-Path $roslyn 'csc.exe') /nologo /nowarn:1702 /langversion:latest /platform:anycpu /optimize+ /target:exe "/out:$exe" `
	"/r:$bin\Microsoft.CodeAnalysis.dll" "/r:$bin\Microsoft.CodeAnalysis.CSharp.dll" "/r:$bin\System.Collections.Immutable.dll" /r:System.Web.Extensions.dll "/r:$facades" (Join-Path $PSScriptRoot 'Indexer.cs')
if ($LASTEXITCODE -ne 0) { throw 'Indexer compilation failed.' }

# 3. Run it.
$mode = if ($Verify) { 'verify' } else { 'update' }
& $exe $WorkspaceRoot $index $evalDir $OutDir $mode
if ($LASTEXITCODE -ne 0) { throw 'Index generation/verification failed.' }

