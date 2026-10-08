# BranchScribe Architecture

## Overview

BranchScribe is a WinUI 3 packaged desktop application built on .NET 8 and the Windows App SDK. It follows an MVVM architecture using CommunityToolkit.Mvvm with dependency injection via Microsoft.Extensions.DependencyInjection.

## Local-First Design

BranchScribe is designed with a **local-first** philosophy:

- **All AI inference runs locally by default** using either Windows App SDK's Phi Silica model or Foundry Local.
- **Sensitive customer data never leaves the device** unless explicitly routed to a cloud provider.
- **Transcription runs locally** (mock provider included; real providers plug in via `ITranscriptionService`).
- **All session data is stored locally** (in-memory for MVP; extend to SQLite/local DB for production).

## Hybrid Routing Loop

The `IRoutingEvaluator` (implemented by `HybridRoutingEvaluator`) decides whether each LLM request should be handled locally or by a cloud provider. It composes multiple `IRoutingRule` evaluators:

| Evaluator | Logic |
|-----------|-------|
| **PrivacyEvaluator** | Detects PII patterns (long digit strings, NI numbers, sort codes). Forces local when detected. |
| **LatencyEvaluator** | Prefers local for short/interactive prompts (live session). |
| **CostEvaluator** | Always prefers local (no per-token cost). Placeholder for real cost model. |
| **LocalContextEvaluator** | Prefers local when a knowledge base is available. |

Each evaluator returns a `(target, weight)` tuple. The router sums weights per target and picks the winner.

### Routing Flow

```
LlmRequest → HybridRoutingEvaluator
                ├─ PrivacyEvaluator    → (Local, 1.0) or (Local, 0.3)
                ├─ LatencyEvaluator    → (Local, 0.7) or (Local, 0.3)
                ├─ CostEvaluator       → (Local, 0.5)
                └─ LocalContextEvaluator → (Local, 0.6) or (Cloud, 0.2)
             → RoutingDecision { Target, Reason, Scores }
```

## Switching Between PhiSilicaLocalLLM and FoundryLocalLLM

Both implement `ILocalLLM`. To switch:

### Option A: Phi Silica (Windows App SDK)
In `App.xaml.cs`, change the DI registration:
```csharp
services.AddSingleton<ILocalLLM, PhiSilicaLocalLLM>();
```

Requirements:
- Windows 11 build 26100+ with NPU
- `systemAI` capability in Package.appxmanifest (uncomment the line)
- Model downloaded via Windows Update

### Option B: Foundry Local
```csharp
services.AddSingleton<ILocalLLM, FoundryLocalLLM>();
```

Requirements:
- Foundry Local running (e.g., `foundry-local start`)
- Endpoint configured in `appsettings.json` under `FoundryLocal:Endpoint`

### Fallback
Both implementations fall back to `MockLocalLLM` when the real provider is unavailable.

## Agent Architecture

The `AdvisorAssistantAgent` implements a tool-call pattern:

- **GetMissingFields**: Returns fields not yet extracted.
- **GetLowConfidenceFields**: Fields with confidence below threshold.
- **GetCitation**: Transcript snippet + timestamp for a field.
- **SummariseSession**: Meeting summary.
- **DraftCustomerRecap**: Customer-friendly recap.

Each session maintains an `AgentThread` with multi-turn conversation state.

## Project Structure

```
src/BranchScribe/
├── Models/          # Data models (Session, FormSchema, etc.)
├── Services/
│   ├── Interfaces/  # Service contracts
│   ├── LLM/         # LLM providers (PhiSilica, Foundry, Mock)
│   ├── Transcription/  # Transcription providers
│   ├── Extraction/  # Form field extraction
│   ├── Planning/    # Next question planner
│   ├── Agent/       # Advisor assistant agent
│   ├── Routing/     # Hybrid routing evaluators
│   └── Metrics/     # Sustainability metrics
├── ViewModels/      # MVVM ViewModels
├── Views/           # XAML pages
├── Helpers/         # Converters, navigation helpers
└── Assets/Schemas/  # Form schema definitions
```

## Metrics & Sustainability

The `MetricsEvaluator` calculates:
- **Energy (Wh)** = Device Watts × Inference Time (s) / 3600
- **CO₂ (g)** = Energy (kWh) × Grid Carbon Factor (g/kWh)
- **CO₂ Saved** = Estimated cloud CO₂ − Local CO₂

Configuration is in `appsettings.json` under `Metrics`.
