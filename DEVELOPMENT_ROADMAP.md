# NEXUS CIVILIZATION — Development Roadmap

> Phase 02 delivered the core game architecture and Phase 03 delivered the planet and world
> foundation. The first construction slice and save/load that were originally sketched for
> Phase 03 now live in Phase 04 together with civilization and economy, followed by
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
8. **Tests** — 97 new EditMode tests (clock, registry, manager, sessions, settings) and 3 new
   PlayMode tests (session start, pause/speed, scene-transition + clean restart).
9. **Documentation** — this roadmap, [ARCHITECTURE.md](ARCHITECTURE.md) §11 and
   [PHASE_02_REPORT.md](PHASE_02_REPORT.md).

**Exit criteria met**: lifecycle, session create/terminate, time and speed control, ordered
system init/shutdown, scene integration and tests *authored*; static validation passes. The
suites have not been executed because the development environment has no Unity Editor — see
the report for the verified/unverified split.

## Phase 03 — Planet System & World Foundation  ✅ complete

Entry criteria: Phase 02 approved by the project owner.

**Delivered**

1. **NEXUS-01 planet** — procedural icosphere with displaced land, a separate flat ocean
   submesh, baked equirectangular albedo + normal maps, polar caps, a Fresnel atmosphere shell,
   warm key / cyan rim lighting and deep-navy ambient floor. No flat sphere texture; the whole
   body is generated from data and the session seed.
2. **Planet data architecture** — `PlanetDefinition` ScriptableObject (identity, physics,
   generation tuning, quality, region file reference) plus a validated JSON region table:
   8 regions with terrain, water class, coordinates/angular radius, climate, resources,
   environmental indicators and habitability. Parser and validator degrade gracefully.
3. **Multiple identifiable regions** — region lookup (`RegionLocator`) shared by picking,
   surface tinting and the dossier; spherical-cap highlight plus ring; nearest-region fallback
   so every point of the planet resolves to a region.
4. **Interaction** — mouse orbit/zoom, touch orbit/pinch, damped camera with clamped pitch and
   distance, adaptive near/far planes, click/tap selection, `Escape`/empty-space to clear.
5. **Regional view** — camera flies to the selected region, the dossier shows every authored
   property and the live season/temperature line, and a settlement anchor is reserved for
   Phase 04.
6. **Simulation integration** — `PlanetSystem` registered in `ApplicationBootstrapper.Compose`,
   initialized from `SimulationInitializationContext`, deterministic from `SimulationContext`,
   published `PlanetEnvironmentUpdatedEvent` once per simulated day, safe shutdown, no UI or
   camera dependency.
7. **Quality scaling** — desktop/tablet 5 subdivisions & 512 px maps, phones 4 subdivisions &
   256 px maps with a wider camera profile; runtime textures are released after upload.
8. **Tests** — 80 new EditMode tests (definition/validation, region parsing, geometry/lookup,
   surface determinism, planet system lifecycle, camera, view/selection, input commands) and
   4 new PlayMode tests (planet construction, region selection + panel + focus, overview
   return, click-to-select through the real input path).
9. **Documentation** — [ARCHITECTURE.md](ARCHITECTURE.md) §12, this roadmap,
   [PHASE_03_REPORT.md](PHASE_03_REPORT.md) and the README.

**Exit criteria met**: interactive planet, region selection and regional information,
deterministic planet simulation on the existing graph, scene integration and tests *authored*;
static validation passes. The suites have not been executed here because the environment has
no Unity Editor — see the report for the verified/unverified split.

## Phase 04 — Settlement, Civilization, Economy & Saving

Entry criteria: Phase 03 approved.

1. **Construction slice** — one buildable structure per region anchored through
   `PlanetView.TryGetSettlementAnchor`, cost/build-time resolved on tick with completion events.
2. **Civilization & economy** — population/colony growth, resource chains, production queues,
   technology tree with prerequisites, unlock-driven UI, balance data as ScriptableObjects.
3. **Saving** — implement the `SaveData` payload for planet/region/empire, enable *Continue*
   and restore sessions through `TryCreateSession(name, seed, ...)`.

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
