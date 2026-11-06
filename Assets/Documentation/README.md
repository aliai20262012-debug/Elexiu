# Assets/Documentation

In-editor reference material. The authoritative project documents live in the repository root
because they must be readable on GitHub and by tools that are not Unity:

| Document | Location |
| --- | --- |
| Project overview, setup, requirements | [`../../README.md`](../../README.md) |
| Architecture, layers, decisions | [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md) |
| Phase plan | [`../../DEVELOPMENT_ROADMAP.md`](../../DEVELOPMENT_ROADMAP.md) |
| Phase 01 completion report | [`../../PHASE_01_REPORT.md`](../../PHASE_01_REPORT.md) |
| Original repository README (preserved) | [`original-repository-readme.md`](original-repository-readme.md) |

## Conventions

* **Namespaces mirror folders** — `Assets/Scripts/Core/State` → `Nexus.Core.State`.
* **One public type per file**, file name equals type name (enforced by `Tools/validate_project.py`).
* **Scenes contain entry points only.** Views build their widgets at runtime through
  `Nexus.UI.UiFactory`; no visual tuning happens in the scene view.
* **Services are created in `ApplicationBootstrapper`** and resolved by interface from
  `AppContext`. Never `new` a service inside a `MonoBehaviour`.
* **`UnityEngine.SceneManagement` is called only from `Core/Scenes/SceneLoader.cs`.**
* **Every asset is committed together with its `.meta`.**
* **Events are `readonly struct` types implementing `Nexus.Core.Events.IGameEvent`.**
