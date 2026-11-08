# PHASE 01 COMPLETION REPORT — NEXUS CIVILIZATION

**Foundation & Project Setup**
Date: 2026-11-06 · Branch: `arena/317a56b6-elexiu`

> **Honesty statement first.** The environment used to build this phase has **no Unity
> Editor and no C# compiler installed** (`unity`, `dotnet`, `mono`, `mcs` are all absent;
> Unity cannot be installed from the allowed network). Therefore the Unity Test Framework
> suites were **written but not executed**, and no "the project compiles" claim is made.
> Everything that *can* be verified without Unity was verified with a purpose-built static
> validator — see [§5](#5-verification-that-was-actually-executed). The first thing to check
> on your machine is the compile + test run in [§7](#7-exact-instructions-to-open-and-run).

---

## 1. Files and folders created

### 1.1 Folder structure (47 folders under `Assets/`)

```
Assets/
  Art/          Animations, Audio, Environments, Materials, Models, Prefabs, Textures, UI
  Core/         UI, Environment                       (cross-scene presentation assets)
  Documentation/                                      (conventions + preserved original README)
  Resources/    Config/                               (runtime-loaded configuration assets)
  Scenes/       Bootstrap/, MainMenu/, Gameplay/
  Scripts/      Core/ (Bootstrap, Configuration, Events, Logging, Platform, Scenes, State)
                AI/, Camera/, Civilization/, Construction/, Diplomacy/, Economy/, Editor/,
                Planet/, SaveSystem/, ScriptableObjects/, Settings/, Simulation/,
                Technology/, UI/
  Settings/                                           (URP pipeline + renderer + volume assets)
  Tests/        EditMode/, PlayMode/
```

Two folders were added beyond the requested list, both justified and documented:

| Folder | Why |
| --- | --- |
| `Assets/Resources/Config/` | Configuration assets must be loadable at runtime without a scene or prefab reference (avoids fragile GUID links in scenes and lets tests run head-less). |
| `Tools/` (repository root, outside `Assets/`) | `generate_project_metadata.py` (deterministic `.meta`/build-settings generator) and `validate_project.py` (static project validation). Unity never imports them. |

### 1.2 Files

| Category | Count | Details |
| --- | --- | --- |
| C# sources | 67 (5,752 lines) | 50 runtime, 2 editor, 11 EditMode test files, 4 PlayMode test files |
| Unity scenes | 3 | `Bootstrap.unity`, `MainMenu.unity`, `Gameplay.unity` |
| ScriptableObject assets | 2 | `Resources/Config/GameConfiguration.asset`, `Resources/Config/UiTheme.asset` |
| URP assets | 7 | PC/Mobile pipeline + renderer, URP global settings, 3 volume profiles (Unity 6.3 URP template, unmodified) |
| Assembly definitions | 4 | `Nexus.Runtime`, `Nexus.Editor`, `Nexus.Tests.EditMode`, `Nexus.Tests.PlayMode` |
| Project settings | 23 files | URP wired, text serialisation, visible meta files, build settings |
| Package manifest | 1 | 37 pinned entries (6 packages + 31 engine modules) |
| Meta files | 131 | Deterministically generated, committed for every asset and folder |
| Documentation | 6 | `README.md`, `ARCHITECTURE.md`, `DEVELOPMENT_ROADMAP.md`, `PHASE_01_REPORT.md`, `Assets/Documentation/README.md`, preserved original repo README |
| Tooling | 2 | `Tools/validate_project.py`, `Tools/generate_project_metadata.py` |
| Repo hygiene | 2 | `.gitignore` (Unity standard + test/coverage + Python) and `.gitattributes` (line-ending normalisation, text/binary rules) |

The pre-existing `README.md` banner was **not deleted**: the original content is preserved at
`Assets/Documentation/original-repository-readme.md`. No existing file was overwritten except
`README.md`, which the phase required to become the project readme (its previous content is the
preserved copy), and `ProjectSettings/EditorBuildSettings.asset`, which only listed a sample
scene from a template.

## 2. Unity version and packages detected / configured

* **Editor version:** `6000.3.24f1` — Unity **6.3 LTS** (declared in
  `ProjectSettings/ProjectVersion.txt` with changeset `4e7b9b5b6244`).
  Chosen because 6.0 LTS reaches end of support 2026-10-16 and 6.3 LTS is supported until
  2027-12-04. Any newer `6000.3.x` patch opens the project with a standard upgrade prompt.
* **No Unity Editor is installed in the build environment**, so no editor version was
  "detected" — the version was selected deliberately and its package versions were verified
  against real 6.3 projects (URP `17.3.0`, Input System `1.20.0`, uGUI `2.0.0`,
  Test Framework `1.6.0`).

Packages in `Packages/manifest.json`:

| Package | Version | Purpose |
| --- | --- | --- |
| `com.unity.render-pipelines.universal` | 17.3.0 | Universal Render Pipeline |
| `com.unity.inputsystem` | 1.20.0 | Input System (Active Input Handling = **Both**) |
| `com.unity.ugui` | 2.0.0 | uGUI **and TextMeshPro** (TMP ships inside uGUI 2.x) |
| `com.unity.test-framework` | 1.6.0 | EditMode + PlayMode tests |
| `com.unity.ide.visualstudio` | 2.0.26 | IDE integration |
| `com.unity.ide.rider` | 3.0.40 | IDE integration |
| 31 × `com.unity.modules.*` | 1.0.0 | Engine modules (no unrelated third-party packages) |

Render pipeline configuration:

* `Graphics Settings → Scriptable Render Pipeline Settings` = `Assets/Settings/PC_RPAsset.asset`
* Quality level `Mobile` → `Mobile_RPAsset.asset` (index 0), quality level `PC` → `PC_RPAsset.asset` (index 1)
* `UniversalRenderPipelineGlobalSettings` asset assigned in `GraphicsSettings.asset`
* Colour space **Linear**, `1920×1080` windowed default, resizable window, run in background

Player/project identifiers: company `Nexus Civilization Studio`, product
`Nexus Civilization`, bundle id `com.nexuscivilization.game` (Standalone / Android / iPhone),
Android architectures ARM64, auto-rotation allowed, mobile-friendly splash colour.

## 3. Scenes created

| Scene | Path | Contents |
| --- | --- | --- |
| **Bootstrap** | `Assets/Scenes/Bootstrap/Bootstrap.unity` | Main Camera (URP) + `BootstrapController`. Shows the splash, composes services, loads the main menu, and displays a retryable error overlay on any failure. |
| **Main Menu** | `Assets/Scenes/MainMenu/MainMenu.unity` | Main Camera (URP) + `MainMenuController`. Builds the whole shell UI at runtime. |
| **Gameplay** | `Assets/Scenes/Gameplay/Gameplay.unity` | Main Camera (URP), Directional Light, `PlanetSystem` (`PlanetViewController`), `Gameplay` (`GameplaySceneController`). |

All three are registered in `ProjectSettings/EditorBuildSettings.asset` in that order
(Bootstrap = index 0).

## 4. Scripts implemented

### Core architecture

| File | Responsibility |
| --- | --- |
| `Core/Bootstrap/ApplicationBootstrapper.cs` | Composition root; `Boot()` (installs services, idempotent) and `Compose()` (testable, no side effects) |
| `Core/Bootstrap/BootstrapController.cs` | Bootstrap scene entry: splash → boot → main menu, timeout handling, retry |
| `Core/ServiceRegistry.cs` | Explicit DI container (register/resolve/dispose in reverse order) |
| `Core/AppContext.cs` | The single static access point to the installed registry; reset on play-mode start |
| `Core/GameManager.cs` | Thin coordinator: owns the state machine and scene flow only |
| `Core/State/GameState.cs`, `GameStateMachine.cs`, `InvalidStateTransitionException.cs` | Table-driven state machine with validated transitions |
| `Core/Events/*` | `IGameEvent`, `IEventBus`, `EventBus` (struct events, exception-isolated), 10 event types |
| `Core/Scenes/*` | `SceneNames`, `ISceneLoader`, `SceneLoadResult`, `SceneLoader` (the only class calling `UnityEngine.SceneManagement`) |
| `Core/Configuration/*` | `IConfigurationService`, `ConfigurationService` (Resources load + validated defaults) |
| `Core/Platform/*` | `IPlatformInfo`, `PlatformInfo` (pure `Classify`), `PlatformKind`, `UiLayoutProfile`, `PlatformConfiguration` |
| `Core/Logging/GameLog.cs` | Tagged log funnel |

### Feature areas

| Area | Files |
| --- | --- |
| Simulation placeholder | `Simulation/ISimulationManager.cs`, `SimulationManager.cs` (fixed-step accumulator + `SimulationTickEvent`), `GameplaySceneController.cs` |
| Save system | `SaveSystem/ISaveService.cs`, `JsonFileSaveService.cs`, `NullSaveService.cs`, `SaveData.cs` (schema-versioned), `SaveSlotInfo.cs` |
| Settings | `Settings/ISettingsService.cs`, `SettingsService.cs` (quality + master volume, persisted) |
| Configuration | `ScriptableObjects/GameConfiguration.cs`, `ScriptableObjects/UiTheme.cs` |
| Responsive UI | `UI/ResponsiveCanvas.cs`, `UI/SafeAreaFitter.cs`, `UI/UiFactory.cs`, `UI/UiLabel.cs`, `UI/UiButton.cs`, `UI/UiTextBackend.cs` |
| Views | `UI/MainMenuController.cs`, `UI/MainMenuView.cs`, `UI/SettingsPanel.cs`, `UI/BootstrapLoadingView.cs`, `UI/GameplayHud.cs`, `UI/ErrorOverlay.cs` |
| Planet / camera placeholders | `Planet/PlanetViewController.cs`, `Camera/CameraRig.cs` |
| Editor tooling | `Editor/NexusEditorMenu.cs` (open scenes, create config assets), `Editor/NexusProjectValidator.cs` (menu + `-executeMethod` batch validation) |

Empty-but-created domain folders (`Civilization`, `Economy`, `Diplomacy`, `AI`, `Technology`,
`Construction`) intentionally contain no code: Phase 01 must not implement unfinished gameplay
systems. They are kept in Git through their folder `.meta` files.

## 5. Verification that was actually executed

### 5.1 Static project validation — **executed, passing**

`python3 Tools/validate_project.py --verbose` (run in the sandbox, not in Unity):

| Check | Result |
| --- | --- |
| Unity YAML parse (3 scenes, 9 `.asset`, 23 project settings) | ✅ all parse |
| Every asset/folder has a `.meta`, no orphan metas, no duplicate GUIDs | ✅ 0 errors |
| All GUID references inside the project resolve | ✅ 16 internal references |
| Scene integrity (component ↔ GameObject ↔ Transform hierarchy, missing components) | ✅ 0 errors |
| Scene MonoBehaviour script GUIDs resolve to project scripts | ✅ all 4 resolve |
| Every serialized field in a scene exists on the target script | ✅ 0 errors |
| Build settings list Bootstrap → MainMenu → Gameplay with matching GUIDs | ✅ |
| URP wiring: `m_CustomRenderPipeline` + both quality levels point at project URP assets | ✅ 2 quality levels |
| C# structure: 67 files, brace/parenthesis balance, namespace present, file name ↔ type, no duplicate public/internal types | ✅ 82 types, no duplicates |
| Assembly definitions: valid JSON, 4 required assemblies present, references resolvable | ✅ |
| Package manifest: valid JSON, required packages pinned | ✅ 37 entries |
| Required files present (scenes, config assets, URP assets, docs, .gitignore) | ✅ |
| GUID references owned by Unity packages | ℹ️ 161 (only resolvable inside the editor) |

Final line of the run: **`0 error(s), 0 warning(s)`**.

### 5.2 Automated tests — **written, NOT executed**

* **EditMode: 65 tests** in 10 fixtures — `GameStateMachineTests` (6), `EventBusTests` (7),
  `ServiceRegistryTests` (7), `ConfigurationTests` (5), `GameManagerFlowTests` (11),
  `SaveServiceTests` (7), `PlatformTests` (8), `UiScalingTests` (6),
  `ApplicationBootstrapperTests` (4), `BuildSettingsTests` (4).
* **PlayMode: 5 tests** in 3 fixtures — `BootstrapFlowTests` (2), `MainMenuSceneTests` (2),
  `SceneNavigationTests` (1).

They cover exactly the requested areas: application initialisation
(`ApplicationBootstrapperTests`, `BootstrapFlowTests`), game state transitions
(`GameStateMachineTests`, `GameManagerFlowTests`), configuration loading
(`ConfigurationTests`), and basic scene navigation (`SceneNavigationTests`,
`BuildSettingsTests`, `MainMenuSceneTests`).

**Why they were not executed:** the environment has no Unity Editor, no Test Framework
runner and no C# compiler, and Unity cannot be installed from the sandbox's allowed network
hosts. No result is claimed for them. Expected outcome on your machine is described in §7.

### 5.3 Manual API verification (against Unity's published reference source)

Because a compiler was unavailable, the risky engine APIs were checked against
`Unity-Technologies/UnityCsReference` and the package sources:

| API assumption | Verification |
| --- | --- |
| `UnityEngine.Device.SystemInfo.deviceType` exists | ✅ confirmed in `Runtime/Export/Device/SystemInfo.cs` |
| `UnityEngine.DeviceType` has **no** `Tablet` member | ✅ confirmed; the code and its test were corrected to use size heuristics instead of a non-existent enum value |
| `RenderPipelineAsset.defaultMaterial` is public and virtual | ✅ confirmed |
| `TMP_Settings.defaultFontAsset` is **static** | ✅ confirmed in uGUI sources; the code uses the static accessor |
| `InputSystemUIInputModule` assigns default UI actions in `OnEnable` when added at runtime | ✅ confirmed in `InputSystemUIInputModule.cs` (`if (HasNoActions()) AssignDefaultActions();`) |
| URP script GUIDs written into the copied URP assets are the package's own | ✅ cross-checked against real Unity 6.3 URP projects |
| Package versions valid for 6.3 | ✅ cross-checked against three public 6000.3 projects' manifests |
| Assembly names used in the asmdefs | ✅ `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner` confirmed in the packages' own `.asmdef` files |
| Cross-file member/property names (e.g. `_view.NewGameButton.Button`, `AppContext.IsReady`) | ✅ scripted cross-reference: no unresolved project-type members |
| `using` directives vs. the engine types each file touches | ✅ scripted check: no missing `using` (3 reported hits were property accesses, not types) |

## 6. Remaining errors and limitations

### Blocking issues

**None known to the static checks.** The following have *not* been proven because they require
Unity: (a) the C# actually compiles, (b) the scenes open without missing-reference errors,
(c) the Test Framework suites pass, (d) the shell renders as designed.

### Known limitations (deliberate, tracked in the roadmap)

1. **TMP Essential Resources are not committed.** Until `Window → TextMeshPro → Import TMP
   Essential Resources` is run, the UI renders with the legacy-text fallback (readable, but
   not the intended typography). The fallback is by design and unit-visible via `UiTextBackend`.
2. **No `.inputactions` asset.** UI input uses the Input System's default UI actions
   (`AssignDefaultActions`). A gameplay action map arrives in Phase 02.
3. **No save is written yet**, so *Continue* is permanently disabled in Phase 01 (as required).
4. **Gameplay is a placeholder**: rotating sphere, tick counter, return-to-menu. No simulation
   domain, no planet data, no economy.
5. **Build modules are not installable from the repo** (see §7 / README §5). Android/iOS
   settings are pre-configured but the corresponding modules must exist in your editor install.
6. **Editor/PlayMode validation of the URP assets** cannot be performed here; the assets are
   the unmodified Unity 6.3 URP template set (PC + Mobile), which is the safest possible
   starting point.
7. `Tools/generate_project_metadata.py` must be re-run if assets are added outside Unity
   (otherwise Unity generates its own `.meta` files, which is also fine — just don't do both
   for the same file).
8. Retrying a failed bootstrap disposes and rebuilds services but leaves the previous
   `[SceneLoader]` host object alive (harmless, inert).

## 7. Exact instructions to open and run

1. Install **Unity 6000.3.24f1** (or newer 6000.3 LTS) with:
   * `Windows Build Support (IL2CPP)` (mandatory for the primary target)
   * `Android Build Support` + `OpenJDK` + `Android SDK & NDK Tools` (for tablets/phones)
   * `iOS Build Support` (only on macOS, only when you start the iOS build)
2. Unity Hub → **Add project from disk** → the repository root.
3. Open the project and wait for the package import (URP 17.3.0, Input System 1.20.0,
   uGUI 2.0.0, Test Framework 1.6.0).
4. `Window → TextMeshPro → Import TMP Essential Resources` (one time, recommended).
5. `Nexus → Validate Project` — expect `Validation passed` in the Console.
6. Open `Assets/Scenes/Bootstrap/Bootstrap.unity` (or `Nexus → Open Bootstrap Scene`) → **Play**.
   Expected: splash → main menu with `NEW GAME / CONTINUE(disabled) / SETTINGS / EXIT`.
   `NEW GAME` opens the gameplay placeholder; `RETURN TO MENU` comes back.
7. Tests: `Window → General → Test Runner` → **Run All** for EditMode and PlayMode.
   Headless:
   ```bash
   Unity -batchmode -projectPath . -runTests -testPlatform EditMode \
         -testResults ./TestResults/editmode.xml -logFile -
   Unity -batchmode -projectPath . -runTests -testPlatform PlayMode \
         -testResults ./TestResults/playmode.xml -logFile -
   ```
8. Static validation (no Unity required): `python3 Tools/validate_project.py --verbose`.

### If step 3 or 7 reports a problem

* *Package resolution error* → the pinned version is not available for your editor build;
  open `Packages/manifest.json` and let Package Manager bump URP/Input System/uGUI one minor
  version, or use exactly `6000.3.24f1`.
* *Missing TMP font / invisible text* → step 4 was skipped; the legacy fallback should still
  render text.
* *PlayMode tests fail on scene load* → confirm `ProjectSettings/EditorBuildSettings.asset`
  lists Bootstrap, MainMenu, Gameplay (the `BuildSettingsTests` fixture asserts this).

## 8. Git

The repository was already initialised (`git` history preserved, existing remote untouched).
No existing file was deleted. A Unity `.gitignore` was added, `.meta` files are tracked, and
the work is committed on the session branch `arena/317a56b6-elexiu`.

**Phase 01 is complete pending the on-machine compile + test run in §7; Phase 02 is not
started and waits for approval.**
