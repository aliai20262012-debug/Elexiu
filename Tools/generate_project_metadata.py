#!/usr/bin/env python3
"""
Generates deterministic Unity .meta files (GUIDs + importers) and the build settings
scene list for the Nexus Civilization project.

Unity would generate .meta files itself on first import, but committing them keeps the
repository diff-clean for the whole team and guarantees the GUIDs referenced by the scenes,
the configuration assets and EditorBuildSettings stay stable.

Usage:  python3 Tools/generate_project_metadata.py
"""

import hashlib
import json
import pathlib

PROJECT_ROOT = pathlib.Path(__file__).resolve().parent.parent
ASSETS = PROJECT_ROOT / "Assets"

# GUIDs referenced from hand written YAML (scenes). These must never change.
FIXED_GUIDS = {
    "Assets/Scripts/Core/Bootstrap/BootstrapController.cs": "8d1f0a4c5b2e4f7d9c3a1b6e5f804721",
    "Assets/Scripts/UI/MainMenuController.cs": "3c7e9b2d4a1f4c6b8e0d2f5a7c9b1e34",
    "Assets/Scripts/Simulation/GameplaySceneController.cs": "5b2d8f1a6c3e4a9d7f0b4c8e2a6d1f57",
    "Assets/Scripts/Planet/PlanetViewController.cs": "9a4c1e7b3d8f42569b0c5e2a7f4d1b83",
}

MONO_IMPORTER = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

DEFAULT_IMPORTER = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

NATIVE_IMPORTER = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

ASMDEF_IMPORTER = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def relative(path):
    return path.relative_to(PROJECT_ROOT).as_posix()


def guid_for(rel_path):
    if rel_path in FIXED_GUIDS:
        return FIXED_GUIDS[rel_path]
    digest = hashlib.md5(("nexus-civilization:" + rel_path).encode("utf-8")).hexdigest()
    return digest


def importer_template_for(rel_path):
    suffix = pathlib.PurePosixPath(rel_path).suffix.lower()
    if suffix == ".cs":
        return MONO_IMPORTER
    if suffix == ".asmdef":
        return ASMDEF_IMPORTER
    if suffix == ".asset":
        return NATIVE_IMPORTER
    if suffix in (".unity", ".prefab", ".mat", ".controller", ".anim", ".json", ".md", ".txt"):
        return DEFAULT_IMPORTER
    return DEFAULT_IMPORTER


def collect_paths():
    entries = []
    for path in sorted(ASSETS.rglob("*")):
        if path.name.endswith(".meta"):
            continue
        if path.is_dir():
            entries.append(path)
        elif path.is_file():
            entries.append(path)
    return entries


def write_meta_files():
    written = 0
    for path in collect_paths():
        meta_path = path.with_name(path.name + ".meta")
        rel = relative(path)

        if rel.endswith("/Documentation") or rel.startswith("Assets/Documentation"):
            # Documentation is imported as plain text assets; keep the meta for folder ordering.
            pass

        if meta_path.exists():
            continue

        guid = guid_for(rel)
        if path.is_dir():
            content = FOLDER_META.format(guid=guid)
        else:
            content = importer_template_for(rel).format(guid=guid)

        meta_path.write_text(content)
        written += 1

    return written


def write_editor_build_settings():
    scene_paths = [
        "Assets/Scenes/Bootstrap/Bootstrap.unity",
        "Assets/Scenes/MainMenu/MainMenu.unity",
        "Assets/Scenes/Gameplay/Gameplay.unity",
    ]

    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!1045 &1",
        "EditorBuildSettings:",
        "  m_ObjectHideFlags: 0",
        "  serializedVersion: 2",
        "  m_Scenes:",
    ]

    for scene_path in scene_paths:
        lines.append("  - enabled: 1")
        lines.append("    path: " + scene_path)
        lines.append("    guid: " + guid_for(scene_path))

    lines.append("  m_configObjects: {}")
    lines.append("  m_UseUCBPForAssetBundles: 0")
    lines.append("")

    (PROJECT_ROOT / "ProjectSettings" / "EditorBuildSettings.asset").write_text("\n".join(lines))


def main():
    written = write_meta_files()
    write_editor_build_settings()
    print("meta files written: {0}".format(written))
    print("EditorBuildSettings.asset updated with {0} scenes".format(3))


if __name__ == "__main__":
    main()
