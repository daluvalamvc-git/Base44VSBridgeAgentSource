; Inno Setup script to bundle VsixBootstrapper.exe + Base44AIPilot.vsix
; Place this .iss file next to the built VsixBootstrapper.exe and the Base44AIPilot.vsix,
; then compile with Inno Setup to produce an installer that runs the bootstrapper.

[Setup]
AppName=Base44 AI Pilot - VSIX Installer
AppVersion=1.0
DefaultDirName={userdesktop}\Base44AIInstaller
DefaultGroupName=Base44AI
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=Base44AIPilot_Installer
Compression=lzma
SolidCompression=yes
PrivilegesRequired=none

[Files]
; Expect the two files to be in the same folder as this script when compiling.
Source: "VsixBootstrapper.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "Base44AIPilot.vsix"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{userdesktop}\Install Base44 AI Pilot"; Filename: "{app}\Uninst.exe"; Tasks: desktopicon

[Run]
; Run the bootstrapper to install the VSIX silently. Remove /quiet if you want the VSIX UI.
Filename: "{tmp}\VsixBootstrapper.exe"; Parameters: """{tmp}\Base44AIPilot.vsix"""; Flags: runhidden waituntilterminated

[UninstallRun]
; No uninstall action required for the VSIX itself; this runs if you uninstall the wrapper.
; (VSIX uninstall must be done via Visual Studio or VSIXInstaller with uninstall parameters.)
