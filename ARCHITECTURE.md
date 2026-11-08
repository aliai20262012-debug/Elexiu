# NEXUS CIVILIZATION — Architecture

> Phase 02 scope: the deterministic simulation core — lifecycle, clock, system graph, game
> sessions and the gameplay control surface. No planet generation, economy, technology or AI
> rules exist yet; those systems plug into the contracts described below.

## 1. Goals and constraints

| Goal | How it is met |
| --- | --- |
| Modular, testable architecture | 4 assemblies, constructor-injected services, one composition root, no `MonoBehaviour` singletons |
| Gameplay independent of presentation | Simulation publishes events; UI reads services and never drives simulation internals |
| Frame-rate independent, deterministic simulation | One fixed-step clock advanced at a single boundary; systems receive whole ticks, never `Time.deltaTime` |
| Safe, reversible lifecycle | Explicit `SimulationStatus` + rollback on failed initialization + idempotent shutdown |
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

Splitting the runtime into per-domain assemblies (Planet, Civilization, Economy…) is still
deliberately deferred. Phase 02 added the domain *contracts* (`ISimulationSystem`,
`SimulationContext`) inside `Nexus.Runtime`; the folders already match the intended split, so
the move is mechanical when there is more than one domain assembly (see
[DEVELOPMENT_ROADMAP.md](DEVELOPMENT_ROADMAP.md#phase-03--civilization-economy-technology)).

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
│  ISceneLoader · ISimulationManager · IGameSessionService     │
│  ISimulationSystemRegistry · GameManager                     │
└───────────────────────────┬──────────────────────────────────┘
                            │ resolved by
┌───────────────────────────▼──────────────────────────────────┐
│ Presentation (scenes / views)                                │
│  BootstrapController · MainMenuController + MainMenuView     │
│  GameplaySceneController · GameplayHud · SimulationControlBar│
│  SettingsPanel                                               │
└───────────────────────────┬──────────────────────────────────┘
                            │ drives (one frame-time boundary)
┌───────────────────────────▼──────────────────────────────────┐
│ Simulation core (no Unity types)                             │
│  SimulationClock · SimulationManager · SimulationSystemRegistry│
│  GameSessionService · domain systems (later phases)          │
└──────────────────────────────────────────────────────────────┘
```

Dependency direction is always *inwards*: views know interfaces, never concrete services,
and never each other. `GameManager` knows the simulation only through the event bus;
`GameplaySceneController` is the only object that reads `Time.deltaTime`.

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

Registration/disposal order (Phase 02):

```
eventBus → configuration → platform → platformConfiguration → save → settings
        → sceneLoader → ISimulationManager → IGameSessionService → GameManager
```

Disposal runs in reverse, so the session service ends the session and stops the simulation
*before* the simulation manager and its system registry are disposed.

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
| `Pause()` / `Resume()` | `Gameplay` / `Paused` | no scene load; mirrors the simulation pause |
| `ReturnToMainMenu()` | `Gameplay`, `Paused` | → `ReturningToMenu` → loads `MainMenu` → `MainMenu` |
| `ReportFailure(reason)` | any | → `Failed`, publishes the failure event |
| `Shutdown()` | most states | → `ShuttingDown` (terminal) |

`GameplaySceneController` keeps the *application* state and the *simulation* state aligned:
pausing the session also transitions the app to `Paused` (and back), so input/pause overlays in
later phases have a single source of truth. `GameState` describes the application shell;
`SimulationStatus` describes the simulation driver. They are related but not identical: a
gameplay scene can exist while the simulation is `Stopped` (session ended) or `Failed`.

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
| `SimulationTickEvent` | `SimulationManager` (per tick) | domain systems, planet view |
| `SimulationTickCompletedEvent` | `SimulationManager` (after all systems ran) | HUD time readout, telemetry, session progress |
| `SimulationStartedEvent` / `SimulationStoppedEvent` | `SimulationManager` | HUD, session service, future save-on-exit |
| `SimulationPausedEvent` / `SimulationResumedEvent` | `SimulationManager` | HUD, audio ducking later |
| `SimulationSpeedChangedEvent` | `SimulationManager` | HUD, AI budget scaling later |
| `SimulationSystemFailedEvent` | `SimulationSystemRegistry` | diagnostics overlay, telemetry |
| `GameSessionCreatedEvent` / `GameSessionEndedEvent` | `GameSessionService` | HUD, future save system, analytics |
| `SettingsChangedEvent` | `SettingsService` | audio mixer, quality UI |
| `NewGameRequestedEvent` / `ReturnToMenuRequestedEvent` | reserved for the input layer | game flow |

**Why two tick events?** `SimulationTickEvent` is the *domain* signal published at the start of
a tick (systems and views react to it). `SimulationTickCompletedEvent` is the *outcome* signal
published after every system ran and carries the elapsed time, the failure count and the
session id. They are not duplicates: one marks work, the other reports its result. The HUD
listens only to the completed event, so it can never display a tick that did not finish.

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
* Menu layout uses anchors in **reference units** plus one `VerticalLayoutGroup`; there are no
  fixed pixel coordinates in screen space and no fixed aspect assumptions.
* `UiLayoutProfile` (`Compact` / `Regular` / `Expanded`) drives density decisions:
  `SimulationControlBar` uses it for bar height, button widths and font sizes, and the
  gameplay header shrinks its title on `Compact` devices.

### Gameplay control surface

`SimulationControlBar` is the Phase 02 simulation UI: a bottom strip inside the safe area with

* `T+ HH:MM:SS` simulated-time readout (hours are not wrapped at 24),
* a lifecycle line (`RUNNING // 2x`, `PAUSED // 1x`, `INITIALIZATION FAILED`, `STOPPED`),
* the session line (`NEW COLONY // SEED 12345 // TICK 000042`),
* **PAUSE / RESUME** (label follows the state, disabled when no session is loaded),
* one button per configured speed level (1x / 2x / 4x by default), the active one highlighted
  in electric cyan,
* **RETURN TO MENU**, appended by `GameplayHud` into the same right-aligned action row.

The bar is pure presentation: buttons raise `PauseResumeRequested` / `SpeedRequested` events and
`GameplaySceneController` translates them into `IGameSessionService` calls. The controls are
laid out with a `HorizontalLayoutGroup` anchored to the right of the bar, so a wide desktop
window simply gains empty space between the clock and the buttons instead of stretching them.

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

Save/load is still **not** wired into the UI flow: no system writes a save, therefore
*Continue* correctly stays disabled.

Phase 02 prepared persistence by making `GameSession` a self-contained payload
(identifier, civilization name, deterministic seed, tick/time progress, end reason). A future
`ISaveService` implementation serializes that object plus domain snapshots; `TryCreateSession`
already accepts an explicit seed so a loaded session can be restored deterministically.

## 11. Simulation core (Phase 02)

### 11.1 Lifecycle

```
Stopped ──TryStartSimulation(session)──▶ Running ⇄ Paused
   ▲                                      │
   │                                      ├── Stop() ──▶ Stopped (clock + registry reset)
   │                                      └── init failure ──▶ Failed (rolled back)
   └──────────────────── Reset() / Dispose() ────────────────────┘
```

| Operation | Effect |
| --- | --- |
| `TryStartSimulation(context, out error)` | resets the clock, initializes the system graph, binds the session id, publishes `SimulationStartedEvent` (+ `SimulationPausedEvent` when configured to start paused) |
| `Pause()` | allowed only while running; clock stops accumulating, `SimulationPausedEvent` |
| `Resume()` | allowed only while paused, `SimulationResumedEvent` |
| `SetSpeed(multiplier)` | validated against `SimulationSpeed` (0.25–100), publishes `SimulationSpeedChangedEvent`; rejected if unchanged |
| `Tick(delta)` | converts frame time into zero or more ticks (see 11.2) |
| `Stop()` | shuts systems down in reverse order, publishes `SimulationStoppedEvent` with final values, resets clock and session id |
| `Reset()` | `Stop()` + reapply validated settings |
| `Dispose()` | `Stop()` + dispose the owned system registry |

Failure handling: if the graph cannot be ordered or a system throws during `Initialize`, the
registry shuts down every system it already initialized, `TryStartSimulation` returns `false`
with a readable reason, and the status becomes `Failed`. A retry is allowed from `Failed`.

### 11.2 Deterministic clock

`SimulationClock` is a pure C# fixed-step accumulator (`System` only, no Unity types):

* `Advance(delta)` clamps the frame delta to `MaxFrameDeltaSeconds` (default `0.25 s`) and
  multiplies it by the active speed, so a hitch cannot fast-forward the world.
* At most `MaxCatchUpTicksPerFrame` ticks (default `8`) are produced per frame; whole ticks
  beyond that budget are **discarded and counted** (`DroppedTicks`) instead of being replayed
  on later frames.
* `ElapsedSeconds` is derived as `TickCount × TickIntervalSeconds`, so simulated time can never
  drift away from the ticks systems observed.
* Pausing clears the partial accumulator; resuming cannot fire a tick accumulated before the
  pause. Nothing accumulates while paused.
* NaN/∞/negative deltas are ignored; invalid intervals, budgets and clamps are clamped instead
  of throwing.
* `PendingTickFraction` exposes 0–1 progress for optional presentation interpolation; simulation
  systems must not use it.

Determinism: the same sequence of `Advance`/`SetSpeed`/`SetPaused` calls yields the same tick
count and elapsed time on every platform. Unity frame time is read in exactly one place
(`GameplaySceneController.Update`), so frame rate changes cannot change simulation results.

### 11.3 System graph

```csharp
public interface ISimulationSystem
{
    string SystemId { get; }
    int ExecutionOrder { get; }
    IReadOnlyList<string> Dependencies { get; }
    void Initialize(SimulationInitializationContext context);
    void Tick(SimulationContext context);
    void Shutdown();
}
```

* `SimulationSystemBase` implements the lifecycle order for convenience (`Tick` is ignored
  before `Initialize`/after `Shutdown`); extend it unless custom semantics are needed.
* `SimulationSystemRegistry` resolves order with Kahn's algorithm: dependencies first, then
  ascending `ExecutionOrder`, then `SystemId` (ordinal) as a stable tie-break. Registration
  order never decides execution order.
* Missing dependencies, self-dependencies and cycles are reported as
  `SystemInitializationFailure` entries (phase `Ordering`) and abort initialization before any
  system is initialized.
* A system that throws during `Tick` is isolated: the failure is logged, published as
  `SimulationSystemFailedEvent`, its peers still tick, and after
  `MaxConsecutiveFailures` (3) consecutive failures the system is disabled for the session.
* `ShutdownAll` runs in reverse initialization order and is idempotent; shutdown exceptions are
  logged and do not prevent the remaining systems from shutting down.
* Systems receive `SimulationInitializationContext` (session id, seed, name, interval, speed)
  once per session and an immutable `SimulationContext` (tick, delta, elapsed, speed, session)
  per tick. They never read `Time.*`, never touch scenes and never construct services.

`SimulationSettings` is an immutable, validated timing value object
(`TickIntervalSeconds`, `MaxCatchUpTicksPerFrame`, `MaxFrameDeltaSeconds`, speed ladder,
default speed, start-paused). It is built from `GameConfiguration` through
`SimulationSettings.FromConfiguration`; every field is clamped, filtered and de-duplicated in
the constructor, and the speed ladder falls back to 1x / 2x / 4x when nothing usable is
configured.

### 11.4 Game sessions

`GameSession` is plain data: `SessionId`, `CivilizationName`, deterministic `Seed`,
`CreatedAtUtc`, `State` (`None`/`Active`/`Paused`/`Ended`), `TickCount`, `ElapsedSeconds`,
`EndedAtUtc`, `EndReason`. It contains no scene or presentation state and is the payload a
future save system will serialize.

`GameSessionService` coordinates session and simulation:

* `TryCreateSession(name, seed, out session, out error)` — sanitizes the name (trim, 48 char
  limit, configured fallback), generates an id (or uses the injected factory) and a seed when
  `seed == 0`, publishes `GameSessionCreatedEvent`, then starts the simulation. If the start
  fails, the half-created session is ended with `InitializationFailed` and the error is
  returned.
* `Pause()` / `Resume()` — only valid in the matching state; they drive the simulation and keep
  `GameSession.State` in sync.
* `SetSimulationSpeed(multiplier)` — requires an active session.
* `EndSession(reason)` — clears the current session, stops the simulation, publishes
  `GameSessionEndedEvent` with the final tick/time. Idempotent.
* Session progress is updated from `SimulationTickCompletedEvent`; if the simulation is stopped
  directly (shutdown, future campaign reset) the service closes the session instead of leaving
  it `Active` forever.
* Re-entering the gameplay scene while a session survived reuses it instead of orphaning it.

`GameplaySceneController` is the only bridge between Unity and the core: it creates the session,
feeds `Time.deltaTime` into the driver, mirrors pause into `GameState`, and ends the session on
`OnDestroy` (`SceneUnloaded`) or the menu button (`PlayerRequested`).

### 11.5 Timing guarantees

| Guarantee | Where it is enforced |
| --- | --- |
| Gameplay never reads frame time | Only `GameplaySceneController.Update` calls `Tick(Time.deltaTime)`; `SimulationContext` carries tick time |
| Long frames cannot freeze or fast-forward the app | `MaxFrameDeltaSeconds` clamp + `MaxCatchUpTicksPerFrame` budget |
| Backlog cannot accumulate without bound | Excess whole ticks are dropped and counted |
| Pause is exact | Accumulator cleared on pause; `Advance` returns 0 while paused |
| Speed changes are safe | Multipliers clamped to 0.25–100; same-speed changes ignored |
| Simulated time is consistent | `ElapsedSeconds = TickCount × Interval` |
| Start/stop is repeatable | `Stop` resets clock + registry; second session starts at tick 0 |
| Scene transitions cannot leak sessions | `OnDestroy` ends the session; `GameManager` rejects overlapping loads |

### 11.6 Configuration

New `GameConfiguration` fields (all optional, all validated on load and in `Sanitize()`):

| Field | Default | Meaning |
| --- | --- | --- |
| `_simulationTickIntervalSeconds` | `0.5` | Simulated seconds per tick |
| `_simulationSpeedMultipliers` | `1, 2, 4` | Selectable speed ladder |
| `_defaultSimulationSpeedMultiplier` | `1` | Speed at session start (snapped to a level) |
| `_simulationStartsPaused` | `false` | Start sessions paused |
| `_maxCatchUpTicksPerFrame` | `8` | Ticks a single frame may produce |
| `_maxFrameDeltaSeconds` | `0.25` | Frame-delta clamp |
| `_defaultCivilizationName` | `NEW COLONY` | Fallback session name |

## 12. Key decisions and trade-offs

| Decision | Rationale | Cost |
| --- | --- | --- |
| Build the UI from code (`UiFactory`) instead of prefabs | Scenes stay ~280–430 lines of YAML, no GUID breakage, layout is reviewable in C#, and tests can assert on widgets | Designers must edit C# to change layout until a prefab/theme pipeline exists |
| One static (`AppContext`) | Scene objects need *some* entry point to the injected graph | Documented and reset on play mode; everything else is instance based |
| One runtime assembly | Avoids multi-assembly overhead while there is one domain | Per-domain assemblies deferred to the phase that adds the second domain |
| Fixed step with a catch-up budget | Deterministic results and bounded per-frame work | A stutter can drop ticks; drops are counted rather than silently replayed |
| Speed as a clock multiplier | One implementation for pause/speed and every future system | Very high speeds coarsen the tick-to-real-time mapping (documented, clamped at 100x) |
| Session state kept separate from scene/UI | Save/load, tests and future campaign modes all reuse it | One extra indirection (`IGameSessionService`) for simple flows |
| Two tick events | Domain signal and outcome signal have different consumers | Slightly larger event catalogue, documented in §6 |
| `SimulationSettings` as an immutable struct | Invalid configuration is impossible to construct | Reconfiguration allocates a small value object |

## 13. Extension points (Phase 03+)

* **Domain systems** — implement `ISimulationSystem` (or extend `SimulationSystemBase`), register
  in `ApplicationBootstrapper.Compose` (or a future `GameplayFeatureRegistry`) and read only
  `SimulationContext`. Planet, economy, civilization, technology, diplomacy, environment and AI
  systems all use the same pipeline, including ordering and failure isolation.
* **Deterministic randomness** — `SimulationInitializationContext.Seed` is the agreed source for
  per-system PRNGs; no system should call `UnityEngine.Random`.
* **Save/load** — serialize `GameSession` + domain snapshots through `ISaveService`; restore by
  calling `TryCreateSession(name, seed, ...)` and replaying ticks if needed.
* **ISceneLoader** — replace with additive/addressable loading without touching `GameManager`.
* **UiTheme** — one asset controls every colour and font size (add a TMP font asset reference
  for brand typography).
* **ServiceRegistry** — add services in `ApplicationBootstrapper.Compose` and resolve them by
  interface; no scene changes required.
