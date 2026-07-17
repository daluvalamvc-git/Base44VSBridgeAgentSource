# Base44 AI Pilot — Visual Studio 2022 Extension

An AI-powered coding assistant for .NET MVC solutions, embedded in Visual Studio 2022.
Powered by your Base44 account — uses your Base44 credits.

---

## What It Does

| Menu Item | What It Does |
|---|---|
| **Ask AI** | Ask any question — agent reads your whole solution for context |
| **Analyze Solution** | Full architecture analysis: patterns, anti-patterns, recommendations |
| **Generate Feature** | End-to-end feature: Controller + Model + ViewModel + Views + Migration |
| **Refactor** | Refactor across the solution — returns unified diffs for review |
| **Explain Flow** | Traces request flow Controller → Service → Repository → EF Core → View |
| **Settings** | Set your Base44 API key and endpoint |

---

## Installation

### Prerequisites
- Visual Studio 2022 Community (17.x), 64-bit
- .NET Framework 4.7.2 SDK
- Visual Studio SDK (install via VS Installer → Individual Components → "Visual Studio SDK")

### Build & Install
```
1. Open Base44AIPilot.sln in Visual Studio 2022
2. Build → Build Solution (Ctrl+Shift+B)
3. The .vsix file appears in bin/Debug/
4. Double-click the .vsix to install
5. Restart Visual Studio
```

### Configure
```
1. Go to Tools → Options → Base44 AI Pilot
2. Enter your Base44 API Key
   (Find it at: https://app.base44.com/superagent/6a57fda9caabceffcbd70384 → Settings → API Docs)
3. Base URL is pre-filled: https://app.base44.com/api/apps/6a57fda9caabceffcbd70384
4. Click OK
```

---

## How It Works

```
Visual Studio 2022
    │
    ├─ SolutionReader.cs     ← Walks .sln → reads ALL files from disk
    │
    ├─ ApiClient.cs          ← Calls chunkSolutionContext function (prioritizes files)
    │                           to fit within token budget
    │
    └─ analyzeAndChat func   ← Sends chunked context + prompt to Base44 agent
                                Agent (dotnet_vs_pilot) analyzes and responds
                                Response contains text + [NEW FILE:] + unified diffs
                                DiffPreviewDialog lets you review before applying
```

---

## API Endpoints Used

| Function | URL | Purpose |
|---|---|---|
| chunkSolutionContext | POST /api/v1/chunk-context | Prioritizes solution files to fit token budget |
| analyzeAndChat | POST /api/v1/chat | Sends context + prompt to Base44 agent, returns response |
| generateDiff | POST /api/v1/diff | Generates unified diff from original + modified content |

---

## Privacy & Security
- Your API key is stored in VS encrypted settings (never in plain text files)
- Source code is sent to Base44's servers for AI analysis — treat it as you would GitHub Copilot
- The agent never stores your full source code permanently
- Connection strings and secrets found in config files are flagged but not reproduced in responses

---

## Credits Usage
Each prompt uses Base44 message credits. Larger solutions (more files sent) use more tokens.
Monitor usage at: https://app.base44.com/superagent/6a57fda9caabceffcbd70384
