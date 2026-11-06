# NEXUS CIVILIZATION

**Phase 01 — Foundation & Project Setup**

A cross-platform (Windows PC, Android tablets, Android phones, iOS-ready) strategy game
built with Unity 6.3 LTS, Universal Render Pipeline, the Input System package and
TextMeshPro.

This repository is currently a **verified technical foundation**, not a game: it contains the
application architecture, project configuration, folder structure, the Boot → Main Menu →
Gameplay shell, and the automated test suites. Gameplay systems start in Phase 02.

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
  `FOUNDATION BUILD · PHASE 01`, the buttons **NEW GAME / CONTINUE / SETTINGS / EXIT**,
  a build stamp and a footer with the detected platform. *Continue stays disabled until a
  save exists; Exit is hidden on mobile platforms.*
* **Gameplay** — a rotating placeholder planet, the simulation tick counter and a
  **RETURN TO MENU** button. No gameplay rules are implemented yet.

## 4. Running the tests

From the editor: `Window → General → Test Runner`.

* **EditMode** (`Assets/Tests/EditMode`) — 65 tests covering the state machine, event bus,
  service registry, configuration, game flow with a fake scene loader, save service,
  platform classification, UI scaling maths and build settings.
* **PlayMode** (`Assets/Tests/PlayMode`) — 5 tests covering bootstrap initialisation,
  scene navigation and the main-menu presentation.

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
  Scripts/       Nexus.Runtime assembly - Core, Simulation, Planet, UI, Camera, SaveSystem,
                 ScriptableObjects, Settings, Editor (Nexus.Editor assembly)
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
| [DEVELOPMENT_ROADMAP.md](DEVELOPMENT_ROADMAP.md) | Phase plan and Phase 02 entry criteria |
| [PHASE_01_REPORT.md](PHASE_01_REPORT.md) | What was created, what was verified, what is still open |

## 8. Ground rules

* Never delete or rename folders without updating the assemblies, `Nexus.Core.Scenes.SceneNames`
  and `ProjectSettings/EditorBuildSettings.asset`.
* Keep `Assets/**/*.meta` in version control.
* Commit `.meta` for every new asset in the **same** commit as the asset.
* Do not add services to scenes; register them in `ApplicationBootstrapper`.
* `UnityEngine.SceneManagement` is used in exactly one place (`Core/Scenes/SceneLoader.cs`)
  plus the bootstrap/play-mode tests.
