"""Associate existing audio with the original character voice table, without inference."""
from __future__ import annotations

import argparse
import collections
import csv
import json
import os
from pathlib import Path
import re

from extract_reference import safe_relative, sha256, save_bytes, save_json, write_report, remove_empty_subdirectories


TABLE = "reverse/data/current/11_ボイス情報.x"


def parse_voice_table(text: str) -> list[dict]:
    lines = text.splitlines()
    if not lines or lines[0].strip() != "tree ボイス情報 = {":
        raise ValueError("Expected the original ボイス情報 tree")
    role = purpose = None
    references = []
    closed = False
    for number, line in enumerate(lines[1:], 2):
        if not line.strip():
            continue
        if line == "};":
            if role is not None or purpose is not None:
                raise ValueError("Unclosed role/purpose in voice table")
            closed = True
            continue
        if closed:
            raise ValueError("Content after the voice table")
        match = re.fullmatch(r"\t([^\t]+?) = \{", line)
        if match:
            if role is not None:
                raise ValueError(f"Nested role at line {number}")
            role = json.loads(match[1]) if match[1].startswith('"') else match[1]
            continue
        match = re.fullmatch(r"\t\t([^\t]+?) = \{", line)
        if match:
            if role is None or purpose is not None:
                raise ValueError(f"Invalid purpose at line {number}")
            purpose = json.loads(match[1]) if match[1].startswith('"') else match[1]
            continue
        match = re.fullmatch(r"\t\t\tvoice = \(list\) \{\s*(.*?)\s*\},", line)
        if match:
            if role is None or purpose is None:
                raise ValueError(f"Voice list without a role/purpose at line {number}")
            values = json.loads("[" + match[1] + "]")
            for position, value in enumerate(values):
                if type(value) not in (str, int) or str(value) == "":
                    raise ValueError(f"Invalid voice reference at line {number}")
                references.append({"role": role, "purpose": purpose, "reference": str(value),
                                   "source_table": TABLE, "source_line": number, "list_position": position})
            continue
        if line == "\t\t}," and purpose is not None:
            purpose = None
            continue
        if line == "\t}," and role is not None and purpose is None:
            role = None
            continue
        raise ValueError(f"Unexpected voice-table syntax at line {number}")
    if not closed or not references:
        raise ValueError("Voice table is incomplete or empty")
    return references


def plan_associations(references: list[dict], audio_rows: list[dict]) -> tuple[list[dict], list[dict]]:
    by_stem = collections.defaultdict(list)
    for row in audio_rows:
        if row["kind"] == "voice":
            by_stem[str(Path(row["original_name"]).with_suffix(""))].append(row)
    associations = []
    contexts = collections.defaultdict(list)
    for reference in references:
        matches = by_stem.get(reference["reference"], [])
        if len(matches) > 1:
            raise ValueError(f"Ambiguous original audio name: {reference['reference']}")
        association = dict(reference, status="matched" if matches else "not_in_voice_archive")
        if matches:
            row = matches[0]
            association.update(original_name=row["original_name"], sha256=row["sha256"])
            contexts[row["original_name"]].append(association)
        associations.append(association)
    plans = []
    for row in audio_rows:
        if row["kind"] != "voice":
            continue
        links = contexts.get(row["original_name"], [])
        relative = safe_relative(row["original_name"], split_fullwidth=False)
        if links:
            # A reused recording is stored once; every table context remains in the index.
            first = links[0]
            target = Path("assets/audio/voice/按角色") / safe_relative(first["role"], split_fullwidth=False)
            target /= safe_relative(first["purpose"], split_fullwidth=False)
            target /= relative
        else:
            target = Path("assets/audio/voice/未关联") / relative
        plans.append({"original_name": row["original_name"], "old_output": row["output"],
                      "output": str(target), "sha256": row["sha256"],
                      "status": "table_matched" if links else "not_in_this_table",
                      "associations": links})
        for link in links:
            link["output"] = str(target)
    return plans, associations


def write_csv(path: Path, rows: list[dict], columns: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".voice-new")
    with temporary.open("w", encoding="utf-8-sig", newline="") as output:
        writer = csv.DictWriter(output, fieldnames=columns)
        writer.writeheader()
        writer.writerows(rows)
    os.replace(temporary, path)


def organize(root: Path) -> dict:
    root = root.resolve(strict=True)
    voice_root = (root / "assets/audio/voice").resolve(strict=True)
    table = root / TABLE
    audio_report = root / "reverse/reports/audio_assets.json"
    audio_bytes = audio_report.read_bytes()
    audio_rows = json.loads(audio_bytes.decode("utf-8"))
    references = parse_voice_table(table.read_text(encoding="utf-8"))
    plans, associations = plan_associations(references, audio_rows)
    manifest = root / "assets/manifest.csv"
    manifest_bytes = manifest.read_bytes()
    with manifest.open(encoding="utf-8-sig", newline="") as input_file:
        reader = csv.DictReader(input_file)
        columns, manifest_rows = reader.fieldnames, list(reader)
    if sum(row["kind"] == "voice" for row in manifest_rows) != len(plans):
        raise ValueError("The asset manifest and audio report disagree on voice count")
    targets = set()
    for number, plan in enumerate(plans, 1):
        source, target = root / plan["old_output"], root / plan["output"]
        for path in (source, target):
            if voice_root not in path.resolve().parents:
                raise ValueError(f"Voice target outside its owned directory: {path}")
        key = str(target).casefold()
        if key in targets:
            raise ValueError(f"Colliding voice destinations: {target}")
        targets.add(key)
        existing = source if source.is_file() else target
        if not existing.is_file() or sha256(existing) != plan["sha256"]:
            raise ValueError(f"Missing or modified original voice file: {existing}")
        if source != target and source.exists() and target.exists():
            raise FileExistsError(f"Refusing to overwrite existing voice file: {target}")
        if number % 5000 == 0:
            print("VOICE PREFLIGHT", number, "/", len(plans), flush=True)
    backup = root / "reverse/reports/voice_table_before"
    if not (backup / "audio_assets.json").exists():
        save_bytes(backup / "audio_assets.json", audio_bytes)
        save_bytes(backup / "manifest.csv", manifest_bytes)
    write_report(root / "reverse/reports/voice_table_plan.json",
                 {"source_table": TABLE, "table_sha256": sha256(table), "plans": plans,
                  "associations": associations})
    moved = 0
    for number, plan in enumerate(plans, 1):
        source, target = root / plan["old_output"], root / plan["output"]
        if source != target and source.exists():
            target.parent.mkdir(parents=True, exist_ok=True)
            source.rename(target)
            moved += 1
        if number % 5000 == 0:
            print("VOICE ORGANIZE", number, "/", len(plans), flush=True)
    destinations = {p["original_name"]: p["output"] for p in plans}
    for row in audio_rows:
        if row["kind"] == "voice":
            row["output"] = destinations[row["original_name"]]
    for row in manifest_rows:
        if row["kind"] == "voice":
            row["output"] = destinations[row["original_name"]]
    write_report(audio_report, audio_rows)
    write_csv(manifest, manifest_rows, columns)
    index = voice_root / "角色用途索引.csv"
    association_columns = ["role", "purpose", "reference", "status", "original_name", "output", "sha256",
                           "source_table", "source_line", "list_position"]
    write_csv(index, associations, association_columns)
    write_csv(voice_root / "缺失引用.csv", [r for r in associations if r["status"] != "matched"], association_columns)
    write_report(voice_root / "角色用途索引.json", associations)
    write_report(voice_root / "全部语音索引.json", plans)
    removed = remove_empty_subdirectories(voice_root)
    errors = []
    for number, plan in enumerate(plans, 1):
        target = root / plan["output"]
        if not target.is_file() or sha256(target) != plan["sha256"]:
            errors.append(plan["output"])
        if number % 5000 == 0:
            print("VOICE VERIFY", number, "/", len(plans), flush=True)
    counts = collections.Counter(p["status"] for p in plans)
    physical = len(list(voice_root.rglob("*.ogg")))
    report = {"source_table": TABLE, "table_sha256": sha256(table),
              "role_groups": len({r["role"] for r in references}), "table_references": len(references),
              "matched_references": sum(r["status"] == "matched" for r in associations),
              "missing_references": [r for r in associations if r["status"] != "matched"],
              "voice_file_counts": dict(counts), "physical_voice_files": physical,
              "moved_files": moved, "sha256_verified": len(plans) - len(errors), "file_errors": errors,
              "removed_empty_directories": [str(p.relative_to(root)) for p in removed],
              "dialogue_text_inferred": False, "story_scene_inferred": False,
              "other_voice_tables_used": False}
    write_report(root / "reverse/reports/voice_table_association.json", report)
    if errors or physical != len(plans):
        raise RuntimeError("Voice association verification failed; retained plan and backup are available")
    print("VOICE TABLE COMPLETE", json.dumps({k: v for k, v in report.items() if k != "missing_references"},
                                           ensure_ascii=False), flush=True)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True)
    organize(parser.parse_args().root)


if __name__ == "__main__":
    main()
