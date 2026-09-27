<#
.SYNOPSIS
    Build and publish AMR Interpretation Engine (AMRIE.WinUI) as a Windows Installer (MSI).
.DESCRIPTION
    Builds the complete Windows Installer (MSI) via WiX v5 (AMRIE.WinUI.Installer).
    Version is computed automatically from the build date (yy.M.d), or can be overridden via -Version.
.EXAMPLE
    .\Publish-WinUI.ps1
    .\Publish-WinUI.ps1 -Platform x64 -Configuration Release
    .\Publish-WinUI.ps1 -Platform x64 -Configuration Release -Version "26.9.27"
    .\Publish-WinUI.ps1 -Type Unpackaged
#>
param(
    [ValidateSet("MSI", "Unpackaged")]
    [string]$Type = "MSI",

    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

# Calculate version based on build date: yy.M.d (e.g., 26.9.27 for Sept 27, 2026)
$today = Get-Date
$yy = $today.ToString("yy")
$m = $today.Month
$d = $today.Day
$buildDate = $today.ToString("dd-MM-yyyy")

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = "$yy.$m.$d"
}

$ProjectPath = "AMRIE.WinUI\AMRIE.WinUI.csproj"
$InstallerPath = "AMRIE.WinUI.Installer\AMRIE.WinUI.Installer.wixproj"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " AMR Interpretation Engine MSI Build" -ForegroundColor Cyan
Write-Host " Publisher : Brigham and Women's Hospital" -ForegroundColor Cyan
Write-Host " Version   : $Version" -ForegroundColor Cyan
Write-Host " Build Date: $buildDate" -ForegroundColor Cyan
Write-Host " Target    : $Type | $Platform | $Configuration" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Build WiX MSI
if ($Type -eq "MSI") {
    Write-Host "`nBuilding Windows Installer MSI (v$Version)..." -ForegroundColor Yellow
    dotnet build $InstallerPath `
        -c $Configuration `
        -p:Platform=$Platform `
        -p:Version=$Version `
        -p:BuildDate=$buildDate

    if ($LASTEXITCODE -ne 0) {
        Write-Error "MSI build failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }

    $msiOutput = "AMRIE.WinUI.Installer\bin\$Platform\$Configuration\en-us\AMR_Interpretation_Engine_WinUI_$Platform.msi"
    Write-Host "`nMSI Installer built successfully: $msiOutput" -ForegroundColor Green
}

# 2. Unpackaged only
if ($Type -eq "Unpackaged") {
    Write-Host "`nPublishing Unpackaged Binaries (v$Version)..." -ForegroundColor Yellow
    dotnet publish $ProjectPath `
        -c $Configuration `
        -p:Platform=$Platform `
        -p:RuntimeIdentifier=win-$Platform `
        -p:WindowsPackageType=None `
        -p:PublishProfile="Properties\PublishProfiles\win-$Platform.pubxml" `
        -p:Version=$Version

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Unpackaged publish failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }

    $pubDir = "AMRIE.WinUI\bin\$Platform\$Configuration\net8.0-windows10.0.26100.0\win-$Platform\publish\"
    Write-Host "`nUnpackaged binaries published successfully: $pubDir" -ForegroundColor Green
}

Write-Host "`nCompleted successfully for version $Version ($buildDate)!" -ForegroundColor Green
