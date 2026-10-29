"""Rebuild the isolated MPL-2.0 v2.5.1 model metadata. No runtime network use.

Only structural declarations are translated; no upstream parser, prose or runtime
code is bundled. The pinned source, digests and license accompany the resource.
"""
import hashlib
import json
import re
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / "artifacts/stage17/upstream"
CACHE.mkdir(parents=True, exist_ok=True)
REPO = "nHapiNET/nHapi"
REVISION = "9027797b1fd1faebcffd743ac11324499145f29d"
(CACHE / "revision.txt").write_text(REVISION, encoding="utf-8")
BASE = f"https://raw.githubusercontent.com/{REPO}/{REVISION}/"
sources = {}

def fetch(path):
    target = CACHE / path
    if not target.exists():
        target.parent.mkdir(parents=True, exist_ok=True)
        try:
            target.write_bytes(urllib.request.urlopen(BASE + path, timeout=30).read())
        except Exception as error:
            raise RuntimeError("Cannot retrieve " + path) from error
    data = target.read_bytes()
    sources[path] = hashlib.sha256(data).hexdigest()
    return data.decode("utf-8-sig")

segments, datatypes = {}, {}
PREFIX = "src/NHapi.Model.V251/"

def datatype(name):
    if name in datatypes or name in {"Varies", "ST", "TX", "FT", "ID", "IS", "NM", "SI", "DT", "DTM", "TM", "GTS", "TN"}:
        return
    datatypes[name] = []
    text = fetch(PREFIX + "Datatype/" + name + ".cs")
    components = re.findall(r'data\[(\d+)\]\s*=\s*new\s+(\w+)\(message(?:,\s*(\d+))?', text)
    for index, kind, table in components:
        kind = {"TSComponentOne": "DTM", "TSComponentTwo": "ID"}.get(kind, kind)
        datatypes[name].append({"position": int(index) + 1, "dataType": kind, "table": table or None})
    for component in datatypes[name]:
        datatype(component["dataType"])

def segment(name):
    if name in segments:
        return
    text = fetch(PREFIX + "Segment/" + name + ".cs")
    fields = re.findall(r'this\.add\(typeof\((\w+)\),\s*(true|false),\s*(\d+),\s*(\d+),\s*new System.Object\[\]\{message(?:,\s*(\d+))?\}', text)
    if not fields:
        raise ValueError("Unrecognized segment declarations: " + name)
    segments[name] = [{"position": i + 1, "dataType": kind, "min": int(required == "true"), "max": int(count) or None,
                       "maxLength": None if int(length) in (0, 99999) else int(length), "table": table or None}
                      for i, (kind, required, count, length, table) in enumerate(fields)]
    for field in segments[name]:
        datatype(field["dataType"])

def structure(name, folder="Message"):
    text = fetch(PREFIX + folder + "/" + name + ".cs")
    entries = re.findall(r'this\.add\(typeof\((\w+)\),\s*(true|false),\s*(true|false)\)', text)
    if not entries:
        raise ValueError("Unrecognized group declarations: " + name)
    result, counts = [], {}
    for child, required, repeats in entries:
        counts[child] = counts.get(child, 0) + 1
        element = {"key": child + ("_" + str(counts[child]) if counts[child] > 1 else ""), "min": int(required == "true"), "max": None if repeats == "true" else 1}
        if len(child) == 3:
            element["segment"] = child
            segment(child)
        else:
            element["children"] = structure(child, "Group")
        result.append(element)
    # The generated model expands the standard's ORM choice into six required
    # declarations. Preserve the actual choice (HL7 v2.5.1 section 4.4.1).
    if name == "ORM_O01_ORDER_DETAIL":
        result = [{"key": "DETAIL_TYPE", "min": 1, "max": 1, "choice": True, "children": result[:6]}] + result[6:]
    return result

structures = {name: structure(name) for name in ["ADT_A01", "ADT_A02", "ADT_A03", "ORM_O01", "ORU_R01"]}
resource = {"source": f"https://github.com/{REPO}/tree/{REVISION}", "license": "MPL-2.0", "structures": structures,
            "segments": segments, "datatypes": datatypes, "sourceSha256": sources}
destination = ROOT / "src/HL7Workbench.Core/ValidationCatalog/v251-model.json"
destination.parent.mkdir(parents=True, exist_ok=True)
destination.write_text(json.dumps(resource, indent=2, sort_keys=True) + "\n", encoding="utf-8")
(ROOT / "build/third-party/nHapi-MPL-2.0-LICENSE.txt").write_text(fetch("LICENSE"), encoding="utf-8")
print(f"Pinned {REVISION}: {len(structures)} structures, {len(segments)} segments, {len(datatypes)} datatypes")
