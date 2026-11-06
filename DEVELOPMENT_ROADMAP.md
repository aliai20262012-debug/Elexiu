# NEXUS CIVILIZATION — Development Roadmap

Phase 01 (this repository state) delivers the foundation and the playable shell.
No gameplay system is started until Phase 02 is approved.

---

## Phase 01 — Foundation & Project Setup  ✅ complete

* Unity 6.3 LTS project, URP, Input System, TextMeshPro, Git-ready structure
* Service composition, state machine, event bus, configuration, platform detection,
  save-system interface, simulation clock placeholder
* Bootstrap / Main Menu / Gameplay scenes with a dark sci-fi shell
* Responsive UI foundation (canvas scaling, safe area, compact/regular/expanded profiles)
* EditMode + PlayMode test suites, editor project validator, documentation
* Exit criteria: project opens without blocking errors, the shell runs, validation passes —
  see [PHASE_01_REPORT.md](PHASE_01_REPORT.md)

## Phase 02 — Simulation Domain & Gameplay Loop

Entry criteria: Phase 01 approved.

1. **Domain skeleton** — introduce `IDomainSystem` (`Tick(SimulationContext)`), system
   registry and deterministic RNG; split the runtime into `Nexus.Domain` assemblies.
2. **Planet model** — `Planet`, `HexTile`/`Region` data, biome and resource generation
   (pure C#, unit tested, no `MonoBehaviour`).
3. **Camera & input** — Input Actions asset, pan/zoom/edge scroll, touch gestures,
   selection; replaces the placeholder `CameraRig`.
4. **Construction slice** — one buildable structure, cost/time resolution on tick.
5. **Turn/tick ownership** — pause, speed multipliers, and the simulation clock bound to
   gameplay state instead of the scene.
6. **Saving** — implement `SaveData` payload for the planet/empire and enable *Continue*.
7. **UI pipeline** — move shell widgets into themeable prefabs driven by `UiTheme`, add the
   brand TMP font asset, HUD layout for Compact/Regular profiles.
8. **Tests** — determinism tests for generation, snapshot tests for save round-trips,
   PlayMode test for a full build-then-tick cycle.

## Phase 03 — Civilization, Economy, Technology

Population/colony growth, resource chains, production queues, technology tree with
prerequisites, unlock-driven UI, balance data as ScriptableObjects.

## Phase 04 — Diplomacy & AI

Faction model, relationship matrix, treaties, opinion modifiers, utility/behaviour-tree AI
driven by the same tick pipeline (deterministic, replayable).

## Phase 05 — Content, Audio & Polish

Art pipeline (models → prefabs → LOD), audio mixer + music states, localisation,
accessibility (UI scale, colour-blind palettes, remapping), onboarding.

## Phase 06 — Build, Release & Live Ops

IL2CPP builds per platform, Android AAB + keystore handling, iOS provisioning, automated
CI (batch-mode compile, EditMode + PlayMode tests, static validator), crash reporting,
store assets.

---

## Continuous workstreams

| Workstream | Cadence |
| --- | --- |
| `python3 Tools/validate_project.py` | Every commit (CI gate) |
| EditMode tests (must stay < 10 s) | Every commit |
| PlayMode scene tests | Every PR touching scenes, UI or flow |
| Documentation update | Every phase and every architectural change |
| Package/editor upgrades | Deliberate, one per phase, with a full test run |

## Known Phase 01 follow-ups (tracked here, not started)

1. Import TMP Essential Resources and commit the generated `Assets/TextMesh Pro` folder, or
   provide a font-asset generator so the shell always renders with TMP.
2. Add a `.inputactions` asset with UI + gameplay maps instead of the Input System's default
   UI actions (`AssignDefaultActions` is used today).
3. Add CI workflow (GitHub Actions with GameCI) running validation and both test suites.
4. Split `Nexus.Runtime` into `Nexus.Core`, `Nexus.Domain`, `Nexus.Presentation`.
5. Replace code-built UI with themed prefabs and a `UiTheme`-driven style provider.
6. Add addressable/async scene loading with a loading screen and progress.
