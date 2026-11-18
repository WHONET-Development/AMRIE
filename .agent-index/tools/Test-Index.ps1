<#
.SYNOPSIS
Tests AMRIE solution-wide agent index verification, cross-project navigation queries, deterministic regeneration, and staleness rejection.
#>
[CmdletBinding()]
param(
	[string]$WorkspaceRoot
)
$ErrorActionPreference = 'Stop'
$index = Split-Path $PSScriptRoot -Parent
$repo = Split-Path $index -Parent
if (-not $WorkspaceRoot) { $WorkspaceRoot = $repo }
$WorkspaceRoot = (Resolve-Path -LiteralPath $WorkspaceRoot).ProviderPath
if (-not $WorkspaceRoot.EndsWith('\')) { $WorkspaceRoot += '\' }

$updateScript = Join-Path $PSScriptRoot 'Update-Index.ps1'

Write-Host "1. Running freshness verification against current sources..."
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $updateScript -Verify
if ($LASTEXITCODE -ne 0) { throw "Initial index freshness verification failed." }

Write-Host "2. Verifying cross-project routes and symbols..."
$routes = Get-Content -LiteralPath (Join-Path $index 'routes.jsonl') | ForEach-Object { $_ | ConvertFrom-Json }
$requiredRoutes = @(
	'interpretation engine',
	'breakpoints',
	'expert rules',
	'file interpretation',
	'WinForms',
	'WinUI',
	'command line',
	'test suites'
)
foreach ($req in $requiredRoutes) {
	$hit = $routes | Where-Object { $_.task -match $req }
	if (-not $hit) { throw "Missing required route matching: '$req'" }
}

$symbols = Get-Content -LiteralPath (Join-Path $index 'symbols.jsonl')
$expectedProjectIds = @('AMRIE.Library', 'AMRIE.CLI', 'AMRIE.WinForms', 'AMRIE.WinUI', 'AMRIE.Library.Tests', 'AMRIE.Library.DataGenerator')
foreach ($p in $expectedProjectIds) {
	$pattern = "`"project`":`"$p`""
	if (-not ($symbols | Select-String -Pattern $pattern -Quiet)) {
		throw "Expected symbols for project '$p' not found in symbols.jsonl"
	}
}

Write-Host "3. Testing deterministic regeneration into scratch directory..."
$scratchDir = Join-Path $index ('local\test-scratch-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratchDir -Force | Out-Null
try {
	& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $updateScript -OutDir $scratchDir
	if ($LASTEXITCODE -ne 0) { throw "Regeneration to scratch directory failed." }

	foreach ($file in @('files.jsonl', 'symbols.jsonl', 'relationships.jsonl', 'state.jsonl', 'routes.jsonl')) {
		$orig = Get-Content -LiteralPath (Join-Path $index $file) -Raw
		$regen = Get-Content -LiteralPath (Join-Path $scratchDir $file) -Raw
		if ($orig -ne $regen) {
			throw "Non-deterministic output detected in $file."
		}
	}
	Write-Host "   Deterministic regeneration verified byte-for-byte."

	Write-Host "4. Testing staleness rejection on simulated file change..."
	$testFile = Join-Path $scratchDir 'symbols.jsonl'
	[System.IO.File]::AppendAllText($testFile, "`n")
	$ErrorActionPreference = 'Continue'
	$probe = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $updateScript -Verify -OutDir $scratchDir 2>&1
	$probeExitCode = $LASTEXITCODE
	$ErrorActionPreference = 'Stop'
	if ($probeExitCode -eq 0 -or -not (($probe | Out-String) -match 'Stale or missing index')) {
		throw "Staleness probe did not reject modified index file as expected."
	}
	Write-Host "   Staleness rejection successfully verified."

	Write-Output "PASS: Solution-wide agent index verification, cross-project routing, deterministic regeneration, and staleness detection."
}
finally {
	if (Test-Path -LiteralPath $scratchDir) {
		Remove-Item -LiteralPath $scratchDir -Recurse -Force
	}
}

