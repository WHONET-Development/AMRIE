<#
.SYNOPSIS
Builds AMRIE solution projects into an isolated sandbox output directory without disturbing repository outputs.
.PARAMETER Project
Project ID from projects.json (e.g. AMRIE.Library, AMRIE.CLI, AMRIE.WinForms, AMRIE.WinUI, AMRIE.Library.Tests, AMRIE.Library.DataGenerator). Defaults to 'AMRIE.Library'.
.PARAMETER Platform
Target platform: x86, x64, or AnyCPU. Defaults to the project's default platform in projects.json.
.PARAMETER Configuration
Build configuration: Debug or Release. Defaults to Debug.
.PARAMETER Label
Output label: candidate or baseline. Defaults to candidate.
.PARAMETER WorkspaceRoot
AMRIE solution root directory containing the indexed projects. Defaults to the AMRIE repository root.
#>
[CmdletBinding()]
param(
	[string]$Project = 'AMRIE.Library',
	[ValidateSet('x86', 'x64', 'AnyCPU')][string]$Platform,
	[string]$Configuration = 'Debug',
	[ValidateSet('candidate', 'baseline')][string]$Label = 'candidate',
	[string]$WorkspaceRoot
)
$ErrorActionPreference = 'Stop'
$index = Split-Path $PSScriptRoot -Parent
$repo = Split-Path $index -Parent
if (-not $WorkspaceRoot) { $WorkspaceRoot = $repo }
$WorkspaceRoot = (Resolve-Path -LiteralPath $WorkspaceRoot).ProviderPath
if (-not $WorkspaceRoot.EndsWith('\')) { $WorkspaceRoot += '\' }

$projectsJson = Get-Content -LiteralPath (Join-Path $index 'projects.json') -Raw | ConvertFrom-Json
$spec = $projectsJson.projects | Where-Object { $_.id -eq $Project }
if (-not $spec) {
	$validIds = ($projectsJson.projects | Select-Object -ExpandProperty id) -join ', '
	throw "Unknown project '$Project'. Valid project ids: $validIds"
}

if (-not $Platform) {
	$Platform = $spec.platform
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
if ($LASTEXITCODE -ne 0 -or -not $vs) { throw 'Visual Studio MSBuild was not found.' }

$isolatedRoot = Join-Path $index "local\$Label\$Platform"
New-Item -ItemType Directory -Path (Join-Path $isolatedRoot 'bin') -Force | Out-Null
$propsPath = Join-Path $isolatedRoot 'Isolated.props'

@'
<Project>
  <PropertyGroup Condition="'$(UsingMicrosoftNETSdk)' != 'true'">
    <OutputPath>$(IsolatedValidationRoot)\bin\$(MSBuildProjectName)\</OutputPath>
    <IntermediateOutputPath>$(IsolatedValidationRoot)\obj\$(MSBuildProjectName)\</IntermediateOutputPath>
    <PreBuildEvent></PreBuildEvent>
    <PostBuildEvent></PostBuildEvent>
    <RunPostBuildEvent>Never</RunPostBuildEvent>
  </PropertyGroup>
  <PropertyGroup Condition="'$(UsingMicrosoftNETSdk)' == 'true'">
    <OutputPath Condition="'$(TargetFramework)' == ''">$(IsolatedValidationRoot)\bin\$(MSBuildProjectName)\</OutputPath>
    <OutputPath Condition="'$(TargetFramework)' != ''">$(IsolatedValidationRoot)\bin\$(MSBuildProjectName)\$(TargetFramework)\</OutputPath>
    <PreBuildEvent></PreBuildEvent>
    <PostBuildEvent></PostBuildEvent>
    <RunPostBuildEvent>Never</RunPostBuildEvent>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath $propsPath -Encoding UTF8

# Determine required dependencies in build order
$dependencyTree = @{
	'AMRIE.Library' = @()
	'AMRIE.CLI' = @('AMRIE.Library')
	'AMRIE.WinForms' = @('AMRIE.Library')
	'AMRIE.WinUI' = @('AMRIE.Library')
	'AMRIE.Library.Tests' = @('AMRIE.Library')
	'AMRIE.Library.DataGenerator' = @('AMRIE.Library')
}

$toBuild = @()
if ($dependencyTree.ContainsKey($Project)) {
	foreach ($dep in $dependencyTree[$Project]) {
		$depBin = Join-Path $isolatedRoot "bin\$dep"
		if (-not (Test-Path $depBin) -or @(Get-ChildItem $depBin -Recurse -File | Where-Object { $_.Extension -in '.dll', '.exe' }).Count -eq 0) {
			$toBuild += $dep
		}
	}
}
$toBuild += $Project

function Invoke-ProjectBuild($targetId) {
	$targetSpec = $projectsJson.projects | Where-Object { $_.id -eq $targetId }
	$csproj = Join-Path $WorkspaceRoot $targetSpec.path
	$logPath = Join-Path $isolatedRoot "build-$targetId.log"
	$workDir = $repo
	$targetPlatform = if ($Platform) { $Platform } elseif ($targetSpec.platform) { $targetSpec.platform } else { 'AnyCPU' }
	$msbuild = if ($targetPlatform -ne 'x86') {
		Join-Path $vs 'MSBuild\Current\Bin\amd64\MSBuild.exe'
	} else {
		Join-Path $vs 'MSBuild\Current\Bin\MSBuild.exe'
	}

	$arguments = @(
		$csproj,
		'/nologo',
		'/m:1',
		'/nr:false',
		'/p:UseSharedCompilation=false',
		'/t:Build',
		"/p:Configuration=$Configuration",
		"/p:Platform=$targetPlatform",
		"/p:IsolatedValidationRoot=$isolatedRoot",
		"/p:CustomBeforeMicrosoftCommonTargets=$propsPath",
		'/p:BuildProjectReferences=false',
		'/p:PreBuildEvent=',
		'/p:PostBuildEvent=',
		'/p:RunPostBuildEvent=Never',
		'/v:minimal',
		"/flp:logfile=$logPath;verbosity=normal"
	)

	& $msbuild @arguments
	if ($LASTEXITCODE -ne 0) {
		throw "Isolated build failed for $targetId ($targetPlatform $Configuration); see $logPath."
	}
}

foreach ($item in $toBuild) {
	Write-Host "Building isolated dependency: $item ($Platform)..."
	Invoke-ProjectBuild $item
}

Write-Output "Successfully built $Project and dependencies ($Platform $Configuration) into $isolatedRoot"

