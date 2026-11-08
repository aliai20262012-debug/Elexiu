# NEXUS CIVILIZATION

**Phase 02 — Core Game Architecture**

A cross-platform (Windows PC, Android tablets, Android phones, iOS-ready) strategy game
built with Unity 6.3 LTS, Universal Render Pipeline, the Input System package and
TextMeshPro.

This repository is currently a **verified technical foundation with a working simulation core**,
not a game: it contains the application architecture, project configuration, the
Boot → Main Menu → Gameplay shell, the deterministic simulation lifecycle (clock, pause, speed,
game sessions, ordered system graph) and the automated test suites. Domain gameplay — planet
generation, economy, civilization, diplomacy, AI — starts in Phase 03.

---

## 1. Requirements

| Item | Version / note |
| --- | --- |
| Unity Editor | **6000.3.24f1** (Unity 6.3 LTS). Any newer `6000.3.x` patch also works and will simply upgrade the project. |
| Modules | Windows Build Support (IL2CPP/Mono), Android Build Support (SDK/NDK/OpenJDK), iOS Build Support (macOS host required). Install what your machine can host — see [Missing modules](#5-platform-modules). |
| Render pipeline | Universal Render Pipeline `17.3.0` (installed through the package manifest) |
| Input | `com.unity.inputsystem` `1.20.0` — *Active Input Handling* is set to **Both** |
| UI / text | `com.unity.ugui` `2.0.0` (includes TextMeshPro) |
| Tests | `com.unity.test-framework` `1.6.0` |

No other packages are required. No third-party asset is used.

## 2. Opening the project

1. Clone the repository.
2. Unity Hub → **Add → Add project from disk** → select the repository root (the folder that
   contains `Assets/`, `Packages/` and `ProjectSettings/`).
3. Open with **Unity 6000.3.24f1** (or a newer 6000.3 LTS patch).
4. On first import Unity resolves the packages listed in `Packages/manifest.json`.
5. **One-time step:** `Window → TextMeshPro → Import TMP Essential Resources`.
   Until this is done the shell still runs, but labels fall back to legacy uGUI text
   (the fallback is intentional, see [ARCHITECTURE.md](ARCHITECTURE.md#7-text-rendering-fallback)).
6. Open `Assets/Scenes/Bootstrap/Bootstrap.unity` (or `Nexus → Open Bootstrap Scene`) and
   press **Play**.

`Nexus → Validate Project` runs the editor-side project validator and logs the result.

## 3. What you should see

* **Bootstrap scene** — a deep navy splash with `NEXUS CIVILIZATION`, a status line
  (`COMPOSING SERVICES` → `LOADING COMMAND INTERFACE`) and a progress bar.
* **Main Menu** — the title in electric cyan with wide letter spacing, subtitle
  `CORE SIMULATION BUILD · PHASE 02`, the buttons **NEW GAME / CONTINUE / SETTINGS / EXIT**,
  a build stamp and a footer with the detected platform. *Continue stays disabled until a
  save exists; Exit is hidden on mobile platforms.*
* **Gameplay** — a rotating placeholder planet plus the simulation control bar: a
  `T+ HH:MM:SS` simulated clock, the lifecycle line (`RUNNING // 1x`), the session line
  (`NEW COLONY // SEED … // TICK …`), **PAUSE/RESUME**, **1x / 2x / 4x** speed buttons and
  **RETURN TO MENU**. No gameplay rules are implemented yet — the clock only produces ticks.

### 3.1 Simulation controls

| Control | Effect |
| --- | --- |
| `PAUSE` / `RESUME` | Pauses/resumes the deterministic clock; the application state mirrors it (`Gameplay` ⇄ `Paused`) |
| `1x` / `2x` / `4x` | Multiplies simulated time; the active level is highlighted in cyan. Levels come from `GameConfiguration.SimulationSpeedMultipliers` |
| Clock line | Simulated time (`T+ HH:MM:SS`), derived from `ticks × interval` |
| Session line | Civilization name, deterministic seed and current tick |
| `RETURN TO MENU` | Ends the session (publishing `GameSessionEndedEvent`), stops the clock and reloads the main menu |

The simulation is frame-rate independent: the scene feeds `Time.deltaTime` into
`SimulationManager.Tick` once per frame, and everything below that boundary consumes whole
ticks (default `0.5 s`). See [ARCHITECTURE.md](ARCHITECTURE.md#11-simulation-core-phase-02).

## 4. Running the tests

From the editor: `Window → General → Test Runner`.

* **EditMode** (`Assets/Tests/EditMode`) — **162 tests** covering the state machine, event bus,
  service registry, configuration, game flow with a fake scene loader, save service, platform
  classification, UI scaling maths, build settings, and the Phase 02 core: deterministic clock
  (15), system registry ordering/failure isolation (18), simulation lifecycle and events (21),
  game sessions (15) and validated simulation settings (15).
* **PlayMode** (`Assets/Tests/PlayMode`) — **8 tests** covering bootstrap initialisation, scene
  navigation, the main-menu presentation, session start + ticking, pause/speed controls and
  safe session teardown across a scene transition with a clean restart.

> The suites are committed as source. They were authored in an environment without a Unity
> Editor, so they have **not** been executed yet — see
> [PHASE_02_REPORT.md](PHASE_02_REPORT.md#5-test-results).

Command line (headless):

```bash
# EditMode
Unity -batchmode -projectPath . -runTests -testPlatform EditMode \
      -testResults ./TestResults/editmode.xml -logFile -

# PlayMode
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode \
      -testResults ./TestResults/playmode.xml -logFile -
```

Static project validation (works even without Unity installed):

```bash
python3 Tools/validate_project.py --verbose
```

## 5. Platform modules

The build modules are **not** bundled with the repository and cannot be installed by it.
Check `Unity Hub → Installs → <version> → Add modules`:

| Target | Module | Status in this repository |
| --- | --- | --- |
| Windows PC | `Windows Build Support (IL2CPP)` | Settings pre-configured; module must be installed locally |
| Android tablets / phones | `Android Build Support` + SDK/NDK/OpenJDK | Settings pre-configured; module must be installed locally |
| iOS (future release) | `iOS Build Support` | Player settings and bundle identifier prepared; requires a macOS build host |

Graphics APIs, quality tiers and bundle identifiers are already configured
(`Assets/Settings/PC_RPAsset.asset` for desktop, `Mobile_RPAsset.asset` for Android/iOS,
quality levels `Mobile` and `PC`).

## 6. Repository layout (short version)

```
Assets/
  Art/           Models, Materials, Textures, Environments, UI, Audio, Animations, Prefabs
  Core/          Cross-scene presentation assets (UI/ and Environment/ placeholder folders)
  Documentation/ Conventions and the preserved original repository README
  Resources/     Config/GameConfiguration.asset and Config/UiTheme.asset (loaded at runtime)
  Scenes/        Bootstrap/, MainMenu/, Gameplay/
  Scripts/       Nexus.Runtime assembly - Core, Simulation (clock, driver, system graph,
                 sessions), Planet, UI, Camera, SaveSystem, ScriptableObjects, Settings,
                 Editor (Nexus.Editor assembly)
  Settings/      URP pipeline + renderer assets, volume profiles (PC and Mobile tiers)
  Tests/         EditMode/ and PlayMode/ (Nexus.Tests.* assemblies)
Packages/        manifest.json (pinned, verified versions)
ProjectSettings/ Unity 6.3 project settings (URP wired, 1920x1080 windowed default)
Tools/           Project metadata generator + static validator (not imported by Unity)
```

Full description: [ARCHITECTURE.md](ARCHITECTURE.md).

## 7. Documentation

| Document | Purpose |
| --- | --- |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Layers, service graph, event system, state machine, UI scaling, decisions |
| [DEVELOPMENT_ROADMAP.md](DEVELOPMENT_ROADMAP.md) | Phase plan, completed phases and entry criteria |
| [PHASE_01_REPORT.md](PHASE_01_REPORT.md) | Foundation: what was created, what was verified, what is still open |
| [PHASE_02_REPORT.md](PHASE_02_REPORT.md) | Simulation core: files, decisions, test inventory, verification steps |

## 8. Ground rules

* Never delete or rename folders without updating the assemblies, `Nexus.Core.Scenes.SceneNames`
  and `ProjectSettings/EditorBuildSettings.asset`.
* Keep `Assets/**/*.meta` in version control.
* Commit `.meta` for every new asset in the **same** commit as the asset.
* Do not add services to scenes; register them in `ApplicationBootstrapper`.
* `UnityEngine.SceneManagement` is used in exactly one place (`Core/Scenes/SceneLoader.cs`)
  plus the bootstrap/play-mode tests.
