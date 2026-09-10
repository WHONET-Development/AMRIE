<#
.SYNOPSIS
    Build and launch the WinUI 3 desktop application using dotnet run or WinApp CLI (winapp).
.DESCRIPTION
    By default launches in unpackaged desktop mode (requires no Developer Mode).
    Pass -Packaged to launch with WinApp CLI package registration.
.EXAMPLE
    .\BuildAndRun.ps1
    .\BuildAndRun.ps1 -Configuration Release
    .\BuildAndRun.ps1 -Packaged
#>
param(
    [string]$Project = "AMRIE.WinUI\AMRIE.WinUI.csproj",
    [string]$Configuration = "Debug",
    [switch]$Packaged,
    [switch]$Detach
)

$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")

if ($Packaged) {
    Write-Host "Building and launching $Project with WinApp CLI..." -ForegroundColor Cyan
    if ($Detach) {
        winapp run $Project -c $Configuration --detach
    } else {
        winapp run $Project -c $Configuration --debug-output
    }
} else {
    Write-Host "Building and launching $Project in unpackaged desktop mode..." -ForegroundColor Cyan
    dotnet build $Project -c $Configuration -nr:false
    if ($LASTEXITCODE -eq 0) {
        if ($Detach) {
            Start-Process dotnet -ArgumentList "run --project `"$Project`" -c $Configuration --no-build"
        } else {
            dotnet run --project "$Project" -c $Configuration --no-build
        }
    }
}
