# NEXUS CIVILIZATION — Development Roadmap

> Phase 02 renumbered the later phases. Phase 02 delivered the core game architecture, so the
> previously planned "simulation domain & gameplay loop" work (planet model, camera, first
> construction slice) now lives in Phase 03, followed by civilization/economy (04),
> diplomacy/AI (05), content & polish (06) and release (07).

---

## Phase 01 — Foundation & Project Setup  ✅ complete

* Unity 6.3 LTS project, URP, Input System, TextMeshPro, Git-ready structure
* Service composition, state machine, event bus, configuration, platform detection,
  save-system interface, simulation clock placeholder
* Bootstrap / Main Menu / Gameplay scenes with a dark sci-fi shell
* Responsive UI foundation (canvas scaling, safe area, compact/regular/expanded profiles)
* EditMode + PlayMode test suites, editor project validator, documentation
* Exit criteria met — see [PHASE_01_REPORT.md](PHASE_01_REPORT.md)

## Phase 02 — Core Game Architecture  ✅ complete

Entry criteria: Phase 01 approved by the project owner.

**Delivered**

1. **Simulation lifecycle** — `SimulationManager` with `Stopped / Running / Paused / Failed`
   states, explicit start, pause, resume, speed change, stop, reset and dispose; failed
   initialization rolls the system graph back.
2. **Deterministic clock** — pure C# `SimulationClock` (fixed step, frame-delta clamp,
   catch-up budget, dropped-tick accounting, pause clears the accumulator, speed 0.25–100x).
3. **System graph** — `ISimulationSystem`, `SimulationSystemBase` and
   `SimulationSystemRegistry`: dependency ordering (Kahn), stable tie-breaks, missing
   dependency/cycle reporting, rollback, failure isolation, auto-disable after repeated tick
   failures, reverse-order idempotent shutdown.
4. **Game sessions** — `GameSession` payload (id, civilization name, seed, progress, end
   reason) and `IGameSessionService` coordinating creation, pause/resume mirroring, speed
   changes, external stops, termination and clean re-creation.
5. **Events** — `SimulationStarted/Stopped/Paused/Resumed/SpeedChanged/TickCompleted`,
   `SimulationSystemFailed`, `GameSessionCreated/Ended`; the Phase 01 `SimulationTickEvent` is
   kept as the domain tick signal.
6. **Configuration** — seven validated simulation settings in `GameConfiguration`
   (interval, speed ladder, default speed, start paused, catch-up budget, frame clamp, default
   civilization name), with clamping/filtering and safe fallbacks.
7. **Gameplay integration & UI** — `GameplaySceneController` creates the session, feeds frame
   time into the driver, mirrors pause into `GameState`, ends the session on scene exit;
   `SimulationControlBar` adds pause/resume, speed buttons (1x/2x/4x), simulated clock,
   lifecycle status and session/tick readout to the existing dark sci-fi HUD.
8. **Tests** — 84 new EditMode tests (clock, registry, manager, sessions, settings) and 3 new
   PlayMode tests (session start, pause/speed, scene-transition + clean restart).
9. **Documentation** — this roadmap, [ARCHITECTURE.md](ARCHITECTURE.md) §11 and
   [PHASE_02_REPORT.md](PHASE_02_REPORT.md).

**Exit criteria met**: lifecycle, session create/terminate, time and speed control, ordered
system init/shutdown, scene integration and tests *authored*; static validation passes. The
suites have not been executed because the development environment has no Unity Editor — see
the report for the verified/unverified split.

## Phase 03 — World Simulation: Planet, Camera & First Construction

Entry criteria: Phase 02 approved.

1. **Planet model** — `Planet`, `HexTile`/`Region` data, biome and resource generation as pure
   C# systems registered with `SimulationSystemRegistry` (seeded from
   `SimulationInitializationContext.Seed`, unit tested, no `MonoBehaviour`).
2. **Planet presentation** — replace `PlanetViewController` with a renderer driven by
   `SimulationTickCompletedEvent`.
3. **Camera & input** — `.inputactions` asset, pan/zoom/edge scroll, touch gestures, selection;
   replaces the placeholder `CameraRig`.
4. **Construction slice** — one buildable structure resolved on tick (cost, build time,
   completion event) using the existing system ordering.
5. **Saving** — implement the `SaveData` payload for planet/empire and enable *Continue*;
   restore sessions through `TryCreateSession(name, seed, ...)`.
6. **Tests** — determinism tests for generation, save round-trips, PlayMode build-then-tick.

## Phase 04 — Civilization, Economy, Technology

Population/colony growth, resource chains, production queues, technology tree with
prerequisites, unlock-driven UI, balance data as ScriptableObjects. Every system registers
against the Phase 02 pipeline and receives seeds/time from `SimulationContext`.

## Phase 05 — Diplomacy & AI

Faction model, relationship matrix, treaties, opinion modifiers, utility/behaviour-tree AI
driven by the same tick pipeline (deterministic, replayable).

## Phase 06 — Content, Audio & Polish

Art pipeline (models → prefabs → LOD), audio mixer + music states, localisation, accessibility
(UI scale, colour-blind palettes, remapping), onboarding.

## Phase 07 — Build, Release & Live Ops

IL2CPP builds per platform, Android AAB + keystore handling, iOS provisioning, automated CI
(batch-mode compile, EditMode + PlayMode tests, static validator), crash reporting, store assets.

---

## Continuous workstreams

| Workstream | Cadence |
| --- | --- |
| `python3 Tools/validate_project.py` | Every commit (CI gate) |
| EditMode tests (must stay < 10 s) | Every commit |
| PlayMode scene tests | Every PR touching scenes, UI or flow |
| Documentation update | Every phase and every architectural change |
| Package/editor upgrades | Deliberate, one per phase, with a full test run |

## Known follow-ups (tracked here, not started)

1. Import TMP Essential Resources and commit the generated `Assets/TextMesh Pro` folder, or
   provide a font-asset generator so the shell always renders with TMP.
2. Add a `.inputactions` asset with UI + gameplay maps instead of the Input System's default UI
   actions (`AssignDefaultActions` is used today).
3. Add a CI workflow (GitHub Actions with GameCI) running validation and both test suites.
4. Split `Nexus.Runtime` into `Nexus.Core`, `Nexus.Domain`, `Nexus.Presentation` assemblies now
   that the simulation contracts exist.
5. Replace code-built UI with themed prefabs and a `UiTheme`-driven style provider.
6. Add addressable/async scene loading with a loading screen and progress.
7. Wire the settings menu into simulation pause (the app-level pause already exists; the
   settings panel should not require its own state).
8. Add a deterministic replay harness that feeds a recorded `Advance`/`SetSpeed` sequence into
   `SimulationClock` and compares snapshots (foundation for save verification and CI).
9. Optional interpolation of the planet view using `SimulationClock.PendingTickFraction`
   (presentation only; never in systems).
