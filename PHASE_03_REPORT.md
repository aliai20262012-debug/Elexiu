# PHASE 03 REPORT — Planet System & World Foundation

**Status:** complete, awaiting project-owner approval
**Branch:** `arena/317a56b6-elexiu`
**Base commit:** `941612e` (approved Phase 02)
**Scope:** one playable planet ("NEXUS-01") with data-driven regions, orbit/touch camera,
region selection + dossier, deterministic planet simulation system, quality scaling, tests and
documentation. No economy, civilization, technology, diplomacy, multiplayer or city building.

---

## 1. Summary

Phase 03 turns the Phase 02 shell into a world you can actually explore. The gameplay scene now
shows a procedurally generated sphere — displaced continents, a separate flat ocean shell, baked
albedo and normal maps, polar caps, a Fresnel atmosphere and studio-style lighting — carrying
eight authored regions. The player orbits with mouse or touch, zooms without clipping into the
surface, selects a region to read its full dossier, and the camera flies into a regional view; a
deterministic `PlanetSystem` runs the planet calendar and per-region environment on the existing
simulation graph.

Everything from Phase 01 and Phase 02 is preserved: same scenes and GUIDs, same composition
root, same clock/session/graph, same HUD, same event catalogue (extended, not replaced), same
tests (extended, not modified), same git history. The placeholder `PlanetViewController` was
replaced in place, keeping its serialized fields so `Gameplay.unity` did not need to change.

---

## 2. Delivered features

### 2.1 Planet model & visual quality

* Icosphere geometry (2–6 subdivisions; 5 on desktop/tablet, 4 on phones) displaced by a
  deterministic height field — continents, shelves and ridges are real geometry, not a texture
  trick.
* Two submeshes: **land** (albedo + normal + roughness, subtly metallic) and **ocean** (flat sea
  sphere, smooth, high specular) so the coastline is a genuine material edge and the sea catches
  the key light.
* Baked equirectangular **albedo** (biome palette: deep navy ocean, teal shelf, electric-cyan
  coastal glow, olive/green plains, grey highlands, tan desert, snow ice, ember volcanic) and a
  derived tangent-space **normal map**, both generated from one shared height grid.
* Polished ice caps, region-hint tinting on the surface so authored regions are visually
  discoverable, cheap grain for texture break-up.
* **Atmosphere**: additive Fresnel shell (`Resources/Shaders/PlanetAtmosphere.shader`, URP
  compatible, with a stock Unlit fallback if the shader is unavailable).
* **Lighting**: warm key directional light with a shadow map, cyan rim light, navy ambient floor;
  the rig reuses an existing key light instead of duplicating it.
* **Selection feedback**: spherical-cap highlight + thin ring, additive unlit, parented to the
  rotating body, rebuilt only when the selection changes, gently pulsed.

### 2.2 Data architecture

* `PlanetDefinition` (ScriptableObject) — identity (id, name, designation, seed), physics
  (radius, gravity, day length, orbital period, axial tilt, visual spin), surface tuning (sea
  level, continent/detail frequency, ridge strength, relief, ocean depth), quality (mesh
  subdivisions, texture width, atmosphere density) and a region-file reference.
  Asset: `Assets/Resources/Config/Planet/Nexus-01.asset`.
* Region table as diffable JSON: `Assets/Resources/Config/Planet/nexus-01.regions.json`,
  parsed through `PlanetRegionFile` DTOs into immutable `PlanetRegion` objects, with a defensive
  parser (string enums, `null` blocks, per-issue reporting) and a semantic validator (unique
  ids/codes, coordinate ranges, water/terrain consistency, duplicate deposits, habitability
  range).
* Per-region data: terrain category, water classification, land fraction, latitude/longitude and
  angular radius, `ClimateProfile` (temperature envelope, seasonal range, humidity, wind,
  precipitation), `ResourceDeposit[]` (minerals, metals, rare earths, water, biomass,
  geothermal), `EnvironmentalIndicators` (air quality, radiation, biodiversity, tectonics) and
  `HabitabilityProfile` (score, rating, limiting factor).
* Graceful degradation: missing JSON → `PlanetRegionLibrary` fallback set; missing planet asset →
  `PlanetDefinition.CreateRuntimeDefault()`; malformed entries are skipped/clamped, never fatal.

### 2.3 NEXUS-01 data (as shipped)

| Property | Value |
| --- | --- |
| Designation | NEXUS-01 · Class M · "Aurora Prime" |
| Seed | `20261106` (session seed is mixed in: `seed ^ sessionSeed`) |
| Radius | 6 180 km (render radius 1.9 units) |
| Gravity | 0.97 g |
| Day length | 900 simulated seconds (15 min at 1x) |
| Orbital period | 96 days, axial tilt 23.4°, visual spin 1.5°/s |
| Surface | sea level 0.5, continent frequency 1.35, detail 7.5, ridge 0.55, relief 0.03, ocean depth 0.012 |
| Quality | 5 subdivisions, 512 px maps (phones: 4 / 256 px), atmosphere 0.65 |

| # | Region | Code | Terrain | Habitability |
| --- | --- | --- | --- | --- |
| 1 | AURORA BASIN | NX-AU-01 | Plains | 0.81 |
| 2 | VERDANT SHELF | NX-VD-02 | Coastal | 0.74 |
| 3 | KARRIC HIGHLANDS | NX-KR-03 | Highlands | 0.52 |
| 4 | DUNE MERIDIAN | NX-DS-04 | Desert | 0.24 |
| 5 | HALCYON TUNDRA | NX-HL-05 | Tundra | 0.41 |
| 6 | MERIDIAN ABYSS | NX-OC-06 | Ocean | 0.08 |
| 7 | EMBER REACH | NX-EM-07 | Volcanic | 0.18 |
| 8 | POLAR VEIL | NX-IS-08 | Ice | 0.12 |

25 resource entries are split across the eight regions (each region 2–4 deposits).

### 2.4 Interaction, camera and regional view

* **Orbit camera** (`PlanetCameraController`): yaw/pitch with clamped pitch, distance clamped to
  `[1.35R, 6.2R]` so the camera can never enter the surface, overview 3.4R / focus 2.05R,
  frame-rate independent `1 - exp(-8·dt)` damping, `IsSettled`, adaptive near/far planes.
  Input handling is separated from camera commands, so tests drive it directly.
* **Input** (`PlanetInputController`): left-drag orbit, wheel zoom (120 units per notch),
  tap/click selection (≤10 px, ≤0.45 s), one-finger orbit, two-finger pinch (relative to screen
  height), `Escape` to clear, `EventSystem` guarding so HUD clicks never leak into the camera.
* **Selection**: physics raycast → body-local direction → `RegionLocator`; exact region wins,
  otherwise the nearest authored region is used, so every click on the planet resolves. The same
  locator tints the surface, keeping visuals and data consistent.
* **Regional view**: the camera flies to the region (planet rotation pauses while focused), the
  dossier fills with identity, terrain/water, coordinates, climate, resources, environmental
  indicators and habitability; `ENTER REGION` re-focuses after free orbiting, `OVERVIEW`
  (or empty-space click / `Escape`) returns and clears.
* **Dossier UI** (`RegionInfoPanel`, built from the Phase 01 `UiFactory`/`UiTheme`): right sidebar
  on wide screens, narrower lower panel on phones, per-region accent colour, live
  `DAY / SEASON / TEMPERATURE` line fed by `PlanetEnvironmentUpdatedEvent`, and a reserved
  "SETTLEMENT SLOT" line for Phase 04.
* **Extension point**: `PlanetView.TryGetSettlementAnchor(regionId, out position, out normal)`
  gives Phase 04 a surface anchor + normal without any new picking code.

### 2.5 Simulation integration

* `PlanetSystem : ISimulationSystem` — registered once in
  `ApplicationBootstrapper.Compose`, so every session (menu → gameplay, restart, PlayMode tests)
  gets it with no scene wiring.
* Initialized from `SimulationInitializationContext` (region snapshot + `seed ^ sessionSeed`),
  ticks read only `SimulationContext` (`Tick`, `SimulatedSeconds`, `ElapsedSeconds`), and the
  system shuts down idempotently.
* Deterministic calendar: `days = elapsed / dayLength`; season phase from the orbital period;
  per-region temperature = authored envelope at the current season in the correct hemisphere,
  corrected by latitude and altitude.
* Publishes `PlanetEnvironmentUpdatedEvent` at most once per simulated day.
* Exposes `Surface`, `State`, `Regions` and `TryGetRegionEnvironment(regionId, out env)` for
  future systems; no reference to cameras, UI or any `MonoBehaviour`.

### 2.6 Performance & quality scaling

* Desktop/tablet: 5 subdivisions (~10 240 verts), 512 × 256 maps. Phones: 4 subdivisions
  (~2 560 verts), 256 × 128 maps, compact camera profile (wider framing, slightly stronger
  screen-space margins).
* Textures are generated once at build time and made non-readable after upload; the CPU copies
  are released. Highlight/ring meshes are rebuilt only on selection change; the camera, input and
  HUD update paths allocate nothing per frame.
* Mobile shader stays on the URP Lit/Unlit path — no post-processing, one shadow-casting light,
  one additive shell.

---

## 3. New and modified files

### 3.1 New — runtime (`Assets/Scripts/Planet/`, 32 scripts)

Data layer: `RegionTerrainCategory.cs`, `WaterClassification.cs`, `ResourceType.cs`,
`ResourceDeposit.cs`, `ClimateProfile.cs`, `EnvironmentalIndicators.cs`,
`HabitabilityProfile.cs`, `PlanetRegion.cs`, `PlanetLabels.cs`, `PlanetGeo.cs`,
`RegionLocator.cs`, `PlanetRegionFile.cs`, `PlanetRegionParser.cs`, `PlanetRegionLibrary.cs`,
`PlanetValidationReport.cs`, `PlanetDataValidator.cs`, `PlanetDefinition.cs`, `PlanetNoise.cs`,
`PlanetSurfaceSettings.cs`, `PlanetSurface.cs`, `PlanetMeshBuilder.cs`,
`PlanetTextureGenerator.cs`.

Presentation / simulation: `PlanetMaterials.cs`, `PlanetLighting.cs`,
`PlanetCameraSettings.cs`, `PlanetCameraController.cs`, `PlanetView.cs`,
`PlanetInputController.cs`, `PlanetRegionEnvironment.cs`, `PlanetSimulationState.cs`,
`PlanetSystem.cs`, `PlanetSceneController.cs`.

### 3.2 New — UI, assets, tests, docs

| File | Purpose |
| --- | --- |
| `Assets/Scripts/UI/RegionInfoPanel.cs` | Regional dossier panel (code-built) |
| `Assets/Resources/Config/Planet/Nexus-01.asset` | `PlanetDefinition` instance (GUID `dd8793399abbf2adabd9862dbb89a806`) |
| `Assets/Resources/Config/Planet/nexus-01.regions.json` | 8-region authored table (GUID `f547a29dac7d4893e6562757b3246527`) |
| `Assets/Resources/Shaders/PlanetAtmosphere.shader` | URP Fresnel atmosphere (GUID `2eaa1ec1651d302bce73bc31be016b8d`) |
| `Assets/Tests/EditMode/PlanetDefinitionTests.cs` | 10 tests — asset values, validation, runtime fallbacks |
| `Assets/Tests/EditMode/PlanetRegionParserTests.cs` | 11 tests — parsing, malformed input, fallbacks |
| `Assets/Tests/EditMode/RegionLocatorTests.cs` | 11 tests — geometry, nearest lookup, angular radius |
| `Assets/Tests/EditMode/PlanetSurfaceTests.cs` | 11 tests — determinism, land/ocean split, biome palette, caches |
| `Assets/Tests/EditMode/PlanetSystemTests.cs` | 11 tests — init, day/season math, events, shutdown, failures |
| `Assets/Tests/EditMode/PlanetCameraControllerTests.cs` | 9 tests — clamps, damping, focus/overview, settle |
| `Assets/Tests/EditMode/PlanetViewTests.cs` | 10 tests — build, hierarchy, selection, anchors, picking |
| `Assets/Tests/EditMode/PlanetInputControllerTests.cs` | 7 tests — orbit/zoom commands, selection, clear |
| `Assets/Tests/PlayMode/PlanetPlayModeTests.cs` | 4 tests — planet in scene, selection + focus, overview, click-through |
| `PHASE_03_REPORT.md` | This report |

Every new asset and script has a `.meta` with a deterministic GUID
(`md5("nexus-civilization:" + path)`), so the repository stays stable across machines.

### 3.3 Modified

| File | Change |
| --- | --- |
| `Assets/Scripts/Planet/PlanetViewController.cs` | Replaced the placeholder sphere with the real planet; keeps its serialized fields (`_radius`, `_rotationDegreesPerSecond`, `_surfaceColor`) so `Gameplay.unity` needed no edit |
| `Assets/Scripts/Simulation/GameplaySceneController.cs` | Builds the planet + `PlanetSceneController`, feeds camera/planet ticks, tears the view down on exit |
| `Assets/Scripts/Core/Bootstrap/ApplicationBootstrapper.cs` | Registers `PlanetSystem` in every session composition |
| `Assets/Scripts/Core/Events/GameEvents.cs` | Adds `PlanetRegionSelectedEvent`, `PlanetRegionClearedEvent`, `PlanetEnvironmentUpdatedEvent` (existing events untouched) |
| `Assets/Scripts/ScriptableObjects/GameConfiguration.cs` | `_defaultPlanet` reference + clamped accessor with `Resources` fallback |
| `Assets/Resources/Config/GameConfiguration.asset` | References `Nexus-01.asset` |
| `Assets/Scripts/UI/GameplayHud.cs` | Hosts `RegionInfoPanel` above the control bar |
| `Tools/generate_project_metadata.py` | Generates metas for shaders / JSON text assets |
| `Tools/validate_project.py` | Planet-data validation + serialized-field-vs-scene check |
| `README.md`, `ARCHITECTURE.md`, `DEVELOPMENT_ROADMAP.md` | Phase 03 documentation |

No scene, prefab, GUID, package or project setting was modified or removed.

---

## 4. Verification performed

### 4.1 Static validation (executed)

`python3 Tools/validate_project.py`:

```
0 error(s), 0 warning(s)
  info: 19 GUID references resolve inside the project
  info: 161 GUID references point to Unity packages
  info: 170 C# types declared across 130 files
  info: 4 assembly definitions validated
  info: 37 packages pinned in the manifest
  info: Unity editor version: 6000.3.24f1
  info: 2 quality levels wired to URP assets
  info: Planet NEXUS-01: 8 regions, 25 resource entries validated
```

The validator checks, for Phase 03: every asset/script has a `.meta`; every internal GUID
reference resolves; the planet asset parses and its values are in range; every region parses,
has unique id/code, valid coordinates, valid terrain/water enums and 0..1 indicators; every
`[SerializeField]` named in `Gameplay.unity` still exists on its component (this is what caught
three fields dropped from `PlanetViewController`).

Additional static checks executed here:

* tree-sitter (C# grammar) parse of all 130 `.cs` files → **0 syntax errors**.
* constructor-arity cross-check of every `new` expression against its declared type →
  **0 mismatches**.
* brace/using/namespace sanity and UTF-8 validity for every new file.

### 4.2 Unity tests — authored, **not executed**

There is no Unity Editor, .NET or Mono in this environment. **No Unity test run, compile or
build happened.** The new suites are committed as source and must be run by the owner:

* **EditMode** — 24 fixtures, **242 tests** (Phase 02: 162; Phase 03 adds **80**).
* **PlayMode** — 5 fixtures, **12 tests** (Phase 02: 8; Phase 03 adds **4**).

```bash
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults ./TestResults/editmode.xml -logFile -
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults ./TestResults/playmode.xml -logFile -
```

Or in the editor: `Window → General → Test Runner → EditMode/PlayMode → Run All`.

If a Phase 03 fixture fails, the most likely causes are the items in §6 (unverified API
assumptions), not the static structure — the validator, parser and geometry logic are pure C#.

### 4.3 Build results

**No build was produced or attempted** (no Unity installation, no platform modules). Player
settings, quality↔URP wiring and bundle identifiers remain as configured in Phase 01.

### 4.4 Screenshots / video

**None available.** Capturing them requires the Unity editor, which this environment does not
have. The owner can reproduce the intended framing by pressing Play in `Bootstrap.unity` and
following §5.

---

## 5. How to verify (owner, ~5 minutes)

1. Open the project in Unity `6000.3.x` and import TMP Essentials if not already done.
2. Open `Assets/Scenes/Bootstrap/Bootstrap.unity` → **Play** → **NEW GAME**.
3. Confirm: continents and oceans are distinguishable, the limb glows cyan, the planet rotates,
   the region dossier is hidden until a region is selected.
4. Drag to orbit, scroll to zoom (the camera must stop before the surface), tap/click a region →
   highlight + dossier + camera fly-in; `OVERVIEW` and `Escape` return.
5. With a region selected, press `1x/2x` and watch the `DAY` counter advance; temperatures differ
   between POLAR VEIL and DUNE MERIDIAN.
6. `Window → General → Test Runner` → run both suites.
7. `python3 Tools/validate_project.py` (optional, mirrors the CI gate).

---

## 6. Known limitations and risks

1. **Unverified Unity compilation.** The C# is written against the real Phase 01/02 APIs and
   cross-checked statically, but only the editor can prove it compiles. Highest-risk spots: the
   `SimulationSystemBase` constructor / `SimulationInitializationContext` argument order, the
   exact `PlanetCameraSettings` member names used by the new tests, and
   `Transform.Find("PlanetBody/RegionHighlight/RegionCap")` in `PlanetViewTests`.
2. **Atmosphere shader** is hand-written HLSL for URP 17 and has never been compiled. If it
   fails, `PlanetMaterials` falls back to a stock Unlit material (visible but flat) — check the
   Console for shader errors on first import.
3. **Region boundaries are angular caps**, not Voronoi cells: overlapping regions resolve by
   normalised distance and the highlight is a circular patch.
4. **No runtime in-scene verification** of camera feel (damping, pinch sensitivity, UI layout at
   phone aspect). Values are reasoned, not tuned by hand in the editor; `PlanetCameraSettings`
   and the dossier's `bottomInset` are the knobs.
5. **Surface is a height field**: no rivers, tectonics, clouds, city lights, or biome-dependent
   material blending beyond the baked albedo.
6. **Season/time**: one planet day is 15 real minutes at 1x (96 days per year), so season changes
   are not observable in a quick demo and are only covered by unit tests.
7. **Texture generation cost** (one noise pass + up to 16 region lookups per texel) is fine at
   256/512 px but would need a job/GPU path before 1024 px or larger planets.
8. **One planet only**: the data model supports several definitions, but no planet picker or
   multi-planet campaign exists yet.
9. Phase 01 open items remain open: TMP Essentials not committed, no `.inputactions` asset
   (Input System defaults are used), no save file yet, Android/iOS modules not present.

---

## 7. Scope control

Not started, by design: economy, population/production, technology tree, AI diplomacy,
multiplayer, city building, save-game payload, multiple planets. `PlanetSystem` deliberately
exposes the region environment and settlement anchors so Phase 04 can build on it without
touching Phase 03 code.

---

## 8. Git

* Branch: `arena/317a56b6-elexiu` (never force-pushed)
* Base: `941612e` — approved Phase 02
* Phase 03 commit: a single commit on this branch,
  "Phase 03: NEXUS-01 planet system, world foundation and regional view"
  (see `git log --oneline 941612e..HEAD`)
* Push: `git push origin arena/317a56b6-elexiu`

Phase 04 has **not** been started and will not be until the owner approves this report.
