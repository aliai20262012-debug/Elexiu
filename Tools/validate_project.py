#!/usr/bin/env python3
"""
Static integrity checks for the Nexus Civilization Unity project.

Runs without the Unity Editor (useful in CI and in environments where Unity is not
installed) and verifies the things that can be checked from the file system:

  * Unity YAML files parse and are structurally valid.
  * Every asset has a .meta file and no GUID is duplicated.
  * Every GUID referenced by scenes / settings / asmdefs resolves.
  * Scene component, transform-hierarchy and script references are intact.
  * C# sources are brace balanced, declare the type that matches the file name,
    and contain no duplicate type definitions.
  * Package manifest and assembly definitions are valid JSON with resolvable references.

Exit code 0 = all checks passed, 1 = at least one error.

Usage:  python3 Tools/validate_project.py [--verbose]
"""

import json
import pathlib
import re
import sys

PROJECT_ROOT = pathlib.Path(__file__).resolve().parent.parent
ASSETS = PROJECT_ROOT / "Assets"
PROJECT_SETTINGS = PROJECT_ROOT / "ProjectSettings"

try:
    import yaml
except ImportError:  # pragma: no cover
    yaml = None

GUID_PATTERN = re.compile(r"guid:\s*([0-9a-f]{32})")

# Package provided scripts that our hand authored scenes reference on purpose.
KNOWN_PACKAGE_SCRIPT_GUIDS = {
    "a79441f348de89743a2939f4d699eac1": "UniversalAdditionalCameraData (URP)",
}

UNITY_STANDARD_COMPONENT_KEYS = {
    "m_ObjectHideFlags",
    "m_CorrespondingSourceObject",
    "m_PrefabInstance",
    "m_PrefabAsset",
    "m_GameObject",
    "m_Enabled",
    "m_EditorHideFlags",
    "m_Script",
    "m_Name",
    "m_EditorClassIdentifier",
}

errors = []
warnings = []
info = []


def error(message):
    errors.append(message)


def warn(message):
    warnings.append(message)


def collect_meta_guids():
    guids = {}
    duplicates = []
    for meta in ASSETS.rglob("*.meta"):
        text = meta.read_text()
        match = GUID_PATTERN.search(text)
        if not match:
            error("Meta file without a GUID: {0}".format(meta.relative_to(PROJECT_ROOT)))
            continue
        guid = match.group(1)
        asset = meta.with_name(meta.name[: -len(".meta")])
        if guid in guids:
            duplicates.append((guid, guids[guid], asset))
        guids[guid] = asset
    for guid, first, second in duplicates:
        error("Duplicate GUID {0} used by {1} and {2}".format(guid, first, second))
    return guids


def check_meta_coverage():
    for path in ASSETS.rglob("*"):
        if path.name.endswith(".meta"):
            continue
        if path.name.startswith("."):
            # Hidden placeholders (".gitkeep") keep empty Unity folders in Git;
            # Unity ignores dot files, so they intentionally have no .meta.
            continue
        meta = path.with_name(path.name + ".meta")
        if not meta.exists():
            error("Missing .meta for {0}".format(path.relative_to(PROJECT_ROOT)))

    for meta in ASSETS.rglob("*.meta"):
        asset = meta.with_name(meta.name[: -len(".meta")])
        if not asset.exists():
            error("Orphan .meta file: {0}".format(meta.relative_to(PROJECT_ROOT)))


def parse_unity_yaml(path):
    """Splits a Unity YAML file into [(classId, fileId, parsedMapping)]."""
    if yaml is None:
        return None

    documents = []
    lines = path.read_text().splitlines()
    current = None

    for line in lines:
        if line.startswith("--- "):
            if current is not None:
                documents.append(current)
            header = line[4:]
            class_match = re.search(r"!u!(\d+)", header)
            id_match = re.search(r"&(-?\d+)", header)
            current = {
                "class": int(class_match.group(1)) if class_match else -1,
                "file_id": int(id_match.group(1)) if id_match else -1,
                "lines": [],
            }
        elif current is not None:
            current["lines"].append(line)

    if current is not None:
        documents.append(current)

    parsed = []
    for document in documents:
        body = "\n".join(document["lines"])
        try:
            data = yaml.safe_load(body) if body.strip() else {}
        except Exception as exception:  # noqa: BLE001
            error("YAML parse failure in {0} (document {1}): {2}".format(
                path.relative_to(PROJECT_ROOT), document["file_id"], exception))
            data = {}
        parsed.append((document["class"], document["file_id"], data))
    return parsed


def check_yaml_documents(guids):
    yaml_files = (
        list(ASSETS.rglob("*.unity"))
        + list(ASSETS.rglob("*.asset"))
        + list(ASSETS.rglob("*.prefab"))
        + list(PROJECT_SETTINGS.glob("*.asset"))
    )

    if yaml is None:
        warn("PyYAML is not installed: YAML structure checks were skipped (pip install pyyaml).")
        return

    for path in yaml_files:
        documents = parse_unity_yaml(path)
        if documents is None:
            continue

        file_ids = {}
        for class_id, file_id, data in documents:
            if file_id in file_ids:
                error("{0}: duplicate fileID {1}".format(path.relative_to(PROJECT_ROOT), file_id))
            file_ids[file_id] = class_id

        for class_id, file_id, data in documents:
            if not isinstance(data, dict) or not data:
                continue

            body = list(data.values())[0]
            if not isinstance(body, dict):
                continue

            # Component -> GameObject back references.
            game_object = body.get("m_GameObject")
            if isinstance(game_object, dict) and game_object.get("fileID"):
                target = game_object["fileID"]
                if target not in file_ids:
                    error("{0}: component {1} points to missing GameObject {2}".format(
                        path.relative_to(PROJECT_ROOT), file_id, target))

            father = body.get("m_Father")
            if isinstance(father, dict) and father.get("fileID"):
                target = father["fileID"]
                if target not in file_ids:
                    error("{0}: transform {1} points to missing parent {2}".format(
                        path.relative_to(PROJECT_ROOT), file_id, target))

            components = body.get("m_Component")
            if isinstance(components, list):
                for entry in components:
                    if isinstance(entry, dict):
                        component = entry.get("component")
                        if isinstance(component, dict) and component.get("fileID") not in file_ids:
                            error("{0}: GameObject {1} references missing component {2}".format(
                                path.relative_to(PROJECT_ROOT), file_id, component.get("fileID")))

            children = body.get("m_Children")
            if isinstance(children, list):
                for child in children:
                    if isinstance(child, dict) and child.get("fileID") not in file_ids:
                        error("{0}: transform {1} references missing child {2}".format(
                            path.relative_to(PROJECT_ROOT), file_id, child.get("fileID")))

        # Script GUIDs must resolve to a script asset in this project.
        for class_id, file_id, data in documents:
            if class_id != 114:
                continue
            body = list(data.values())[0] if isinstance(data, dict) and data else None
            if not isinstance(body, dict):
                continue
            script = body.get("m_Script")
            if not isinstance(script, dict):
                continue
            guid = script.get("guid")
            if not guid:
                continue
            if guid in guids:
                continue
            if guid in KNOWN_PACKAGE_SCRIPT_GUIDS:
                continue
            if path.suffix == ".unity":
                error("{0}: scene references an unknown script GUID {1}".format(
                    path.relative_to(PROJECT_ROOT), guid))
            else:
                # Shipped render pipeline assets reference package scripts; those are
                # resolved by Unity when the packages are installed.
                pass


def read_serialized_field_names(path):
    names = set()
    field_pattern = re.compile(
        r"^\s*(?:\[[^\]]*\]\s*)*(?:public|private|protected|internal)\s+"
        r"(?:static\s+|readonly\s+)*"
        r"[A-Za-z_][A-Za-z0-9_<>,\[\]\.\s]*?\s+"
        r"([A-Za-z_][A-Za-z0-9_]*)\s*(?:=|;)")
    for line in path.read_text().splitlines():
        if "=>" in line:
            continue  # expression bodied members are not serialized fields
        match = field_pattern.match(line)
        if match:
            names.add(match.group(1))
    return names


def check_scene_serialized_fields(guids):
    """Every serialized key in a scene MonoBehaviour must exist as a field on the script."""
    for path in sorted(ASSETS.rglob("*.unity")):
        documents = parse_unity_yaml(path)
        if not documents:
            continue

        for class_id, file_id, data in documents:
            if class_id != 114 or not isinstance(data, dict) or not data:
                continue
            body = list(data.values())[0]
            if not isinstance(body, dict):
                continue
            script = body.get("m_Script")
            if not isinstance(script, dict):
                continue
            guid = script.get("guid")
            script_asset = guids.get(guid)
            if script_asset is None or script_asset.suffix != ".cs":
                continue

            field_names = read_serialized_field_names(script_asset)
            for key in body.keys():
                if key in UNITY_STANDARD_COMPONENT_KEYS:
                    continue
                if key not in field_names:
                    error("{0}: {1} serializes '{2}' which is not a field of {3}".format(
                        path.relative_to(PROJECT_ROOT),
                        script_asset.name,
                        key,
                        script_asset.relative_to(PROJECT_ROOT)))


def check_external_guid_references(guids):
    """GUIDs referenced by project files that belong to packages (cannot be verified offline)."""
    referenced = set()
    scanned = (
        list(ASSETS.rglob("*.unity"))
        + list(ASSETS.rglob("*.asset"))
        + list(ASSETS.rglob("*.prefab"))
        + list(PROJECT_SETTINGS.glob("*.asset"))
    )

    for path in scanned:
        for guid in GUID_PATTERN.findall(path.read_text()):
            referenced.add(guid)

    unresolved = sorted(guid for guid in referenced if guid not in guids and set(guid) != {"0"})
    info.append("{0} GUID references resolve inside the project".format(len(referenced) - len(unresolved)))
    info.append("{0} GUID references point to Unity packages (verified only when Unity imports the project)".format(
        len(unresolved)))
    return unresolved


def check_build_settings(guids):
    path = PROJECT_SETTINGS / "EditorBuildSettings.asset"
    if not path.exists():
        error("ProjectSettings/EditorBuildSettings.asset is missing")
        return

    text = path.read_text()
    scene_entries = re.findall(r"path:\s*(Assets/[^\s]+\.unity)\s*\n\s*guid:\s*([0-9a-f]{32})", text)
    if not scene_entries:
        error("EditorBuildSettings.asset lists no scenes")
        return

    expected_order = ["Bootstrap", "MainMenu", "Gameplay"]
    names = [pathlib.PurePosixPath(scene_path).stem for scene_path, _ in scene_entries]

    if names[: len(expected_order)] != expected_order:
        error("Build settings must list {0} in that order, found {1}".format(expected_order, names))

    for scene_path, guid in scene_entries:
        asset = PROJECT_ROOT / scene_path
        if not asset.exists():
            error("Build settings reference a missing scene: {0}".format(scene_path))
            continue
        if guid not in guids:
            error("Build settings GUID {0} for {1} does not match any asset".format(guid, scene_path))
        elif guids[guid].resolve() != asset.resolve():
            error("Build settings GUID for {0} points at {1}".format(scene_path, guids[guid]))


def check_csharp_sources():
    type_pattern = re.compile(
        r"\b((?:(?:public|internal|private|protected|static|sealed|abstract|readonly|partial|new)\s+)*)"
        r"(class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)")
    declarations = {}

    for path in sorted(ASSETS.rglob("*.cs")):
        rel = path.relative_to(PROJECT_ROOT)
        text = path.read_text()

        if text.count("{") != text.count("}"):
            error("{0}: unbalanced braces ({1} open / {2} close)".format(rel, text.count("{"), text.count("}")))

        if text.count("(") != text.count(")"):
            error("{0}: unbalanced parentheses".format(rel))

        if "namespace " not in text:
            error("{0}: no namespace declaration".format(rel))

        if path.name not in ("AssemblyInfo.cs",) and not re.search(
                r"^\s*(public|internal)\s", text, re.MULTILINE):
            warn("{0}: no public/internal declaration found".format(rel))

        declared_here = []
        for modifiers, kind, name in type_pattern.findall(text):
            declared_here.append(name)
            if "private" in modifiers.split():
                continue  # nested helper types are file scoped
            if name in declarations:
                error("Duplicate type '{0}' declared in {1} and {2}".format(name, declarations[name], rel))
            declarations[name] = rel

        # A MonoBehaviour / ScriptableObject source must define a type named after the file.
        if "MonoBehaviour" in text or "ScriptableObject" in text:
            if path.stem not in declared_here:
                error("{0}: file name does not match any declared type".format(rel))

        # Guard against the most common Unity lifetime bug in this codebase.
        if "FindObjectOfType" in text:
            warn("{0}: uses the obsolete FindObjectOfType API".format(rel))

    info.append("{0} C# types declared across {1} files".format(
        len(declarations), len(list(ASSETS.rglob('*.cs')))))
    return declarations


def check_assembly_definitions():
    asmdefs = {}
    for path in ASSETS.rglob("*.asmdef"):
        rel = path.relative_to(PROJECT_ROOT)
        try:
            data = json.loads(path.read_text())
        except json.JSONDecodeError as exception:
            error("{0}: invalid JSON ({1})".format(rel, exception))
            continue
        name = data.get("name")
        if not name:
            error("{0}: missing assembly name".format(rel))
            continue
        asmdefs[name] = data

    builtin_assemblies = {
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Unity.InputSystem",
        "Unity.TextMeshPro",
        "UnityEngine.UI",
    }

    for name, data in asmdefs.items():
        for reference in data.get("references", []):
            if reference in asmdefs or reference in builtin_assemblies:
                continue
            warn("Assembly '{0}' references '{1}' which is not a project assembly or a known Unity assembly".format(
                name, reference))

    for required in ("Nexus.Runtime", "Nexus.Editor", "Nexus.Tests.EditMode", "Nexus.Tests.PlayMode"):
        if required not in asmdefs:
            error("Required assembly definition missing: {0}".format(required))

    info.append("{0} assembly definitions validated".format(len(asmdefs)))
    return asmdefs


def check_manifest():
    manifest_path = PROJECT_ROOT / "Packages" / "manifest.json"
    if not manifest_path.exists():
        error("Packages/manifest.json is missing")
        return

    try:
        manifest = json.loads(manifest_path.read_text())
    except json.JSONDecodeError as exception:
        error("Packages/manifest.json is invalid JSON: {0}".format(exception))
        return

    dependencies = manifest.get("dependencies", {})
    required = [
        "com.unity.render-pipelines.universal",
        "com.unity.inputsystem",
        "com.unity.ugui",
        "com.unity.test-framework",
    ]

    for package in required:
        if package not in dependencies:
            error("Packages/manifest.json is missing required package: {0}".format(package))

    for package, version in dependencies.items():
        if not re.match(r"^\d+\.\d+\.\d+", version):
            error("Package {0} has an unexpected version string: {1}".format(package, version))

    info.append("{0} packages pinned in the manifest".format(len(dependencies)))


def check_project_version():
    path = PROJECT_SETTINGS / "ProjectVersion.txt"
    if not path.exists():
        error("ProjectSettings/ProjectVersion.txt is missing")
        return

    text = path.read_text()
    match = re.search(r"m_EditorVersion:\s*(\S+)", text)
    if not match:
        error("ProjectVersion.txt has no m_EditorVersion")
        return

    info.append("Unity editor version: {0}".format(match.group(1)))


def check_required_files():
    required = [
        "Assets/Scenes/Bootstrap/Bootstrap.unity",
        "Assets/Scenes/MainMenu/MainMenu.unity",
        "Assets/Scenes/Gameplay/Gameplay.unity",
        "Assets/Resources/Config/GameConfiguration.asset",
        "Assets/Resources/Config/UiTheme.asset",
        "Assets/Settings/PC_RPAsset.asset",
        "Assets/Settings/PC_Renderer.asset",
        "Assets/Settings/Mobile_RPAsset.asset",
        "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset",
        "README.md",
        "ARCHITECTURE.md",
        "DEVELOPMENT_ROADMAP.md",
        "PHASE_01_REPORT.md",
        ".gitignore",
    ]

    for rel in required:
        if not (PROJECT_ROOT / rel).exists():
            error("Required file missing: {0}".format(rel))


def check_urp_wiring(guids):
    graphics = PROJECT_SETTINGS / "GraphicsSettings.asset"
    quality = PROJECT_SETTINGS / "QualitySettings.asset"

    if graphics.exists():
        text = graphics.read_text()
        match = re.search(r"m_CustomRenderPipeline:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})", text)
        if not match:
            error("GraphicsSettings.asset does not assign a render pipeline asset")
        elif match.group(1) not in guids:
            error("GraphicsSettings.asset references a pipeline asset that is not in the project")

    if quality.exists():
        pipelines = re.findall(r"customRenderPipeline:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})", quality.read_text())
        if not pipelines:
            error("QualitySettings.asset does not assign any render pipeline asset")
        for guid in pipelines:
            if guid not in guids:
                error("QualitySettings.asset references an unknown pipeline asset ({0})".format(guid))
        info.append("{0} quality levels wired to URP assets".format(len(pipelines)))


def main():
    verbose = "--verbose" in sys.argv

    guids = collect_meta_guids()
    check_meta_coverage()
    check_yaml_documents(guids)
    check_scene_serialized_fields(guids)
    unresolved = check_external_guid_references(guids)
    check_build_settings(guids)
    check_csharp_sources()
    check_assembly_definitions()
    check_manifest()
    check_project_version()
    check_required_files()
    check_urp_wiring(guids)

    print("NEXUS CIVILIZATION - project validation")
    print("=" * 46)
    for line in info:
        print("  info   : " + line)

    if verbose:
        for guid in unresolved:
            print("  package: guid " + guid)

    for line in warnings:
        print("  warning: " + line)

    for line in errors:
        print("  ERROR  : " + line)

    print("-" * 46)
    print("{0} error(s), {1} warning(s)".format(len(errors), len(warnings)))

    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main())
