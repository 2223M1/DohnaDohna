"""Read-only game extraction; all derived commercial content stays outside the mod.

Requires the portable alice-tools executable and an isolated Python with Pillow,
PyAV and pefile. Never executes a game binary. Outputs are resumable and existing
different files are rejected instead of overwritten.
"""
from __future__ import annotations

import argparse
import bisect
import collections
import concurrent.futures
import csv
import datetime as dt
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import time
import zlib


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(8 * 1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def save_bytes(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        if path.read_bytes() != data:
            raise FileExistsError(f"Refusing to overwrite different output: {path}")
        return
    with path.open("xb") as f:
        f.write(data)


def save_json(path: Path, value) -> None:
    save_bytes(path, (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))


def write_report(path: Path, value) -> None:
    """Replace only this runner's generated status reports, not extracted content."""
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".new")
    tmp.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    os.replace(tmp, path)


def safe_relative(name: str, *, split_fullwidth: bool = True) -> Path:
    if split_fullwidth:
        name = name.replace("／", "/")
    name = name.replace("\\", "/")
    pieces = name.split("/")
    if not name or name.startswith("/") or any(p in ("", ".", "..") for p in pieces):
        raise ValueError(f"Unsafe resource path: {name!r}")
    safe = []
    for part in pieces:
        part = re.sub(r'[<>:"|?*\x00-\x1f]', lambda m: f"%{ord(m[0]):02X}", part)
        if part.endswith((" ", ".")):
            part = part.rstrip(" .") + "__tail"
        if re.fullmatch(r"(?i)(con|prn|aux|nul|com[1-9]|lpt[1-9])(\..*)?", part):
            part = "_" + part
        safe.append(part)
    return Path(*safe)


def remove_empty_subdirectories(root: Path) -> list[Path]:
    """Remove empty descendants only; preserve the root and every file."""
    root = root.resolve(strict=True)
    if not root.is_dir():
        raise NotADirectoryError(root)
    directories = sorted((p for p in root.rglob("*") if p.is_dir()),
                         key=lambda p: len(p.parts), reverse=True)
    removed = []
    for directory in directories:
        if directory.is_symlink() or directory.is_junction() or root not in directory.resolve().parents:
            raise ValueError(f"Empty-directory cleanup target left its owned directory: {directory}")
        if next(directory.iterdir(), None) is None:
            directory.rmdir()
            removed.append(directory)
    return removed


def classify_image(name: str, known_actors: set[str] | None = None) -> tuple[str, Path]:
    relative = safe_relative(name)
    prefix = relative.parts[0]
    categories = {"立绘": "立绘", "立ち絵": "立绘", "表情": "立绘", "顔": "立绘",
                  "背景": "场景与背景", "地图": "场景与背景", "マップ": "场景与背景",
                  "系统": "界面与图标", "パネル": "界面与图标", "サムネイル": "界面与图标",
                  "物品": "界面与图标", "名札": "界面与图标", "アイコン": "界面与图标",
                  "特效": "特效", "エフェクト": "特效", "事件": "剧情图像"}
    if prefix in categories:
        category = categories[prefix]
        if category == prefix and len(relative.parts) > 1:
            relative = Path(*relative.parts[1:])
        return category, relative
    if "特效" in prefix or prefix.startswith("超必杀演出"):
        return "特效", relative
    if prefix.startswith("打斗背景"):
        return "场景与背景", relative
    if prefix in ("キー待ちマーク", "信息窗口", "情感图标", "新手教程", "物品图标配置用") or "信息窗口" in relative.parts:
        return "界面与图标", relative
    if prefix.startswith("顔"):
        return "立绘", relative
    if re.search(r"(?i)(effect|(^|_)fx($|_)|エフェクト)", name):
        return "特效", relative
    actions = r"超攻击|超攻撃|攻击|攻撃|待机|待機|受击|受擊|死亡|倒地|登场|出场|移动|移動|ジャンプ|ダウン|アタック|歩行|構え|防御|胜利|跳跃|回避"
    if len(relative.parts) >= 2 and re.search(actions, relative.parts[1]):
        return "角色与敌人", relative
    if prefix.endswith("ダウン") or (known_actors and prefix in known_actors):
        return "角色与敌人", relative
    if re.search(r"[＿_](?:" + actions + ")", prefix):
        return "角色与敌人", relative
    if len(relative.parts) >= 3 and re.fullmatch(r"[０-９0-9A-Za-z_\-]+", relative.stem):
        return "角色与敌人", relative
    return "待确认", relative


class AfaArchive:
    """AFA v2 with bounded virtual reads across the original split files."""
    def __init__(self, parts: list[Path]):
        self.parts = parts
        self.streams = [p.open("rb") for p in parts]
        self.sizes = [p.stat().st_size for p in parts]
        self.starts = [0]
        for size in self.sizes[:-1]:
            self.starts.append(self.starts[-1] + size)
        self.total_size = sum(self.sizes)
        try:
            self._load_index()
        except BaseException:
            self.close()
            raise

    def _load_index(self):
        header = self.read(0, 44)
        if header[:4] != b"AFAH" or header[8:16] != b"AlicArch" or header[28:32] != b"INFO":
            raise ValueError(f"Not an AFA archive: {self.parts}")
        self.version = struct.unpack_from("<I", header, 16)[0]
        if self.version != 2:
            raise ValueError(f"Only inspected AFA v2 is supported, got {self.version}")
        self.data_offset, info_size, unpacked_size, count = (
            struct.unpack_from("<I", header, x)[0] for x in (24, 32, 36, 40)
        )
        if info_size < 16 or unpacked_size > 256 * 1024 * 1024 or count > 1000000:
            raise ValueError("Invalid AFA directory header")
        toc = zlib.decompress(self.read(44, info_size - 16))
        if len(toc) != unpacked_size:
            raise ValueError("AFA directory size mismatch")
        pos = 0
        self.entries = []
        for i in range(count):
            if pos + 8 > len(toc):
                raise ValueError("Truncated AFA directory")
            length, padded = struct.unpack_from("<II", toc, pos)
            if not length or length > padded or pos + 8 + padded + 16 > len(toc):
                raise ValueError("Invalid AFA directory entry")
            name_bytes = toc[pos + 8:pos + 8 + length]
            name = name_bytes.decode("gb18030")
            pos += 8 + padded
            offset, size = struct.unpack_from("<II", toc, pos + 8)
            pos += 16
            absolute = self.data_offset + offset
            if absolute + size > self.total_size:
                raise ValueError(f"Archive entry outside source files: {name}")
            safe_relative(name)
            self.entries.append({"index": i, "name": name, "offset": absolute,
                                 "size": size, "extension": Path(name).suffix.lower(),
                                 "name_bytes_hex": name_bytes.hex()})
        if pos != len(toc):
            raise ValueError("Unconsumed AFA directory bytes")

    def read(self, offset: int, size: int) -> bytes:
        if offset < 0 or size < 0 or offset + size > self.total_size:
            raise ValueError("Out-of-bounds archive read")
        result = bytearray()
        while size:
            part = bisect.bisect_right(self.starts, offset) - 1
            local = offset - self.starts[part]
            take = min(size, self.sizes[part] - local)
            if take <= 0:
                raise ValueError("Invalid split-file boundary")
            self.streams[part].seek(local)
            block = self.streams[part].read(take)
            if len(block) != take:
                raise ValueError("Short source archive read")
            result.extend(block)
            offset += take
            size -= take
        return bytes(result)

    def close(self):
        for stream in self.streams:
            stream.close()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()


def png_info(path: Path) -> dict:
    from PIL import Image
    with Image.open(path) as image:
        image.load()
        width, height = image.size
        rgba = image.convert("RGBA")
        h = hashlib.sha256(struct.pack("<II", width, height) + rgba.tobytes()).hexdigest()
        return {"width": width, "height": height, "mode": image.mode,
                "pixel_sha256": h, "sha256": sha256(path)}


def audio_info(path: Path) -> dict:
    import av
    with av.open(str(path)) as container:
        if len(container.streams.audio) != 1:
            raise ValueError("Expected one audio stream")
        stream = container.streams.audio[0]
        samples = 0
        for frame in container.decode(stream):
            samples += frame.samples
        if samples <= 0 or not stream.codec_context.sample_rate:
            raise ValueError("No decoded audio samples")
        rate = stream.codec_context.sample_rate
        return {"codec": stream.codec_context.name, "sample_rate": rate,
                "channels": len(stream.codec_context.layout.channels),
                "samples": samples, "duration_seconds": round(samples / rate, 6)}


def normalize_json_export(data: bytes) -> tuple[bytes, list[dict]]:
    """alice JSON retains GB18030 bytes in some name2 fields; preserve originals."""
    try:
        json.loads(data.decode("utf-8"))
        return data, []
    except UnicodeDecodeError:
        pass
    changes = []
    def normalize(match):
        token = match[0]
        try:
            token.decode("utf-8")
            return token
        except UnicodeDecodeError:
            unescaped = json.loads(token.decode("latin1"))
            raw = unescaped.encode("latin1")
            try:
                value = raw.decode("gb18030")
                encoding = "gb18030"
            except UnicodeDecodeError:
                value = raw.decode("gb18030", errors="backslashreplace")
                encoding = "gb18030_with_explicit_byte_escapes"
            changes.append({"offset": match.start(), "original_hex": raw.hex(), "decoded_with": encoding})
            return json.dumps(value, ensure_ascii=False).encode("utf-8")
    output = re.sub(rb'"(?:[^"\\]|\\.)*"', normalize, data)
    json.loads(output.decode("utf-8"))
    return output, changes


class Extractor:
    def __init__(self, game: Path, out: Path, alice: Path, workers: int):
        self.game, self.out, self.alice, self.workers = game.resolve(), out.resolve(), alice.resolve(), workers
        if self.out == self.game or self.game in self.out.parents or self.out in self.game.parents:
            raise ValueError("Output must be separate from the original game")
        self.assets, self.reverse = out / "assets", out / "reverse"
        self.work = out / "tools" / "work-cache"
        self.logs = self.reverse / "reports" / "logs"
        for directory in (self.assets, self.reverse, self.work, self.logs):
            directory.mkdir(parents=True, exist_ok=True)
        self.rows = []
        self.errors = []

    def command(self, label: str, args: list[str], *, cwd: Path | None = None) -> int:
        log = self.logs / (label + ".log")
        env = dict(os.environ, PYTHONIOENCODING="utf-8")
        with log.open("wb") as f:
            result = subprocess.run([str(self.alice), *args, "--input-encoding", "GB18030",
                                     "--output-encoding", "UTF-8"], cwd=cwd, stdout=f,
                                    stderr=subprocess.STDOUT, env=env, check=False)
        if result.returncode:
            self.errors.append({"stage": label, "exit_code": result.returncode,
                                "log": str(log.relative_to(self.out))})
        return result.returncode

    def archive_defs(self):
        return {"cg_current": [self.game / "dohnadohnaCG.afa"],
                "cg_split": [self.game / "001.bin", self.game / "002.bin"],
                "sound": [self.game / "dohnadohnaSound.afa"],
                "voice": [self.game / "dohnadohnaVoice.afa"],
                "pact": [self.game / "dohnadohnaPact.afa"]}

    def inventory(self):
        target = self.reverse / "source_manifest.json"
        if target.exists():
            inventory = json.loads(target.read_text(encoding="utf-8"))
            for item in inventory["files"]:
                source = self.game / item["path"]
                if not source.exists() or source.stat().st_size != item["bytes"] or sha256(source) != item["sha256"]:
                    raise ValueError(f"Source changed since inventory: {source}")
            return inventory
        files = []
        for source in sorted(self.game.rglob("*")):
            if source.is_file():
                files.append({"path": source.relative_to(self.game).as_posix(),
                              "bytes": source.stat().st_size, "sha256": sha256(source)})
        archives = {}
        for key, paths in self.archive_defs().items():
            with AfaArchive(paths) as archive:
                archives[key] = {"parts": [p.relative_to(self.game).as_posix() for p in paths],
                                 "version": archive.version, "count": len(archive.entries),
                                 "extensions": dict(collections.Counter(e["extension"] for e in archive.entries)),
                                 "trailing_bytes": archive.total_size - max(e["offset"] + e["size"] for e in archive.entries)}
                save_json(self.reverse / "archive_indices" / (key + ".json"), archive.entries)
        inventory = {"game_directory": str(self.game), "created_at": dt.datetime.now().astimezone().isoformat(),
                     "files": files, "archives": archives,
                     "tools": {"alice": str(self.alice), "alice_sha256": sha256(self.alice),
                               **{p: importlib.metadata.version(p) for p in ("Pillow", "av", "pefile")}}}
        save_json(target, inventory)
        print("INVENTORY", json.dumps(archives, ensure_ascii=False), flush=True)
        return inventory

    def split_archive(self) -> Path:
        target = self.work / "split-cg.afa"
        parts = self.archive_defs()["cg_split"]
        expected_size = sum(p.stat().st_size for p in parts)
        if target.exists():
            if target.stat().st_size != expected_size:
                raise ValueError("Incomplete reconstructed split archive; preserved for inspection")
            return target
        with target.open("xb") as output:
            for source in parts:
                with source.open("rb") as f:
                    shutil.copyfileobj(f, output, length=8 * 1024 * 1024)
        return target

    def smoke(self):
        result = []
        for label, paths in (("current", self.archive_defs()["cg_current"]),
                             ("split", self.archive_defs()["cg_split"])):
            source = paths[0] if label == "current" else self.split_archive()
            with AfaArchive(paths) as archive:
                for extension in (".ajp", ".qnt", ".dcf", ".pcf"):
                    entry = next(e for e in archive.entries if e["extension"] == extension)
                    target = self.work / "smoke" / f"{label}_{entry['index']}_{extension[1:]}.png"
                    target.parent.mkdir(parents=True, exist_ok=True)
                    if not target.exists():
                        code = self.command(f"smoke_{label}_{extension[1:]}",
                                            ["ar", "extract", "--index", str(entry["index"]),
                                             "--output", str(target), str(source)])
                        if code:
                            raise RuntimeError(f"Image smoke conversion failed: {label} {extension}")
                    result.append({"archive": label, "entry": entry["index"], "extension": extension,
                                   "original_name": entry["name"], **png_info(target)})
        save_json(self.reverse / "reports" / "image_smoke.json", result)
        print("SMOKE", len(result), "image formats decoded", flush=True)

    def dump_scripts(self):
        directory = self.reverse / "scripts"
        directory.mkdir(parents=True, exist_ok=True)
        source = self.game / "dohnadohna.ain"
        requests = [("metadata.json", "--json"), ("code.jam", "--code"),
                    ("code_raw.jam", "--raw-code"), ("text.txt", "--text"),
                    ("functions.txt", "--functions"), ("structures.txt", "--structures"),
                    ("globals.txt", "--globals"), ("libraries.txt", "--libraries"),
                    ("function_types.txt", "--function-types"),
                    ("delegates.txt", "--delegates"), ("enums.txt", "--enums"),
                    ("map.txt", "--map"), ("messages.txt", "--messages"),
                    ("strings.txt", "--strings"), ("decompressed.ain", "--decrypt")]
        for filename, option in requests:
            target = directory / filename
            if target.exists() and target.stat().st_size:
                continue
            if self.command("ain_" + Path(filename).stem,
                            ["ain", "dump", option, "--output", str(target), str(source)]):
                raise RuntimeError(f"AIN export failed: {filename}")
        metadata_path = directory / "metadata.json"
        original_json = metadata_path.read_bytes()
        normalized, changes = normalize_json_export(original_json)
        if changes:
            save_bytes(directory / "raw_exports" / "metadata.original.bin", original_json)
            metadata_path.write_bytes(normalized)
            save_json(self.reverse / "reports" / "metadata_encoding_normalization.json", changes)
        metadata = json.loads(normalized.decode("utf-8"))
        filenames = metadata.get("filenames", [])
        partial_filenames = directory / "filenames.txt"
        partial_archive = directory / "raw_exports" / "filenames.guessed.partial.bin"
        if partial_filenames.exists():
            partial_archive.parent.mkdir(parents=True, exist_ok=True)
            if partial_archive.exists():
                raise FileExistsError("An archived partial filename export already exists")
            partial_filenames.rename(partial_archive)
        save_bytes(directory / "filenames.from_metadata.txt",
                   ("; Authoritative FNAM table from metadata; an empty table means names are not present.\n" +
                    "\n".join(f"{i}\t{name}" for i, name in enumerate(filenames)) + "\n").encode("utf-8"))
        write_report(self.reverse / "reports" / "filename_recovery.json",
                  {"authoritative_filename_count": len(filenames),
                   "cli_filename_guess_status": "FAILED: iconv rejected an inferred filename; partial output retained",
                   "partial_output": "reverse/scripts/raw_exports/filenames.guessed.partial.bin",
                   "guessing_used_for_function_index": False})
        save_json(self.reverse / "reports" / "ain_metadata_shape.json",
                  {k: len(v) if isinstance(v, (list, dict)) else v for k, v in metadata.items()})
        self.split_functions(metadata)
        print("AIN", "bytecode, metadata and text exported", flush=True)

    def split_functions(self, metadata):
        directory = self.reverse / "scripts"
        code = (directory / "code.jam").read_text(encoding="utf-8")
        definitions = list(re.finditer(r"(?m)^FUNC (\d+)\r?$", code))
        functions = {f["index"]: f for f in metadata["functions"]}
        found = set()
        records = []
        for number, match in enumerate(definitions):
            index = int(match[1])
            if index in found or index not in functions:
                raise ValueError(f"Invalid or repeated bytecode function index: {index}")
            found.add(index)
            end = definitions[number + 1].start() if number + 1 < len(definitions) else len(code)
            block = code[match.start():end]
            terminator = re.search(r"(?m)^ENDFUNC(?:[^\n]*)\n?", block)
            if terminator:
                block = block[:terminator.end()]
            function = functions[index]
            target = directory / "functions" / f"f{index:05d}.jam"
            save_bytes(target, (f"; {function['name']}\n; AIN function {index}; address 0x{function['address']:08X}\n" + block).encode("utf-8"))
            records.append({"index": index, "name": function["name"], "address": function["address"],
                            "arguments": len(function["arguments"]),
                            "variables": len(function["variables"]),
                            "output": str(target.relative_to(self.out))})
        absent = [f["index"] for f in functions.values() if f["address"] != 0xFFFFFFFF and f["index"] not in found]
        save_json(directory / "function_index.json", records)
        with (directory / "function_index.csv").open("w", encoding="utf-8-sig", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=("index", "name", "address", "arguments", "variables", "output"))
            writer.writeheader(); writer.writerows(records)
        write_report(self.reverse / "reports" / "ain_coverage.json",
                     {"declared_functions": len(functions), "bytecode_function_bodies": len(found),
                      "no_body_sentinels": [f["index"] for f in functions.values() if f["address"] == 0xFFFFFFFF and f["index"] not in found],
                      "sentinel_with_bytecode_body": [f["index"] for f in functions.values() if f["address"] == 0xFFFFFFFF and f["index"] in found],
                      "missing_function_bodies": absent, "bytecode_split_is_high_level_source": False})
        if absent:
            raise ValueError(f"AIN function bodies are missing: {absent[:10]}")

    def dump_data(self):
        data_root = self.reverse / "data"
        variants = [("current", self.game / "dohnadohnaEx.ex")]
        variants.extend(("variants/" + p.parent.name, p) for p in
                        sorted((self.game / "多娜多娜修改器").rglob("*.ex")))
        for label, source in variants:
            directory = data_root / safe_relative(label)
            directory.mkdir(parents=True, exist_ok=True)
            raw = directory / "original.ex"
            save_bytes(raw, source.read_bytes())
            target = directory / "data.x"
            if not target.exists():
                self.command("ex_" + label.replace("/", "_"),
                             ["ex", "dump", "--split", "--output", str(target), str(raw)], cwd=directory)
        for label in ("pact", "sound", "voice"):
            with AfaArchive(self.archive_defs()[label]) as archive:
                for entry in archive.entries:
                    if entry["extension"] not in (".ex", ".pactex"):
                        continue
                    raw = data_root / label / safe_relative(entry["name"])
                    save_bytes(raw, archive.read(entry["offset"], entry["size"]))
                    target = raw.with_suffix(".x")
                    if not target.exists():
                        self.command(f"{label}_data_{entry['index']}",
                                     ["ex", "dump", "--output", str(target), str(raw)], cwd=raw.parent)
        shader = self.game / "Data" / "Shader.slk"
        save_bytes(data_root / "shaders" / "Shader.slk", shader.read_bytes())
        save_json(data_root / "shaders" / "status.json",
                  {"status": "raw_preserved", "sha256": sha256(shader),
                   "header_hex": shader.read_bytes()[:64].hex(),
                   "note": "SLK shader container is preserved. No recovered HLSL/GLSL source is claimed."})
        write_report(self.reverse / "reports" / "data_export.json", {"errors": self.errors})
        print("DATA", "main/variant tables, Pact definitions and audio mappings exported", flush=True)

    def converted_archive(self, label: str, source: Path) -> Path:
        target = self.work / ("converted_" + label)
        target.mkdir(parents=True, exist_ok=True)
        marker = target / ".conversion_complete.json"
        if not marker.exists():
            code = self.command("convert_" + label,
                                ["ar", "extract", "--images-only", "--output", str(target), str(source)])
            from PIL import Image
            parts = self.archive_defs()["cg_current" if label == "current" else "cg_split"]
            with AfaArchive(parts) as archive:
                entries = archive.entries
            repairs = []
            for entry in entries:
                png = target / safe_relative(entry["name"] + ".png", split_fullwidth=False)
                try:
                    with Image.open(png) as image:
                        image.verify()
                except Exception:
                    if png.exists():
                        quarantine = self.work / "incomplete" / label / f"e{entry['index']:05d}.png"
                        quarantine.parent.mkdir(parents=True, exist_ok=True)
                        if quarantine.exists():
                            raise FileExistsError(f"Already preserved incomplete conversion: {quarantine}")
                        png.rename(quarantine)
                    repairs.append((entry, png))
            print("IMAGE REPAIR", label, len(repairs), "missing/incomplete outputs", flush=True)
            def repair(job):
                entry, png = job
                png.parent.mkdir(parents=True, exist_ok=True)
                rc = self.command(f"repair_{label}_{entry['index']}",
                                  ["ar", "extract", "--index", str(entry["index"]),
                                   "--output", str(png), str(source)])
                return {"index": entry["index"], "name": entry["name"], "exit_code": rc}
            with concurrent.futures.ThreadPoolExecutor(max_workers=self.workers) as pool:
                repair_results = list(pool.map(repair, repairs))
            write_report(self.reverse / "reports" / ("image_conversion_" + label + ".json"),
                         {"bulk_exit_code": code, "repairs": repair_results})
            save_json(marker, {"source_sha256": sha256(source), "alice_sha256": sha256(self.alice),
                               "repair_failures": [r for r in repair_results if r["exit_code"]]})
        return target

    def image_assets(self):
        if (self.reverse / "reports" / "image_classification.json").exists():
            self.inventory()
            print("IMAGES already extracted and organized; existing delivery preserved", flush=True)
            return
        current_pixels = {}
        all_pixels = {}
        results = []
        for label, paths in (("current", self.archive_defs()["cg_current"]),
                             ("variants", self.archive_defs()["cg_split"])):
            source = paths[0] if label == "current" else self.split_archive()
            converted = self.converted_archive(label, source)
            with AfaArchive(paths) as archive:
                jobs = []
                names_used = set()
                for entry in archive.entries:
                    raw = self.assets / "raw" / label / safe_relative(entry["name"], split_fullwidth=False)
                    save_bytes(raw, archive.read(entry["offset"], entry["size"]))
                    png = converted / safe_relative(entry["name"] + ".png", split_fullwidth=False)
                    jobs.append((entry, raw, png))
                def decode(job):
                    entry, raw, png = job
                    try:
                        return job, png_info(png), None
                    except Exception as error:
                        return job, None, str(error)
                with concurrent.futures.ThreadPoolExecutor(max_workers=self.workers) as pool:
                    for number, (job, info, error) in enumerate(pool.map(decode, jobs), 1):
                        entry, raw, png = job
                        row = {"kind": "image", "source": "+".join(str(p.name) for p in paths),
                               "index": entry["index"], "original_name": entry["name"],
                               "raw": str(raw.relative_to(self.out)), "status": "failed" if error else "ok"}
                        if error:
                            row["error"] = error
                            self.errors.append(row)
                        else:
                            key = str(safe_relative(entry["name"]).with_suffix("")).casefold()
                            duplicate = current_pixels.get(key, {}).get(info["pixel_sha256"])
                            if label == "variants" and duplicate is None:
                                duplicate = all_pixels.get(info["pixel_sha256"])
                            if label == "variants" and duplicate is not None:
                                row.update(info, status="duplicate", duplicate_of=duplicate)
                            else:
                                relative = safe_relative(entry["name"]).with_suffix(".png")
                                name_key = relative.as_posix().casefold()
                                if name_key in names_used or len(str(self.assets / "images" / label / relative)) > 235:
                                    relative = Path("_indexed") / f"e{entry['index']:05d}_{relative.name[-90:]}"
                                names_used.add(relative.as_posix().casefold())
                                target = self.assets / "images" / label / relative
                                save_bytes(target, png.read_bytes())
                                output = str(target.relative_to(self.out))
                                row.update(info, output=output)
                                all_pixels.setdefault(info["pixel_sha256"], output)
                                if label == "current":
                                    current_pixels.setdefault(key, {})[info["pixel_sha256"]] = output
                        results.append(row)
                        if number % 1000 == 0:
                            print("IMAGES", label, number, "/", len(jobs), flush=True)
        self.rows.extend(results)
        write_report(self.reverse / "reports" / "image_assets.json", results)

    def audio_assets(self):
        if (self.reverse / "reports" / "voice_table_association.json").exists():
            self.inventory()
            print("AUDIO already extracted and voice-table-associated; existing delivery preserved", flush=True)
            return
        results = []
        for label in ("sound", "voice"):
            paths = self.archive_defs()[label]
            jobs = []
            with AfaArchive(paths) as archive:
                for entry in archive.entries:
                    if entry["extension"] != ".ogg":
                        continue
                    relative = safe_relative(entry["name"])
                    kind = "voice" if label == "voice" else "music" if relative.parts[0] in ("音乐", "音楽", "BGM") else "sound"
                    if kind == "music" and len(relative.parts) > 1:
                        relative = Path(*relative.parts[1:])
                    target = self.assets / "audio" / kind / relative
                    data = archive.read(entry["offset"], entry["size"])
                    save_bytes(target, data)
                    jobs.append((entry, target, kind, hashlib.sha256(data).hexdigest()))
            def decode(job):
                entry, target, kind, digest = job
                row = {"kind": kind, "source": paths[0].name, "index": entry["index"],
                       "original_name": entry["name"], "output": str(target.relative_to(self.out)),
                       "sha256": digest, "status": "ok"}
                try:
                    row.update(audio_info(target))
                except Exception as error:
                    row.update(status="failed", error=str(error))
                return row
            with concurrent.futures.ThreadPoolExecutor(max_workers=self.workers) as pool:
                for number, row in enumerate(pool.map(decode, jobs), 1):
                    results.append(row)
                    if row["status"] != "ok":
                        self.errors.append(row)
                    if number % 2000 == 0:
                        print("AUDIO", label, number, "/", len(jobs), flush=True)
        self.rows.extend(results)
        write_report(self.reverse / "reports" / "audio_assets.json", results)

    def loose_assets(self):
        from PIL import Image
        results = []
        for source in sorted(self.game.rglob("*")):
            if not source.is_file() or source.suffix.lower() not in (".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"):
                continue
            relative = source.relative_to(self.game)
            target = self.assets / "images" / "loose" / relative
            row = {"kind": "loose_image", "source": relative.as_posix(), "original_name": source.name,
                   "output": str(target.relative_to(self.out)), "status": "ok"}
            try:
                with Image.open(source) as image:
                    for frame in range(getattr(image, "n_frames", 1)):
                        image.seek(frame)
                        image.load()
                    row.update(width=image.width, height=image.height, frames=getattr(image, "n_frames", 1))
                save_bytes(target, source.read_bytes())
                row["sha256"] = sha256(target)
            except Exception as error:
                row.update(status="failed", error=str(error))
                self.errors.append(row)
            results.append(row)
        self.rows.extend(results)
        write_report(self.reverse / "reports" / "loose_assets.json", results)

    def organize_images(self):
        source_report = self.reverse / "reports" / "image_assets.json"
        rows = json.loads(source_report.read_text(encoding="utf-8"))
        used = set()
        moved = 0
        replacements = {}
        known_actors = {safe_relative(r["original_name"]).parts[0] for r in rows
                        if r.get("category") == "角色与敌人" or classify_image(r["original_name"])[0] == "角色与敌人"}
        for row in rows:
            category, relative = classify_image(row["original_name"], known_actors)
            label = "current" if row["source"] == "dohnadohnaCG.afa" else "variants"
            row["category"] = category
            if row["status"] == "ok":
                output_relative = Path("assets/images") / label / category / relative.with_suffix(".png")
                key = output_relative.as_posix().casefold()
                if key in used:
                    output_relative = output_relative.with_name(output_relative.stem + f"__e{row['index']:05d}.png")
                if len(str(self.out / output_relative)) > 235:
                    output_relative = Path("assets/images") / label / category / "_indexed" / f"e{row['index']:05d}_{relative.name[-75:]}.png"
                used.add(output_relative.as_posix().casefold())
                source = self.out / row["output"]
                target = self.out / output_relative
                if source != target:
                    if self.assets.resolve() not in source.resolve().parents or self.assets.resolve() not in target.resolve().parents:
                        raise ValueError("Image organization target left the assets directory")
                    if sha256(source) != row["sha256"]:
                        raise ValueError(f"Output modified after extraction; preserved: {source}")
                    target.parent.mkdir(parents=True, exist_ok=True)
                    if target.exists():
                        raise FileExistsError(f"Organization would overwrite an existing file: {target}")
                    source.rename(target)
                    moved += 1
                    old_relative = row["output"]
                    replacements[old_relative] = str(output_relative)
                    row["output"] = str(output_relative)
            if row.get("raw"):
                source = self.out / row["raw"]
                raw_relative = Path("assets/raw") / label / category / relative
                if len(str(self.out / raw_relative)) > 235:
                    raw_relative = Path("assets/raw") / label / category / "_indexed" / f"e{row['index']:05d}_{relative.name[-75:]}"
                target = self.out / raw_relative
                if source != target:
                    if self.assets.resolve() not in source.resolve().parents or self.assets.resolve() not in target.resolve().parents:
                        raise ValueError("Raw organization target left the assets directory")
                    target.parent.mkdir(parents=True, exist_ok=True)
                    if target.exists():
                        raise FileExistsError(f"Raw organization would overwrite an existing file: {target}")
                    source.rename(target)
                    row["raw"] = str(raw_relative)
        for row in rows:
            if row.get("duplicate_of") in replacements:
                row["duplicate_of"] = replacements[row["duplicate_of"]]
        for branch in (self.assets / "images" / "current", self.assets / "images" / "variants",
                       self.assets / "raw" / "current", self.assets / "raw" / "variants"):
            if branch.exists():
                directories = sorted((p for p in branch.rglob("*") if p.is_dir()), key=lambda p: len(p.parts), reverse=True)
                for directory in directories:
                    if branch.resolve() not in directory.resolve().parents:
                        raise ValueError("Empty-directory cleanup target left its extraction branch")
                    if not any(directory.iterdir()):
                        directory.rmdir()
        write_report(source_report, rows)
        categories = collections.Counter(("current" if r["source"] == "dohnadohnaCG.afa" else "variants", r["category"], r["status"]) for r in rows)
        write_report(self.reverse / "reports" / "image_classification.json",
                     {"moved_png_files": moved, "categories": {"/".join(k): v for k, v in sorted(categories.items())}})
        print("ORGANIZED", moved, "PNG images into purpose/character/action folders", flush=True)

    def organize_audio(self):
        source_report = self.reverse / "reports" / "audio_assets.json"
        rows = json.loads(source_report.read_text(encoding="utf-8"))
        music_table = self.reverse / "data" / "current" / "50_音乐模式情報.x"
        settings = self.reverse / "data" / "current" / "51_音乐設定.x"
        table_text = music_table.read_text(encoding="utf-8")
        settings_text = settings.read_text(encoding="utf-8")
        names = set()
        for match in re.finditer(r"(?m)^\t([^\t\n]+?) = \{$", table_text):
            key = match[1].strip()
            names.add(json.loads(key) if key.startswith('"') else key)
        for match in re.finditer(r'\bmusic = ("(?:[^"\\]|\\.)*")', settings_text):
            names.add(json.loads(match[1]))
        if not names:
            raise ValueError("Original music tables yielded no names")
        matched = set()
        moved = 0
        for row in rows:
            if row["source"] != "dohnadohnaSound.afa" or row["status"] != "ok":
                continue
            stem = str(Path(row["original_name"]).with_suffix(""))
            if stem not in names:
                continue
            matched.add(stem)
            row["kind"] = "music"
            relative = safe_relative(row["original_name"])
            if relative.parts[0] == "音乐" and len(relative.parts) > 1:
                relative = Path(*relative.parts[1:])
            output_relative = Path("assets/audio/music") / relative
            source = self.out / row["output"]
            target = self.out / output_relative
            if source != target:
                if self.assets.resolve() not in source.resolve().parents or self.assets.resolve() not in target.resolve().parents:
                    raise ValueError("Music organization left the assets directory")
                if sha256(source) != row["sha256"]:
                    raise ValueError(f"Audio modified since extraction: {source}")
                if target.exists():
                    raise FileExistsError(f"Refusing to overwrite existing music: {target}")
                target.parent.mkdir(parents=True, exist_ok=True)
                source.rename(target)
                row["output"] = str(output_relative)
                moved += 1
        write_report(source_report, rows)
        removed = remove_empty_subdirectories(self.assets / "audio")
        write_report(self.reverse / "reports" / "audio_classification.json",
                     {"source_tables": [str(music_table.relative_to(self.out)), str(settings.relative_to(self.out))],
                      "music_reference_names": sorted(names), "matched_music_names": sorted(matched),
                      "references_not_present_in_sound_archive": sorted(names - matched),
                      "moved_music_files": moved, "counts": dict(collections.Counter(r["kind"] for r in rows)),
                      "removed_empty_directories": [str(p.relative_to(self.out)) for p in removed]})
        print("MUSIC", len(matched), "tracks matched to original music tables;", moved, "moved", flush=True)
        print("CLEANUP", len(removed), "empty audio directories removed; no files deleted", flush=True)

    def dump_shaders(self):
        import ctypes
        source = self.game / "Data" / "Shader.slk"
        data = source.read_bytes()
        if data[:4] != b"SLK\0" or struct.unpack_from("<I", data, 4)[0] != 2:
            raise ValueError("Unexpected SLK container format")
        count = struct.unpack_from("<I", data, 12)[0]
        table_end = 16 + count * 20
        if count > 100000 or table_end > len(data):
            raise ValueError("SLK table outside source file")
        dll = ctypes.WinDLL(str(Path(os.environ["SystemRoot"]) / "System32" / "d3dcompiler_47.dll"))
        disassemble = dll.D3DDisassemble
        disassemble.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_uint, ctypes.c_char_p, ctypes.POINTER(ctypes.c_void_p)]
        disassemble.restype = ctypes.c_long
        directory = self.reverse / "data" / "shaders"
        records = []
        errors = []
        for index in range(count):
            tag, offset, compressed_size, original_size, key = struct.unpack_from("<IIIII", data, 16 + index * 20)
            if offset < table_end or offset + compressed_size > len(data):
                raise ValueError(f"SLK shader {index} outside source file")
            code = zlib.decompress(data[offset:offset + compressed_size])
            if len(code) != original_size or not code.startswith(b"DXBC"):
                raise ValueError(f"SLK shader {index} has unexpected decoded format/size")
            stem = f"s{index:04d}_id{key}"
            raw = directory / "bytecode" / (stem + ".dxbc")
            save_bytes(raw, code)
            row = {"index": index, "container_tag": tag, "key": key, "offset": offset,
                   "compressed_bytes": compressed_size, "uncompressed_bytes": original_size,
                   "sha256": hashlib.sha256(code).hexdigest(), "bytecode": str(raw.relative_to(self.out))}
            buffer = ctypes.create_string_buffer(code)
            blob = ctypes.c_void_p()
            result = disassemble(buffer, len(code), 0, None, ctypes.byref(blob))
            if result < 0 or not blob.value:
                row.update(status="disassembly_failed", hresult=hex(result & 0xFFFFFFFF))
                errors.append(row)
            else:
                vtable = ctypes.cast(blob, ctypes.POINTER(ctypes.POINTER(ctypes.c_void_p))).contents
                pointer = ctypes.WINFUNCTYPE(ctypes.c_void_p, ctypes.c_void_p)(vtable[3])(blob)
                size = ctypes.WINFUNCTYPE(ctypes.c_size_t, ctypes.c_void_p)(vtable[4])(blob)
                try:
                    text = ctypes.string_at(pointer, size).rstrip(b"\0")
                finally:
                    ctypes.WINFUNCTYPE(ctypes.c_ulong, ctypes.c_void_p)(vtable[2])(blob)
                asm = directory / "assembly" / (stem + ".asm")
                save_bytes(asm, text + b"\n")
                stage = re.search(rb"(?m)^\s*((?:vs|ps|gs|hs|ds|cs)_\d_\d)", text)
                row.update(status="ok", assembly=str(asm.relative_to(self.out)),
                           profile=stage[1].decode("ascii") if stage else "unknown")
            records.append(row)
        save_json(directory / "index.json", records)
        write_report(directory / "status.json", {"status": "DXBC_EXTRACTED_AND_DISASSEMBLED",
                     "records": count, "disassembly_ok": count - len(errors), "errors": errors,
                     "original_hlsl_source_recovered": False, "game_or_shaders_executed": False})
        print("SHADERS", count, "DXBC shaders extracted;", count - len(errors), "disassembled", flush=True)
        if errors:
            raise ValueError("Some extracted DXBC shaders failed disassembly; see shader status")

    def native_index(self):
        import pefile
        targets = [self.game / "dohnadohna.exe", self.game / "dohnadohna.exe.org",
                   self.game / "原版备份" / "dohnadohna.exe", self.game / "Mai@KF.dll",
                   self.game / "OpenSaveFolder.exe", self.game / "ResetConfig.exe"]
        seen = {}
        records = []
        for source in targets:
            digest = sha256(source)
            label = "current" if source.name == "dohnadohna.exe" and source.parent == self.game else (
                "original" if source.suffix == ".org" else "original_backup" if source.parent.name == "原版备份" else source.stem)
            record = {"label": label, "source": str(source), "sha256": digest}
            if digest in seen:
                record["duplicate_of"] = seen[digest]
            else:
                seen[digest] = label
                pe = pefile.PE(str(source))
                record.update(machine=hex(pe.FILE_HEADER.Machine), image_base=hex(pe.OPTIONAL_HEADER.ImageBase),
                              entry_rva=hex(pe.OPTIONAL_HEADER.AddressOfEntryPoint),
                              sections=[{"name": s.Name.rstrip(b"\0").decode("ascii", "replace"),
                                         "rva": hex(s.VirtualAddress), "raw_offset": s.PointerToRawData,
                                         "raw_size": s.SizeOfRawData, "entropy": s.get_entropy()} for s in pe.sections],
                              imports=[{"library": d.dll.decode("ascii", "replace"),
                                        "functions": [{"name": i.name.decode("ascii", "replace") if i.name else None,
                                                       "ordinal": i.ordinal, "address": hex(i.address)} for i in d.imports]}
                                       for d in getattr(pe, "DIRECTORY_ENTRY_IMPORT", [])],
                              exports=[{"name": s.name.decode("ascii", "replace") if s.name else None,
                                        "ordinal": s.ordinal, "rva": hex(s.address)}
                                       for s in getattr(getattr(pe, "DIRECTORY_ENTRY_EXPORT", None), "symbols", [])])
                pe.close()
                strings = re.findall(rb"[\x20-\x7e]{6,}", source.read_bytes())
                save_bytes(self.reverse / "native" / label / "strings_ascii.txt", b"\n".join(strings) + b"\n")
                save_json(self.reverse / "native" / label / "pe_metadata.json", record)
            records.append(record)
        save_json(self.reverse / "native" / "modules.json", records)
        print("NATIVE INDEX", len(records), "files", len(seen), "unique", flush=True)

    def finalize(self):
        rows = []
        for name in ("image_assets.json", "audio_assets.json", "loose_assets.json"):
            source = self.reverse / "reports" / name
            if source.exists():
                rows.extend(json.loads(source.read_text(encoding="utf-8")))
        columns = ["kind", "category", "source", "index", "original_name", "output", "raw", "status", "duplicate_of",
                   "sha256", "pixel_sha256", "width", "height", "mode", "frames", "codec", "sample_rate",
                   "channels", "samples", "duration_seconds", "error"]
        with (self.assets / "manifest.csv").open("w", encoding="utf-8-sig", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=columns)
            writer.writeheader()
            writer.writerows(rows)
        counts = collections.Counter((r["kind"], r["status"]) for r in rows)
        failed = [r for r in rows if r["status"] == "failed"]
        output_errors = []
        output_hashes_checked = 0
        for row in rows:
            if row["status"] == "ok":
                output = self.out / row["output"]
                if self.assets.resolve() not in output.resolve().parents:
                    output_errors.append({"path": row["output"], "error": "Output left the assets directory"})
                elif not output.is_file() or sha256(output) != row["sha256"]:
                    output_errors.append({"path": row["output"], "error": "Output is missing or its SHA256 changed"})
                else:
                    output_hashes_checked += 1
            elif row["status"] == "duplicate" and not (self.out / row["duplicate_of"]).is_file():
                output_errors.append({"path": row["duplicate_of"], "error": "Duplicate points to a missing canonical output"})
        raw_hashes_checked = 0
        for label, paths in (("dohnadohnaCG.afa", self.archive_defs()["cg_current"]),
                             ("001.bin+002.bin", self.archive_defs()["cg_split"])):
            with AfaArchive(paths) as archive:
                image_rows = [r for r in rows if r["kind"] == "image" and r["source"] == label]
                indices = {r["index"] for r in image_rows}
                if indices != set(range(len(archive.entries))):
                    output_errors.append({"source": label, "error": "Image manifest does not cover every source entry"})
                for row in image_rows:
                    entry = archive.entries[row["index"]]
                    expected = hashlib.sha256(archive.read(entry["offset"], entry["size"])).hexdigest()
                    raw = self.out / row["raw"]
                    if not raw.is_file() or sha256(raw) != expected:
                        output_errors.append({"path": row["raw"], "error": "Raw image does not match the original archive"})
                    else:
                        raw_hashes_checked += 1
        expected_audio = {"dohnadohnaSound.afa": 1035, "dohnadohnaVoice.afa": 22427}
        for source, expected in expected_audio.items():
            actual = sum(r["source"] == source and r["kind"] in ("music", "sound", "voice") for r in rows)
            if actual != expected:
                output_errors.append({"source": source, "error": f"Audio coverage {actual} != {expected}"})
        errors = []
        data_report = self.reverse / "reports" / "data_export.json"
        if data_report.exists():
            errors.extend(json.loads(data_report.read_text(encoding="utf-8"))["errors"])
        native = {}
        for report in sorted((self.reverse / "native").glob("*/coverage.json")):
            native[report.parent.name] = json.loads(report.read_text(encoding="utf-8"))
            if report.parent.name in ("current", "original") and native[report.parent.name]["identified_functions"] <= 2:
                native[report.parent.name]["status"] = "LIMITED: packed code sections; only entry stubs identified statically"
                native[report.parent.name]["engine_source_recovered"] = False
        inventory = json.loads((self.reverse / "source_manifest.json").read_text(encoding="utf-8"))
        changed = []
        for item in inventory["files"]:
            source = self.game / item["path"]
            if not source.exists() or sha256(source) != item["sha256"]:
                changed.append(item["path"])
        report = {"asset_counts": {f"{kind}/{status}": count for (kind, status), count in counts.items()},
                  "failed_assets": failed, "data_errors": errors, "native": native,
                  "original_source_changed": changed,
                  "output_errors": output_errors, "output_sha256_verified": output_hashes_checked,
                  "raw_images_verified_against_archives": raw_hashes_checked,
                  "high_level_ain_source": "NOT RECOVERED: AIN v14 full bytecode and metadata exported instead",
                  "shader_source": "HLSL NOT RECOVERED; see data/shaders/status.json for DXBC extraction and disassembly",
                  "game_execution": "NOT RUN", "mod_runtime_tests": "NOT RUN"}
        write_report(self.reverse / "reports" / "summary.json", report)
        print("SUMMARY", json.dumps(report, ensure_ascii=False), flush=True)
        if failed or errors or changed or output_errors:
            raise RuntimeError("Extraction/validation has failures; see reports/summary.json")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--alice", type=Path, required=True)
    parser.add_argument("--workers", type=int, default=8)
    parser.add_argument("--stage", choices=("inventory", "smoke", "scripts", "data", "shaders", "images", "audio", "loose", "organize", "organize-audio", "native-index", "finalize"), required=True)
    args = parser.parse_args()
    if args.workers < 1 or args.workers > 32:
        parser.error("workers must be in [1,32]")
    extractor = Extractor(args.game, args.out, args.alice, args.workers)
    action = {"inventory": extractor.inventory, "smoke": extractor.smoke,
              "scripts": extractor.dump_scripts, "data": extractor.dump_data, "shaders": extractor.dump_shaders,
              "images": extractor.image_assets, "audio": extractor.audio_assets,
              "loose": extractor.loose_assets, "organize": extractor.organize_images,
              "organize-audio": extractor.organize_audio, "native-index": extractor.native_index,
              "finalize": extractor.finalize}[args.stage]
    action()


if __name__ == "__main__":
    main()
