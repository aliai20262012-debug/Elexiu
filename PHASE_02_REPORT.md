# PHASE 02 COMPLETION REPORT — NEXUS CIVILIZATION

**Phase**: 02 — Core Game Architecture
**Branch**: `arena/317a56b6-elexiu` (branched from `main`; Phase 01 preserved in history)
**Editor target**: Unity `6000.3.24f1` (Unity 6.3 LTS), URP 17.3.0, Input System 1.20.0
**Build stamp**: `0.2.0-phase02` / `CORE SIMULATION`

---

## 1. Result summary

Phase 02 adds a deterministic, frame-rate independent simulation core on top of the approved
Phase 01 foundation:

* a fixed-step clock with frame clamping, bounded catch-up and dropped-tick accounting,
* an explicit simulation lifecycle (`Stopped / Running / Paused / Failed`) with safe start,
  pause, resume, speed change, stop, reset and dispose,
* an ordered, dependency-aware system graph with rollback on failed initialization, failure
  isolation during ticks and reverse-order shutdown,
* a game-session service (identity, deterministic seed, progress, termination) that
  coordinates the simulation and publishes lifecycle events,
* a minimal, responsive gameplay control surface (clock, status, pause/resume, speed ladder,
  session line, return to menu),
* validated configuration for every timing parameter,
* 97 new EditMode tests and 3 new PlayMode tests (authored, not executed here).

Phase 01 systems were extended, not replaced. Every GUID, scene, URP asset, package version and
existing test was preserved; the only Phase 01 API change is the widened
`ISimulationManager`/`GameplayHud` surface (see §9).

**Static validation: 0 errors, 0 warnings** (`python3 Tools/validate_project.py`).

---

## 2. Files

All paths are relative to the repository root. `.meta` files were generated deterministically
for every new source file (21 new `.meta` files, no GUID changes to existing assets).

### 2.1 New runtime code (15 files)

| File | Responsibility |
| --- | --- |
| `Assets/Scripts/Simulation/SimulationClock.cs` | Pure C# fixed-step accumulator, clamping, catch-up budget, drop accounting, pause/speed, deterministic elapsed time |
| `Assets/Scripts/Simulation/SimulationSpeed.cs` | Speed domain rules: valid range 0.25–100, clamping, level sanitation, nearest level, invariant labels |
| `Assets/Scripts/Simulation/SimulationStatus.cs` | `Stopped / Running / Paused / Failed` lifecycle enum |
| `Assets/Scripts/Simulation/SimulationSettings.cs` | Immutable validated timing value object (+ `FromConfiguration`) |
| `Assets/Scripts/Simulation/SimulationContext.cs` | Immutable per-tick context handed to systems |
| `Assets/Scripts/Simulation/SimulationInitializationContext.cs` | Per-session initialization context (session id, seed, name, interval, speed) |
| `Assets/Scripts/Simulation/ISimulationSystem.cs` | Domain system lifecycle contract |
| `Assets/Scripts/Simulation/SimulationSystemBase.cs` | Convenience base class enforcing lifecycle order |
| `Assets/Scripts/Simulation/SystemInitializationReport.cs` | Structured failure report + failure struct |
| `Assets/Scripts/Simulation/ISimulationSystemRegistry.cs` | Registration, ordering, init, tick, shutdown contract |
| `Assets/Scripts/Simulation/SimulationSystemRegistry.cs` | Kahn ordering, cycle/missing-dependency detection, rollback, failure isolation, auto-disable, reverse shutdown |
| `Assets/Scripts/Simulation/GameSession.cs` | Session payload + `GameSessionState` / `SessionEndReason` |
| `Assets/Scripts/Simulation/IGameSessionService.cs` | Session lifecycle contract |
| `Assets/Scripts/Simulation/GameSessionService.cs` | Session/simulation coordination, progress sync, event ordering, idempotent termination |
| `Assets/Scripts/UI/SimulationControlBar.cs` | Responsive control bar: clock, status, session line, pause/resume, speed buttons, action row |

### 2.2 Rewritten runtime code (3 files)

| File | Change |
| --- | --- |
| `Assets/Scripts/Simulation/SimulationManager.cs` | Was a 90-line accumulator; now the lifecycle driver (clock + graph + events), with validated settings and clean stop/reset/dispose |
| `Assets/Scripts/Simulation/GameplaySceneController.cs` | Creates the session through `IGameSessionService`, feeds frame time once per frame, mirrors pause into `GameState`, wires HUD intents, ends the session on teardown, exposes test hooks |
| `Assets/Scripts/UI/GameplayHud.cs` | Rebuilt around `SimulationControlBar`; keeps `SetTick`/`SetStatus` and adds time/status/session updates |

### 2.3 Modified runtime code (4 files + 1 asset)

| File | Change |
| --- | --- |
| `Assets/Scripts/Simulation/ISimulationManager.cs` | Added status, elapsed time, speed, settings, system registry, session id, validated start, speed control, reset/dispose |
| `Assets/Scripts/Core/Events/GameEvents.cs` | Added nine Phase 02 events (started, stopped, paused, resumed, speed changed, tick completed, system failed, session created, session ended); `SimulationTickEvent` retained |
| `Assets/Scripts/Core/Bootstrap/ApplicationBootstrapper.cs` | Registers `IGameSessionService`, configures the simulation from `GameConfiguration`, keeps disposal order session → simulation |
| `Assets/Scripts/ScriptableObjects/GameConfiguration.cs` | Added validated simulation settings (speed ladder, default speed, start paused, catch-up budget, frame clamp, default civilization name); version/channel bumped |
| `Assets/Resources/Config/GameConfiguration.asset` | New serialized values, `0.2.0-phase02`, `CORE SIMULATION` |

### 2.4 Tests (6 new files, 1 modified)

| File | New tests |
| --- | --- |
| `Assets/Tests/EditMode/SimulationClockTests.cs` | 15 |
| `Assets/Tests/EditMode/SimulationSystemRegistryTests.cs` | 21 |
| `Assets/Tests/EditMode/SimulationManagerTests.cs` | 28 |
| `Assets/Tests/EditMode/GameSessionServiceTests.cs` | 17 |
| `Assets/Tests/EditMode/SimulationSettingsTests.cs` | 15 |
| `Assets/Tests/PlayMode/GameplaySimulationTests.cs` | 3 `[UnityTest]` |
| `Assets/Tests/EditMode/ApplicationBootstrapperTests.cs` | +1 (and service count 9 → 10) |

### 2.5 Documentation

`ARCHITECTURE.md` (new §11 + event catalogue + decisions/extension points),
`DEVELOPMENT_ROADMAP.md` (Phase 02 complete, later phases renumbered),
`README.md` (phase, controls, test counts, layout), this report.

---

## 3. Architecture decisions

| # | Decision | Rationale |
| --- | --- | --- |
| 1 | **Frame time enters exactly once**: `GameplaySceneController.Update` → `SimulationManager.Tick(Time.deltaTime)` | A single boundary makes frame-rate independence auditable and testable |
| 2 | **Pure C# clock** (no `Time.*`, no `UnityEngine` types) | Deterministic in EditMode tests, reusable in headless/replay tools |
| 3 | **Elapsed time derived from ticks** (`ticks × interval`) instead of summing floats | Simulated time can never drift from what systems observed |
| 4 | **Clamp + bounded catch-up + drop** instead of replaying backlog | A frame hitch can neither freeze the app nor make the world run in bursts; drops are counted |
| 5 | **Pause clears the accumulator** | Resuming cannot fire a tick that was accumulated before the pause |
| 6 | **Dependency-first ordering with stable tie-breaks** (`order`, then `SystemId`) | Deterministic order across platforms and runs; registration order is irrelevant |
| 7 | **Fail fast with rollback** on ordering/initialization errors | No half-initialized graph can ever tick |
| 8 | **Isolate tick exceptions and auto-disable after 3 failures** | One broken future system cannot crash the session; failures are visible via events |
| 9 | **Sessions are plain data**, owned by a service | Save/load, replay and tests reuse the same payload; UI never owns session state |
| 10 | **Session service orchestrates; manager stays session-agnostic** | No circular dependency; manager can be unit tested without sessions |
| 11 | **Two tick events** (`SimulationTickEvent` = domain signal, `SimulationTickCompletedEvent` = outcome) | Different consumers, no duplicate definition; HUD cannot display an unfinished tick |
| 12 | **Immutable validated `SimulationSettings`** | Invalid designer input is impossible to construct; configuration cannot break the clock |
| 13 | **UI raises intents, controller acts** | The control bar is testable and reusable; no widget touches the clock |
| 14 | **Pause mirrors into `GameState.Paused`** | One visible application state for input/overlay work in later phases |

---

## 4. Simulation lifecycle and timing

```
Stopped ──TryStartSimulation(session)──▶ Running ⇄ Paused
   ▲                                      │
   │                                      ├── Stop() ──▶ Stopped (clock + registry reset)
   │                                      └── init failure ──▶ Failed (rolled back)
   └──────────────────── Reset() / Dispose() ────────────────────┘
```

Defaults (from `GameConfiguration.asset`):

| Parameter | Value | Effect |
| --- | --- | --- |
| Tick interval | `0.5 s` | Two ticks per simulated second |
| Speed ladder | `1x, 2x, 4x` | Real-time multipliers |
| Default speed | `1x` | Speed at session start |
| Start paused | `false` | Sessions start running |
| Catch-up budget | `8` ticks/frame | Bounds per-frame work |
| Frame clamp | `0.25 s` | A stall cannot fast-forward the world |
| Default civilization | `NEW COLONY` | Fallback session name |

Determinism and safety rules (all enforced in `SimulationClock` / `SimulationManager`):

* invalid deltas (`0`, negative, `NaN`, `∞`) are ignored;
* interval is clamped to `0.02–5 s`, catch-up to `1–64`, frame clamp to `0.02–1 s`;
* speed is clamped to `0.25–100x`; setting the current speed again raises no event;
* nothing advances while paused or stopped; `Tick` before start is a no-op;
* `Stop()` resets ticks/time/session id, so a restart begins at tick 0 with re-initialized
  systems;
* a failed start leaves `Status = Failed`, an empty session id and a clean clock; retrying is
  allowed.
* a tick batch aborts the moment a listener changes the lifecycle (pause, resume, stop, reset,
  restart, dispose, reconfigure): no stale tick is published into a closed session and no
  `SimulationTickCompletedEvent` is reported for an unfinished tick;
* undispatched ticks of an aborted batch are rolled back from the clock, but only while the
  clock still belongs to that batch (same `TickCount`, same session), so a restart that already
  reset the clock is never corrected backwards;
* `Tick` and `TickAll` are re-entrancy safe; a system may shut the graph down from inside its
  own `Tick` and no further system is dispatched;
* the tick that was already published stays accounted for, so `TickCount` always matches the
  highest tick number any listener observed.

---

## 5. Test results

### 5.1 Inventory (authored)

| Suite | Fixtures | Tests | New in Phase 02 |
| --- | --- | --- | --- |
| EditMode | 16 | **162** | **97** |
| PlayMode | 4 | **8** | **3** |

Phase 02 EditMode coverage:

| Area | Tests | Examples |
| --- | --- | --- |
| Clock | 15 | accumulation, multi-tick frames, clamp, drop budget, pause/resume, speed scaling, elapsed accuracy, invalid deltas, reset, clamping, determinism |
| System registry | 21 | dependency order, execution-order tie-break, id tie-break, missing dependency, cycle, rollback, idempotent init, tick order, failure isolation, auto-disable, reverse shutdown, shutdown failure, event publication, clear, registration rules, duplicate dependency declarations, shutdown mid-tick, rebuild mid-tick, re-entrant `TickAll` |
| Simulation manager | 28 | start/stop, started/stopped events, whole-tick production, tick + completed events, context dispatch, pause/resume, speed change/rejection, shutdown, restart, double start, failed init, catch-up clamp, start-paused, settings clamping, dispose, pause/stop/reset/dispose/restart during a tick event, re-entrant `Tick` |
| Sessions | 17 | create + events, duplicate rejection, generated seed, fallback/truncated names, initialization failure rollback, pause/resume, speed, progress sync, end, idempotent end, external stop, dispose, re-creation |
| Settings | 15 | defaults, clamping, fallback ladder, sort/dedupe, non-finite filtering, nearest level, defensive copy, config mapping, config sanitation, labels |
| Bootstrap | +1 | composed simulation is configured from the project asset |

Phase 02 PlayMode coverage: session + clock start, HUD control presence and clock text, pause
freezing ticks + speed change, scene transition ending the session and a clean second session.

### 5.2 Execution status — **not executed**

The development environment has **no Unity Editor and no .NET/Mono compiler**, so:

* the C# has **not been compiled** by Unity,
* the 162 EditMode and 8 PlayMode tests have **not been run**,
* the updated `GameConfiguration.asset` has **not been imported**,
* the gameplay UI has **not been visually inspected**.

No test result is claimed. The suites are committed as source and must be run in Unity (§8).

---

## 6. Verification that was actually executed

| Check | Result |
| --- | --- |
| `python3 Tools/validate_project.py` | **0 errors, 0 warnings** — 117 C# types across 88 files, 4 asmdefs, 37 packages, 152 `.meta` files, 47 folders, 16 in-project GUIDs resolve, 2 quality levels wired |
| Full C# syntax parse of all 88 sources (tree-sitter C# grammar) | 0 syntax errors |
| Constructor arity scan against declared constructors | 0 mismatches |
| Brace/parenthesis balance, namespace presence, file-name type match | pass (validator) |
| Manual API cross-check of new code against the Phase 01 sources (no `UnityEngine.DeviceType.Tablet`, no `FindObjectOfType`, no `MenuItem(priority:)`) | pass |
| Cross-file member references of the new types (manual review) | consistent |

What static validation cannot prove: Unity compilation, type resolution inside Unity's API
surface, serialization of the new asset fields, runtime lifecycle in a real player loop.

---

## 7. Known limitations

1. **No domain systems yet** — the registry is exercised by production code (`InitializeAll`,
   `TickAll`, `ShutdownAll` run every session) but the shipped registration list is empty by
   design; gameplay begins in Phase 03.
2. **No save/load** — `GameSession` is save-ready, nothing writes a slot, *Continue* stays
   disabled.
3. **No `.inputactions`** — pause/speed are pointer-driven UI buttons; keyboard/touch shortcuts
   arrive with the input phase.
4. **No TMP essentials in the repository** — the HUD uses the Phase 01 TMP/legacy fallback, so
   typography depends on importing TMP Essential Resources.
5. **One runtime assembly** — contracts are in place; the assembly split is a roadmap item.
6. **Speed reconfigure while running** is allowed but rebases the clock (partial tick discarded)
   and logs a warning; production code only configures before a session starts.
7. **No replay harness** — determinism is proven by unit tests, not by a recorded replay tool.
8. **No Unity execution** — see §5.2; this is the single environmental limitation.

---

## 8. Exact instructions to verify

1. Open the repository root in Unity **6000.3.24f1** (or newer 6000.3 LTS).
2. Let the package manager resolve `Packages/manifest.json` (URP 17.3.0, Input System 1.20.0).
3. Import TMP Essential Resources when prompted (`Window → TextMeshPro → Import TMP Essential
   Resources`).
4. Check the console: **zero compile errors** expected. If anything fails, it is a Phase 02
   regression and should be reported before Phase 03.
5. Run `Window → General → Test Runner → EditMode → Run All` — expected **162 / 162 passing**.
6. Run `PlayMode → Run All` — expected **8 / 8 passing** (each boot test takes a few seconds).
7. Open `Assets/Scenes/Bootstrap/Bootstrap.unity`, press **Play**, choose **NEW GAME**:
   * the control bar shows `T+ 00:00:00`, `RUNNING // 1x`, `NEW COLONY // SEED … // TICK …`;
   * the tick counter advances every 0.5 s of simulated time;
   * `PAUSE` freezes the clock and the app enters `Paused`; `RESUME` continues;
   * `2x` / `4x` visibly accelerate the clock and highlight the active button;
   * `RETURN TO MENU` ends the session and returns to the command interface;
   * start a second game: the clock restarts at `T+ 00:00:00` with a new seed.
8. Optional: edit `Assets/Resources/Config/GameConfiguration.asset`
   (`simulation tick interval`, `speed multipliers`, `max catch-up ticks`, frame clamp) and
   confirm the validation clamps/filters bad values instead of failing.
9. Optional: run `python3 Tools/validate_project.py --verbose` (works without Unity) — expected
   `0 error(s), 0 warning(s)`.
10. Optional headless:
    ```bash
    Unity -batchmode -projectPath . -runTests -testPlatform EditMode \
          -testResults ./TestResults/editmode.xml -logFile -
    Unity -batchmode -projectPath . -runTests -testPlatform PlayMode \
          -testResults ./TestResults/playmode.xml -logFile -
    ```

---

## 9. Compatibility with Phase 01

* **Preserved**: scenes, scene GUIDs, all asset GUIDs, URP settings, quality tiers, packages,
  editor version, save system, settings service, platform detection, state machine, event bus
  contract, main menu, bootstrap flow, existing 70 tests (their APIs still compile).
* **Widened (source compatible for callers, breaking only for external implementers)**:
  * `ISimulationManager` gained members (`Status`, `ElapsedSeconds`, `SpeedMultiplier`,
    `Settings`, `Systems`, `SessionId`, `TryStartSimulation`, `SetSpeed`, `Reset`,
    `IDisposable`) — no other Phase 01 class implements it.
  * `GameplayHud` gained members; `SetTick` / `SetStatus` / `ReturnToMenuButton` still exist.
  * `ApplicationBootstrapper.Compose` gained an optional `IGameSessionService` parameter.
  * `GameConfiguration` gained properties; existing ones unchanged.
* **Behavioural change**: `SimulationManager.Stop()` now resets the clock (tick count returns to
  0). Phase 01 tests do not assert the old value; the gameplay HUD never displayed a stale one.
* **Registration count** changed 9 → 10 (`IGameSessionService`); the bootstrap test was updated
  accordingly.

---

## 10. Git

* Branch: `arena/317a56b6-elexiu` (never switched, never renamed).
* Phase 01 history is preserved unchanged:
  `41cc0b9 → 78c0076 → dc94bd9 → 001c972`.
* `7d86ea4` — Phase 01 hygiene: `.gitkeep` placeholders for the documented empty Unity folders
  plus dot-file handling in `Tools/generate_project_metadata.py` / `Tools/validate_project.py`
  (Git cannot store empty directories, which previously left orphan folder `.meta` files).
* `729b73b` — Phase 02: the core simulation architecture, tests, configuration, UI and
  documentation (59 files changed, 5981 insertions(+), 197 deletions(-)).
* `74a802f` — documentation: the exact Phase 02 commit set recorded in this section.
* `b515b70` — the system graph tolerates duplicate dependency declarations (authoring slip, not
  a cycle); one EditMode test added.
* `377f076` — robustness review: tick dispatch aborts when a listener changes the lifecycle
  mid-batch, undispatched ticks are rolled back, `Tick`/`TickAll` are re-entrancy safe, and the
  registry stops dispatching when the graph is shut down or rebuilt from inside a `Tick`
  (12 EditMode tests added, EditMode total 150 → 162).
* The branch was fast-forwarded onto `001c972` and pushed; no commit was amended, rewritten or
  removed, and the pre-rebase tree is byte-identical to the pushed tree (verified with
  `git diff`). Subsequent commits are ordinary fast-forward additions.

---

## 11. Next step

Phase 02 is complete pending the project owner's Unity verification and approval.
**Phase 03 has not been started.** It will cover the planet model, planet presentation, camera
and input actions, and the first construction slice — all registered as
`ISimulationSystem` implementations on the Phase 02 pipeline.
