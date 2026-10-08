"""Create a separate DohnaDohna Studio project using only host mixer scaffolding.

No historical event, asset or custom bank identity is imported. The Master identity
and required mixer graph remain those of the host-compatible minimal project.
"""
import hashlib
import json
import shutil
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
target = ROOT / ".local/fmod"
reference = ROOT / "Reference/LocalAuthoring/FMOD/Metadata"
original = Path(json.loads((ROOT / ".local/original-reference.json").read_text(encoding="utf-8-sig"))["referenceDirectory"])
receipt = json.loads((ROOT / "build/asset-receipt.json").read_text(encoding="utf-8"))
target.mkdir(parents=True, exist_ok=True)
project = target / "DohnaDohna.fspro"
if not project.exists():
    project.write_text('<?xml version="1.0" encoding="UTF-8"?>\n<objects serializationModel="Studio.02.03.00" />\n', encoding="utf-8")
    metadata = target / "Metadata"
    metadata.mkdir(exist_ok=True)
    for name in ("Master.xml", "Mixer.xml", "Tags.xml", "Workspace.xml"):
        shutil.copy2(reference / name, metadata / name)
    for name in ("Group", "Return", "SnapshotGroup", "Platform", "EncodingSetting", "BankFolder", "EffectPresetFolder",
                 "ParameterPresetFolder", "ProfilerFolder", "SandboxFolder"):
        if (reference / name).is_dir():
            shutil.copytree(reference / name, metadata / name, dirs_exist_ok=True)
    master_bank = reference / "Bank/{5d95f0d5-57ba-451e-8d05-a41c9bc3813e}.xml"
    (metadata / "Bank").mkdir(exist_ok=True)
    shutil.copy2(master_bank, metadata / "Bank" / master_bank.name)
    workspace = ET.parse(metadata / "Workspace.xml")
    value = workspace.find(".//property[@name='builtBanksOutputDirectory']/value")
    value.text = "Build"
    workspace.write(metadata / "Workspace.xml", encoding="utf-8", xml_declaration=True)
    # Empty authoring roots with fresh identities; never bring old custom events.
    ws = workspace.find(".//object[@class='Workspace']")
    for relation, kind, directory in (("masterEventFolder", "MasterEventFolder", "EventFolder"),
                                      ("masterAssetFolder", "MasterAssetFolder", "Asset")):
        identity = "{" + str(uuid.uuid4()) + "}"
        ws.find(f"relationship[@name='{relation}']/destination").text = identity
        folder = metadata / directory
        folder.mkdir(exist_ok=True)
        root = ET.Element("objects", serializationModel="Studio.02.03.00")
        obj = ET.SubElement(root, "object", {"class": kind, "id": identity})
        prop = ET.SubElement(obj, "property", name="name")
        ET.SubElement(prop, "value").text = "Master"
        ET.ElementTree(root).write(folder / (identity + ".xml"), encoding="utf-8", xml_declaration=True)
    workspace.write(metadata / "Workspace.xml", encoding="utf-8", xml_declaration=True)

assets = target / "Assets"
assets.mkdir(exist_ok=True)
mapping = {}
for name, relative in receipt["audio"].items():
    source = original / relative.replace("\\", "/")
    if not source.is_file():
        raise FileNotFoundError(source)
    key = hashlib.sha256(name.encode()).hexdigest()[:20]
    destination = assets / (key + source.suffix)
    if not destination.exists():
        shutil.copy2(source, destination)
    mapping[name] = {"asset": str(destination).replace("\\", "/"), "event": "event:/DohnaDohna/" + key}
(target / "events.json").write_text(json.dumps(mapping, ensure_ascii=False, indent=2), encoding="utf-8")
script = (ROOT / "tools/Build-Fmod.js").read_text(encoding="utf-8")
(target / "build-events.js").write_text("var audioRows = " + json.dumps(list(mapping.values()), ensure_ascii=False) + ";\n" + script, encoding="utf-8")
print(f"Prepared {len(mapping)} uniquely named audio events in {project}")
