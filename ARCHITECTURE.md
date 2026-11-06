# NEXUS CIVILIZATION — Architecture

> Phase 01 scope: application architecture and the playable shell. No gameplay rules,
> no planet simulation, no economy. Everything below is implemented unless explicitly marked
> *placeholder*.

## 1. Goals and constraints

| Goal | How it is met |
| --- | --- |
| Modular, testable architecture | 4 assemblies, constructor-injected services, one composition root, no `MonoBehaviour` singletons |
| Gameplay independent of presentation | Simulation layer publishes events; UI reads services and never drives simulation internals |
| Cross-platform (PC / Android tablet / Android phone / iOS) | Platform detection feeds `PlatformConfiguration`, two URP quality tiers, responsive canvas + safe-area handling |
| Small, reviewable scenes | Scenes contain one or two `MonoBehaviour` entry points; UI is built at runtime from code |
| Git friendly | Force Text serialisation, visible meta files, deterministic `.meta` GUIDs, no binaries |

## 2. Assemblies

| Assembly | Location | References | Responsibility |
| --- | --- | --- | --- |
| `Nexus.Runtime` | `Assets/Scripts` | `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI` | All runtime code: core services, simulation, planet, UI, camera, save system |
| `Nexus.Editor` | `Assets/Scripts/Editor` | `Nexus.Runtime` | Editor menu + project validation (never shipped) |
| `Nexus.Tests.EditMode` | `Assets/Tests/EditMode` | `Nexus.Runtime`, test runners | Fast, editor-only unit tests |
| `Nexus.Tests.PlayMode` | `Assets/Tests/PlayMode` | `Nexus.Runtime`, test runners | Scene-level integration tests |

Splitting the runtime into per-domain assemblies (Planet, Civilization, Economy…) is
deliberately deferred: with one gameplay domain present, extra assemblies would only add
friction. The folder/namespace layout already matches the intended split, so the change is
mechanical (see [DEVELOPMENT_ROADMAP.md](DEVELOPMENT_ROADMAP.md#phase-02--simulation-domain)).

## 3. Layered structure

```
┌──────────────────────────────────────────────────────────────┐
│ Bootstrap (composition root)                                 │
│  ApplicationBootstrapper → ServiceRegistry → AppContext      │
└───────────────────────────┬──────────────────────────────────┘
                            │ installs
┌───────────────────────────▼──────────────────────────────────┐
│ Application services                                         │
│  IEventBus · IConfigurationService · IPlatformInfo           │
│  PlatformConfiguration · ISettingsService · ISaveService     │
│  ISceneLoader · ISimulationManager · GameManager             │
└───────────────────────────┬──────────────────────────────────┘
                            │ resolved by
┌───────────────────────────▼──────────────────────────────────┐
│ Presentation (scenes / views)                                │
│  BootstrapController · MainMenuController + MainMenuView     │
│  GameplaySceneController · GameplayHud · SettingsPanel       │
└──────────────────────────────────────────────────────────────┘
```

Dependency direction is always *inwards*: views know interfaces, never concrete services,
and never each other. `GameManager` knows the simulation only through the event bus.

## 4. Composition root and the single static

`ApplicationBootstrapper.Boot()` creates every service exactly once, in dependency order,
registers it against an abstraction in `ServiceRegistry`, and installs the registry in
`AppContext`.

* `ServiceRegistry` — tiny explicit container (`Register<T>`, `Get<T>`, `TryGet<T>`,
  `IsRegistered<T>`), disposes `IDisposable` services in reverse registration order, and
  rejects duplicate registrations.
* `AppContext` — the **only** static mutable state in the codebase. It exists so that scene
  `MonoBehaviour`s (which Unity instantiates) can reach the injected graph. It is reset on
  `SubsystemRegistration` so disabled domain reloads cannot leave stale services behind.

Anything not created here cannot exist at runtime: scenes never call `new` on a service.

### Graceful degradation

If a scene is played directly from the editor (for example `MainMenu.unity`), the scene
controller calls `ApplicationBootstrapper.Boot()` itself and moves the state machine to the
state it would have had. If the bootstrap scene is re-entered while the application is
already running, `Boot()` returns the existing registry (idempotent) and the bootstrap
navigates through `ISceneLoader`.

## 5. State machine and scene flow

`GameStateMachine` is table driven; illegal transitions either return `false`
(`TryTransitionTo`) or throw `InvalidStateTransitionException` (`TransitionTo`).
`GameManager` owns the machine and translates intents into scene loads.

```
Uninitialized → Booting → MainMenu ⇄ LoadingGameplay → Gameplay ⇄ Paused
                              ↑                            │
                              └──── ReturningToMenu ◄───────┘
Any state → Failed (recoverable) → Booting / ShuttingDown
```

| Intent | Allowed from | Effect |
| --- | --- | --- |
| `Initialize()` | `Uninitialized` | → `Booting` |
| `EnterMainMenu()` | `Booting`, `ReturningToMenu` | loads `MainMenu` → `MainMenu` |
| `StartNewGame()` | `MainMenu` | → `LoadingGameplay` → loads `Gameplay` → `Gameplay` |
| `Pause()` / `Resume()` | `Gameplay` / `Paused` | no scene load |
| `ReturnToMainMenu()` | `Gameplay`, `Paused` | → `ReturningToMenu` → loads `MainMenu` → `MainMenu` |
| `ReportFailure(reason)` | any | → `Failed`, publishes the failure event |
| `Shutdown()` | most states | → `ShuttingDown` (terminal) |

Scene identifiers live in `Nexus.Core.Scenes.SceneNames` and are additionally configurable in
`GameConfiguration`, so renaming a scene is a two-line change.

## 6. Event layer

`IEventBus` is a typed publish/subscribe channel over `struct` events (`IGameEvent`), so
publishing does not allocate and handlers cannot mutate payloads. A throwing subscriber is
logged and isolated; other subscribers still run.

| Event | Publisher | Typical subscriber |
| --- | --- | --- |
| `GameStateChangedEvent` | `GameManager` | HUD, menus, analytics later |
| `SceneLoadRequestedEvent` / `SceneLoadCompletedEvent` | `SceneLoader` | loading UI, telemetry |
| `ApplicationInitializedEvent` / `ApplicationInitializationFailedEvent` | bootstrap / `GameManager` | diagnostics overlay |
| `SimulationTickEvent` | `SimulationManager` | planet view, future domain systems |
| `SettingsChangedEvent` | `SettingsService` | audio mixer, quality UI |
| `NewGameRequestedEvent` / `ReturnToMenuRequestedEvent` | reserved for Phase 02 input layer | game flow |

## 7. Text rendering fallback

TextMeshPro is the intended text renderer, but its *Essential Resources* are imported by the
editor, not by the repository. `UiTextBackend` resolves `TMP_Settings.defaultFontAsset` at
runtime; when it is missing, `UiFactory` builds legacy `UnityEngine.UI.Text` labels with the
built-in `LegacyRuntime.ttf` font instead of rendering invisible labels. Import TMP Essential
Resources to get the intended typography (widened letter spacing, SDF quality).

`UiLabel` hides the difference from the views.

## 8. Responsive UI

* `CanvasScaler` — *Scale With Screen Size*, `Match Width Or Height`.
* `ResponsiveCanvas` — recomputes the reference resolution and the match factor when the
  screen changes:
  * portrait (`aspect < 1`) → reference `1080×1920`, match `0` (width)
  * `1.0 – 1.5` → match `0.4`
  * `1.5 – 2.1` (16:9 / 16:10) → match `0.5`
  * `≥ 2.1` (21:9, 32:9) → reference `1920×1080`, match `1` (height)
* `SafeAreaFitter` — anchors a full-screen container inside `Screen.safeArea` (notches,
  punch-holes, rounded corners) and re-applies on rotation.
* Layout uses anchors/`RectTransform` sizes in **reference units** plus one
  `VerticalLayoutGroup` for the menu stack; there are no fixed pixel coordinates in screen
  space and no fixed aspect assumptions.
* `UiLayoutProfile` (`Compact` / `Regular` / `Expanded`) is available for density decisions
  per device class.

## 9. Platform strategy

`PlatformInfo.Detect()` classifies the device from `Application.platform`, `Screen.dpi` and
`DeviceType`; `Classify(runtimePlatform, deviceType, screenWidthInches)` is a pure function so
tablets/phones/desktop are unit tested without hardware.

`PlatformConfiguration.For(platform)` then maps the classification to concrete budgets:

| Device | Target FPS | Quality level | URP asset | Layout |
| --- | --- | --- | --- | --- |
| Windows / macOS / Linux | 120 | 1 (`PC`) | `PC_RPAsset.asset` | `Expanded` |
| Android / iOS tablet | 60 | 0 (`Mobile`) | `Mobile_RPAsset.asset` | `Regular` |
| Android / iOS phone | 60 | 0 (`Mobile`) | `Mobile_RPAsset.asset` | `Compact` |

`SupportsQuit` is false on mobile, which is why the Exit button is hidden there.

## 10. Persistence

`ISaveService` is the persistence boundary. `JsonFileSaveService` writes JSON into
`Application.persistentDataPath/Saves`, validates slot ids, guards every IO call and degrades to
"no save" instead of throwing. `NullSaveService` covers platforms without writable storage.
`SaveData.SchemaVersion` is the migration hook for future gameplay payloads.

Save/load is **not** wired into the UI flow yet: no system writes a save, therefore
*Continue* correctly stays disabled in Phase 01.

## 11. Simulation placeholder

`SimulationManager` is a fixed-step accumulator (default `0.5 s`, clamped ≥ `0.02 s`,
max 8 catch-up ticks per frame). It publishes `SimulationTickEvent` and contains zero domain
logic. Phase 02 registers real systems against that event.

## 12. Key decisions and trade-offs

| Decision | Rationale | Cost |
| --- | --- | --- |
| Build the UI from code (`UiFactory`) instead of prefabs | Scenes stay 280–430 lines of YAML, no GUID breakage, layout is reviewable in C#, and tests can assert on widgets | Designers must edit C# to change layout until a prefab/theme pipeline exists (Phase 02) |
| One static (`AppContext`) | Scene objects need *some* entry point to the injected graph | Documented and reset on play mode; everything else is instance based |
| One runtime assembly in Phase 01 | Avoids multi-assembly overhead while there is one domain | Per-domain assemblies in Phase 02 |
| Editor-generated runtime UI, no TMP font asset committed | No binary assets in the repo, no font atlas to regenerate | Falls back to legacy text until TMP Essentials are imported |
| Package versions pinned to the verified set for 6000.3 | Reproducible clones | Upgrading the editor requires a deliberate manifest bump |
| URP PC/Mobile tiers shipped verbatim from the Unity 6.3 URP template | Known-good renderer data with correct shader GUIDs | Assets must be re-created (not hand edited) when URP changes major version |

## 13. Extension points (Phase 02+)

* `IDomainSystem` + `SimulationTickEvent` — register planet/economy/AI systems.
* `ISceneLoader` — replace with additive/addressable loading without touching `GameManager`.
* `ISaveService` — swap the file store for cloud saves.
* `UiTheme` — one asset controls every colour and font size (add a TMP font asset reference
  for brand typography).
* `ServiceRegistry` — add services in `ApplicationBootstrapper.Compose` and resolve them by
  interface; no scene changes required.
