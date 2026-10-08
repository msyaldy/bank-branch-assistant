# BranchScribe

A WinUI 3 (Windows App SDK) packaged desktop application for in-branch financial advisors. BranchScribe captures live meeting sessions with local AI-powered transcription, extracts structured form data, suggests missing questions, generates meeting pack summaries, and provides an AI advisor assistant — all running locally on-device.

## Features

- **Live Session Capture**: Real-time transcription with form field extraction
- **AI Form Extraction**: Automatically fills form fields from conversation transcript
- **Next Best Question**: AI-powered suggestions for missing information
- **Advisor Assistant**: Multi-turn chat for querying session state, citations, and summaries
- **Meeting Pack Generation**: Advisor and customer summaries with action checklists
- **Insights Dashboard**: Productivity metrics, CO₂ savings, sentiment trends
- **Local-First AI**: Phi Silica (Windows AI) or Foundry Local — sensitive data stays on-device
- **Hybrid Routing**: Intelligent local vs. cloud routing with privacy-aware evaluation

## Prerequisites

- **Windows 11** (build 19041+; build 26100+ for Phi Silica)
- **Visual Studio 2022** (17.9+) with:
  - .NET 8 SDK
  - Windows App SDK workload
  - Windows 10/11 SDK (10.0.22621.0)
- **Git**

### Optional

- **Foundry Local** for local LLM inference: [Install Foundry Local](https://github.com/microsoft/foundry-local)
- **NPU-capable hardware** for Phi Silica (Copilot+ PCs)

## Getting Started

### Clone and Build

```bash
git clone <repository-url>
cd bank-branch-assistant
```

Open `BranchScribe.sln` in Visual Studio 2022, or build from command line:

```bash
dotnet restore BranchScribe.sln
dotnet build BranchScribe.sln -c Debug -p:Platform=x64
```

### Run

From Visual Studio: Set `BranchScribe` as startup project and press F5.

From command line:
```bash
dotnet run --project src/BranchScribe/BranchScribe.csproj
```

## Configuration

### appsettings.json

Located at `src/BranchScribe/appsettings.json`:

```json
{
  "FoundryLocal": {
    "Endpoint": "http://localhost:5273/v1/chat/completions",
    "ApiKey": "",
    "ModelId": "phi-3.5-mini"
  },
  "Metrics": {
    "BaselineMinutesSavedPerSession": 25,
    "DeviceWatts": 15.0,
    "GridCarbonFactorGramsPerKWh": 350.0
  }
}
```

### Enable Windows AI (Phi Silica)

1. Ensure you're on Windows 11 build 26100+
2. In `Package.appxmanifest`, uncomment:
   ```xml
   <uap15:Capability Name="systemAI" />
   ```
3. In `App.xaml.cs`, change the DI registration:
   ```csharp
   services.AddSingleton<ILocalLLM, PhiSilicaLocalLLM>();
   ```
4. The Phi Silica model will be downloaded via Windows Update if not already available.

### Configure Foundry Local

1. Install and start Foundry Local:
   ```bash
   foundry-local start
   ```
2. Update `appsettings.json` with the correct endpoint and model ID.
3. In `App.xaml.cs`, change DI registration:
   ```csharp
   services.AddSingleton<ILocalLLM, FoundryLocalLLM>();
   ```

## Running Tests

```bash
dotnet test tests/BranchScribe.Tests/BranchScribe.Tests.csproj -p:Platform=x64
```

Tests cover:
- Schema dependency resolution
- Missing field detection
- Routing evaluator behavior
- CO₂ calculation accuracy

## Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for detailed architecture documentation.

## Security

See [docs/SECURITY.md](docs/SECURITY.md) for security considerations and implementation placeholders.

## Project Structure

```
bank-branch-assistant/
├── BranchScribe.sln
├── README.md
├── docs/
│   ├── ARCHITECTURE.md
│   └── SECURITY.md
├── src/BranchScribe/
│   ├── BranchScribe.csproj
│   ├── Package.appxmanifest
│   ├── appsettings.json
│   ├── Assets/Schemas/         # Form schema definitions
│   ├── Models/                 # Data models
│   ├── Services/               # Business logic
│   ├── ViewModels/             # MVVM ViewModels
│   ├── Views/                  # XAML pages
│   └── Helpers/                # Converters, utilities
└── tests/BranchScribe.Tests/   # Unit tests
```

## TODOs

- [ ] Integrate real Windows AI (Phi Silica) APIs when available
- [ ] Implement real transcription provider (e.g., Azure Speech, Whisper)
- [ ] Add local storage encryption (DPAPI)
- [ ] Implement consent UI flow
- [ ] Add RAG knowledge base for advisor assistant
- [ ] Implement cloud provider stub with real endpoints
- [ ] Add SQLite persistence for sessions
- [ ] Implement sentiment model (replace stub classifier)
