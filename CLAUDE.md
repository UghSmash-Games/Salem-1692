# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Salem 1692 is a card game built in **Unity 6000.0.37f1** using C#. It implements the Salem witch trial-themed card game with local multiplayer and AI opponents. The game targets **AirConsole** — a platform where players use their smartphones as controllers connected to a shared screen via a WebGL build.

## Development Setup

- **Engine**: Unity 6000.0.37f1 (required — see `ProjectSettings/ProjectVersion.txt`)
- **Render Pipeline**: Universal Render Pipeline (URP)
- **UI**: Unity UI (uGUI) + TextMesh Pro
- **Input**: Unity Input System
- **Platform**: AirConsole (WebGL) — smartphones as controllers, shared screen display
- **No external build tools** — all building, testing, and running happens through the Unity Editor

## Running the Project

Open the project in Unity 6000.0.37f1. Entry scenes are in `Assets/Project/Scenes/`:
- **Title & Menus/** — title screen and menu flow
- **Game/** — main gameplay scene
- **Debug/** — debug/test scene

## Testing & Debug

- Unity Test Framework is installed but there are no automated test suites yet
- `TestManager` (`Scripts/DebugTools/TestManager.cs`) provides manual test utilities
- `DebugPanel` (`Scripts/DebugTools/DebugPanel.cs`) provides in-game debug features
- Press **P** during gameplay to advance game phases (debug key in `GamePhaseManager`)

## Architecture

The project uses a **manager/singleton pattern**. `GameManager` is the central hub with `[DefaultExecutionOrder(-100)]` to ensure it initializes first. Other managers register with or are referenced by `GameManager`.

### Game Phase Flow

The game progresses through phases managed by `GamePhaseManager`:
**Setup → Dawn → Day → Conspiracy → Night → EndGame**

- `GameTurnManager` — tracks whose turn it is and advances through players
- `TurnPhaseController` — controls transitions within individual phases
- `NightResolver` — resolves night phase mechanics
- `CardEffectManager` — executes card play effects
- `GameSetup` — initializes trial deck and distributes cards

### Key Systems

| System | Namespace | Purpose |
|--------|-----------|---------|
| Game Flow | `Salem.GameFlow` | Core game loop, phases, turns, card effects |
| Players | `Salem.Players` | Player, AIPlayer, AITurnSequencer, PlayerManager |
| Cards | `Salem.Cards` | Card, TryalCard, TownHallCard, ActionCardSO (ScriptableObjects) |
| Deck | `Salem.Deck` | DeckManager — shuffle, draw, discard |
| UI | `Salem.UI` | All UI controllers (targeting, card display, logs, end game) |
| Data | `Salem.Data` | Services (PlayerService, TrialService, RNGService), RNG, game results |
| Rules | `Salem.Rules` | Targeting policy for card plays |
| Managers | `Salem.Managers.GameState` | GameStateManager for state tracking |
| Debug | `Salem.DebugTools` | DebugPanel, TestManager |
| Systems | `Salem.Systems` | SceneLoader |
| AirConsole | `Salem.AirConsole` | AirConsoleManager, InputHandler, message protocol |

### AirConsole Integration

The game uses **AirConsole** so players' phones act as controllers. The integration has three layers:

- **`AirConsoleManager`** — Singleton bridge to the AirConsole SDK (`NDream.AirConsole`). Handles device connections, maps device IDs to `Player` objects, sends game state to phones, receives input messages.
- **`AirConsoleInputHandler`** — Processes incoming controller messages (`play_card`, `draw_cards`, `select_target`, `end_turn`) and routes them into `GameTurnManager` / `CardEffectManager`.
- **`AirConsoleMessages`** — Defines the JSON message protocol (C# classes) for screen↔controller communication.

**Controller UI**: `Assets/WebGLTemplates/AirConsole/controller.html` — the HTML/JS page shown on each player's phone. Displays hand, action buttons, and target picker.

**Mode detection**: `PlayerService.IsAirConsoleMode` is set by `AirConsoleManager.Awake()`. When true, `GameTurnManager.RunTurn()` waits for AirConsole input instead of local UI clicks, and `GameManager` skips local player UI setup.

**AirConsole SDK prerequisite**: The AirConsole Unity Plugin (`.unitypackage`) must be imported into the project separately. Download from the Unity Asset Store or the [AirConsole GitHub](https://github.com/AirConsole/airconsole-unity-plugin).

### AI System

AI opponents use `AIPlayer` for decision-making, `AITurnSequencer` for turn execution, and `AITargetingHelper` for target selection. Both human and AI players implement `IPlayerController`.

## Code Conventions

- All scripts live under `Assets/Project/Scripts/` organized by system
- Namespaces follow `Salem.<System>` pattern (e.g., `Salem.GameFlow`, `Salem.Players`)
- File headers use a standard block comment format: AUTHOR, REFERENCES, NOTES (Primary Purpose, Responsibilities, Access Requirements), TODO, FIXME
- ScriptableObjects for card definitions are in `Assets/Project/Prefabs/Scriptable Objects/Cards/`
- `GameManager.Instance` is the primary singleton access point
