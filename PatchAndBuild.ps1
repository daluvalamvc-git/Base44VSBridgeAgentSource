# Base44 AI Pilot — Patch & Build Script
# Run this from the vsix\ folder:
#   Right-click PatchAndBuild.ps1 → Run with PowerShell
# It patches source.extension.vsixmanifest and Base44AIPilot.csproj
# in-place, then launches MSBuild to produce the installable .vsix

$ErrorActionPreference = "Stop"
$projectDir = Join-Path $PSScriptRoot "Base44AIPilot"

# ── 1. Patch source.extension.vsixmanifest ───────────────────────────────────
$manifestPath = Join-Path $projectDir "source.extension.vsixmanifest"
Write-Host "Patching $manifestPath ..."

$manifest = @'
<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011"
                 xmlns:d="http://schemas.microsoft.com/developer/vsx-schema-design/2011">
  <Metadata>
    <Identity Id="Base44AIPilot.DayakarA.A1B2C3D4E5F678901"
              Version="1.0.0"
              Language="en-US"
              Publisher="DayakarA"/>
    <DisplayName>Base44 AI Pilot</DisplayName>
    <Description xml:space="preserve">GitHub Copilot-style AI assistant for .NET MVC solutions powered by Base44.</Description>
    <MoreInfo>https://app.base44.com/superagent/6a57fda9caabceffcbd70384</MoreInfo>
    <License/>
    <Icon>Resources\icon.png</Icon>
    <PreviewImage/>
  </Metadata>
  <Installation AllUsers="false">
    <InstallationTarget Id="Microsoft.VisualStudio.Community"
                        Version="[17.0,18.0)"
                        ProductArchitecture="amd64"/>
    <InstallationTarget Id="Microsoft.VisualStudio.Professional"
                        Version="[17.0,18.0)"
                        ProductArchitecture="amd64"/>
    <InstallationTarget Id="Microsoft.VisualStudio.Enterprise"
                        Version="[17.0,18.0)"
                        ProductArchitecture="amd64"/>
  </Installation>
  <Dependencies>
    <Dependency Id="Microsoft.Framework.NDP" DisplayName=".NET Framework" Version="[4.7.2,)"/>
  </Dependencies>
  <Prerequisites>
    <Prerequisite Id="Microsoft.VisualStudio.Component.CoreEditor"
                  Version="[17.0,18.0)"
                  DisplayName="Visual Studio core editor"/>
  </Prerequisites>
  <Assets>
    <Asset Type="Microsoft.VisualStudio.MefComponent"
           d:Source="Project"
           d:ProjectName="%CurrentProject%"
           Path="|Base44AIPilot|"/>
    <Asset Type="Microsoft.VisualStudio.VsPackage"
           d:Source="File"
           Path="Base44AIPilot.pkgdef"/>
  </Assets>
</PackageManifest>
'@
[System.IO.File]::WriteAllText($manifestPath, $manifest, [System.Text.Encoding]::UTF8)
Write-Host "  manifest OK" -ForegroundColor Green

# ── 2. Patch .csproj — set GeneratePkgDefFile to false ──────────────────────
$csprojPath = Join-Path $projectDir "Base44AIPilot.csproj"
Write-Host "Patching $csprojPath ..."
$csproj = Get-Content $csprojPath -Raw
$csproj = $csproj -replace '<GeneratePkgDefFile>true</GeneratePkgDefFile>',
                            '<GeneratePkgDefFile>false</GeneratePkgDefFile>'
[System.IO.File]::WriteAllText($csprojPath, $csproj, [System.Text.Encoding]::UTF8)
Write-Host "  csproj OK" -ForegroundColor Green

# ── 3. Write Base44AIPilot.pkgdef ───────────────────────────────────────────
$pkgdefPath = Join-Path $projectDir "Base44AIPilot.pkgdef"
Write-Host "Writing $pkgdefPath ..."
$pkgdef = @'
// Base44 AI Pilot — hand-authored pkgdef
[$RootKey$\Packages\{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}]
@="Base44 AI Pilot"
"InprocServer32"="$WinDir$\SYSTEM32\mscoree.dll"
"Class"="Base44AIPilot.Base44AIPilotPackage"
"CodeBase"="$PackageFolder$\Base44AIPilot.dll"
"AllowsBackgroundLoad"=dword:00000001

[$RootKey$\AutoLoadPackages\{ADFC4E64-0397-11D1-9F4E-00A0C911004F}]
"{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"=dword:00000000

[$RootKey$\Menus\{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}]
@="., 1, 0"

[$RootKey$\ToolWindows\{C1D2E3F4-A5B6-7890-1234-567890ABCDEF}]
@="{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
"Name"="Base44 AI Chat"
'@
[System.IO.File]::WriteAllText($pkgdefPath, $pkgdef, [System.Text.Encoding]::UTF8)
Write-Host "  pkgdef OK" -ForegroundColor Green

# ── 4. Run MSBuild ───────────────────────────────────────────────────────────
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    Write-Host "MSBuild not found at expected path. Open the solution in VS and Rebuild All." -ForegroundColor Yellow
    Write-Host "All files are patched — just Rebuild All in VS now."
    Read-Host "Press Enter to exit"
    exit 0
}

Write-Host "`nRunning MSBuild Rebuild..." -ForegroundColor Cyan
& $msbuild $csprojPath /t:Rebuild /p:Configuration=Debug /p:DeployExtension=false /v:minimal

$vsix = Join-Path $projectDir "bin\Debug\Base44AIPilot.vsix"
if (Test-Path $vsix) {
    Write-Host "`n✅ SUCCESS: $vsix" -ForegroundColor Green
    Write-Host "Double-click the .vsix to install it in Visual Studio." -ForegroundColor Green
    explorer.exe (Split-Path $vsix)
} else {
    Write-Host "`n❌ Build failed — check the output above." -ForegroundColor Red
}

Read-Host "`nPress Enter to exit"
