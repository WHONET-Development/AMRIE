<#
.SYNOPSIS
    Build and publish AMRIE.WinUI as an MSIX package or unpackaged (for MSI).
.DESCRIPTION
    Supports building MSIX packages for x64, x86, or ARM64, as well as unpackaged binaries suitable for WiX/MSI installers.
.EXAMPLE
    .\Publish-WinUI.ps1 -Type MSIX -Platform x64 -Configuration Release
    .\Publish-WinUI.ps1 -Type Unpackaged -Platform x64 -Configuration Release
#>
param(
    [ValidateSet("MSIX", "Unpackaged")]
    [string]$Type = "MSIX",

    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProjectPath = "AMRIE.WinUI\AMRIE.WinUI.csproj"

Write-Host "Publishing AMRIE.WinUI ($Type | $Platform | $Configuration)..." -ForegroundColor Cyan

if ($Type -eq "MSIX") {
    # Generate packaged MSIX installer
    dotnet publish $ProjectPath `
        -c $Configuration `
        -p:Platform=$Platform `
        -p:RuntimeIdentifier=win-$Platform `
        -p:WindowsPackageType=MSIX `
        -p:GenerateAppxPackageOnBuild=true `
        -p:AppxPackageDir="..\AppPackages\"
} else {
    # Generate unpackaged binaries for WiX/MSI
    dotnet publish $ProjectPath `
        -c $Configuration `
        -p:Platform=$Platform `
        -p:RuntimeIdentifier=win-$Platform `
        -p:WindowsPackageType=None `
        -p:PublishProfile="Properties\PublishProfiles\win-$Platform.pubxml"
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "Publish completed successfully!" -ForegroundColor Green
} else {
    Write-Error "Publish failed with exit code $LASTEXITCODE."
}
