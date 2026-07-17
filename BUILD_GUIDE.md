# Base44 AI Pilot — Build & Install Guide

## Why legacy-style .csproj?

VSIX extensions **cannot** be built with the modern `Microsoft.NET.Sdk` project format.
The VSSDK tooling (`Microsoft.VsSDK.targets`) only produces a `.vsix` output file when
used with the **legacy non-SDK MSBuild project format** (the one that explicitly imports
`Microsoft.CSharp.targets`). Using `Microsoft.NET.Sdk` produces a `.dll` only.

---

## Prerequisites (install once)

1. **Visual Studio 2022 Community** (v17.x)
   - Download: https://visualstudio.microsoft.com/vs/community/

2. **"Visual Studio extension development" workload**
   - VS Installer → Modify → Workloads → tick "Visual Studio extension development"

3. **.NET Framework 4.7.2 targeting pack**
   - VS Installer → Individual Components → search "4.7.2" → tick and install

---

## Build Steps

```
1. Open  vsix\Base44AIPilot.sln  in Visual Studio 2022
2. Right-click solution → Restore NuGet Packages
   (downloads Newtonsoft.Json + VSSDK packages into vsix\packages\)
3. Build → Rebuild Solution  (Ctrl+Shift+B)
4. Output: Base44AIPilot\bin\Debug\Base44AIPilot.vsix
```

---

## Install the Extension

```
1. Close ALL Visual Studio instances
2. Double-click  Base44AIPilot\bin\Debug\Base44AIPilot.vsix
3. Click Install in the VSIX installer dialog
4. Reopen Visual Studio 2022
5. The "Base44 AI" top-level menu appears in the menu bar
```

---

## Configure the API Key

```
Tools → Options → Base44 AI Pilot → General

  API Key      : <paste your Base44 API key here>
  Token Budget : 12000  (increase for larger solutions)
```

**Where to find your API key:**
1. Open https://app.base44.com/superagent/6a57fda9caabceffcbd70384
2. Settings → API Docs → copy the key shown there

---

## Menu Commands

| Command | What it does |
|---|---|
| Base44 AI → Ask AI... | Free-form question; agent reads your full solution |
| Analyze Solution | Architecture report + 3+ actionable recommendations |
| Generate Feature... | Scaffolds Controller + Model + View + (optional) Migration |
| Refactor... | Cross-solution refactor with diff preview |
| Explain Flow... | Traces request end-to-end with file+method references |
| Settings... | Opens the Options page |

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Build produces .dll not .vsix | Must use legacy-style .csproj (not SDK-style). Use this repo as-is. |
| NuGet restore fails | Check internet connection; run Tools → NuGet Package Manager → Manage Packages |
| "API key not configured" | Tools → Options → Base44 AI Pilot |
| "No solution open" | Open a .NET MVC .sln file before using the extension |
| Extension not visible after install | Restart VS; check Extensions → Manage Extensions |
