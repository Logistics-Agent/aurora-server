"""
Diagram rendering utilities for Aurora Server documentation.
Supports:
1. Local rendering via PlantUML JAR and Java (high-res, offline).
2. Fallback rendering via official PlantUML web service (no local Java/JAR required).
"""

import os
import sys
import zlib
import shutil
import subprocess
import urllib.request
from pathlib import Path

DOCS_ROOT = Path(__file__).resolve().parent
JAR_PATHS = [
    DOCS_ROOT / "tools" / "plantuml.jar",
    DOCS_ROOT / "report" / "diagrams" / "tools" / "plantuml.jar",
]

# PlantUML custom base64 charset
PLANTUML_ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_"

def _encode_6bit(b):
    return PLANTUML_ALPHABET[b & 0x3F]

def _encode_plantuml(text: str) -> str:
    """Encodes PlantUML text into the URL-safe format used by plantuml.com."""
    zlib_obj = zlib.compressobj(9, zlib.DEFLATED, -zlib.MAX_WBITS)
    compressed = zlib_obj.compress(text.encode('utf-8')) + zlib_obj.flush()
    
    result = []
    i = 0
    length = len(compressed)
    while i < length:
        b1 = compressed[i]
        b2 = compressed[i + 1] if i + 1 < length else 0
        b3 = compressed[i + 2] if i + 2 < length else 0
        
        c1 = b1 >> 2
        c2 = ((b1 & 0x3) << 4) | (b2 >> 4)
        c3 = ((b2 & 0xF) << 2) | (b3 >> 6)
        c4 = b3 & 0x3F
        
        result.append(_encode_6bit(c1))
        result.append(_encode_6bit(c2))
        if i + 1 < length:
            result.append(_encode_6bit(c3))
        if i + 2 < length:
            result.append(_encode_6bit(c4))
        i += 3
    return "".join(result)

def find_plantuml_jar() -> Path | None:
    for path in JAR_PATHS:
        if path.exists():
            return path
    return None

def has_java() -> bool:
    try:
        res = subprocess.run(["java", "-version"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        return res.returncode == 0
    except Exception:
        return False

def render_puml_file(puml_path: Path, output_png_path: Path | None = None) -> bool:
    """Renders a single .puml file to PNG."""
    puml_path = Path(puml_path).resolve()
    if not puml_path.exists():
        print(f"[-] File not found: {puml_path}")
        return False

    if output_png_path is None:
        output_png_path = puml_path.with_suffix(".png")
    else:
        output_png_path = Path(output_png_path).resolve()

    jar_path = find_plantuml_jar()
    if jar_path and has_java():
        # Use local JAR
        try:
            cmd = [
                "java",
                "-jar",
                str(jar_path),
                "-tpng",
                "-output",
                str(output_png_path.parent),
                str(puml_path)
            ]
            res = subprocess.run(cmd, capture_output=True, text=True)
            if res.returncode == 0 and output_png_path.exists():
                print(f"[+] Rendered locally: {puml_path.name} -> {output_png_path.name}")
                return True
            else:
                print(f"[!] Local jar failed ({res.stderr.strip()}), trying remote server...")
        except Exception as e:
            print(f"[!] Local render exception: {e}, trying remote server...")

    # Fallback: Render via PlantUML HTTP service
    try:
        content = puml_path.read_text(encoding="utf-8")
        encoded = _encode_plantuml(content)
        url = f"http://www.plantuml.com/plantuml/png/~1{encoded}"
        req = urllib.request.Request(url, headers={"User-Agent": "Aurora-Docs-Renderer/1.0"})
        with urllib.request.urlopen(req, timeout=15) as resp:
            data = resp.read()
            output_png_path.write_bytes(data)
            print(f"[+] Rendered via server: {puml_path.name} -> {output_png_path.name}")
            return True
    except Exception as e:
        print(f"[-] Failed to render {puml_path.name}: {e}")
        return False

def render_directory(directory: Path) -> int:
    """Renders all .puml files in a directory."""
    directory = Path(directory).resolve()
    puml_files = sorted(directory.glob("*.puml"))
    if not puml_files:
        print(f"[*] No .puml files found in {directory}")
        return 0

    print(f"[*] Rendering {len(puml_files)} diagram(s) in {directory.name}...")
    success_count = 0
    for file in puml_files:
        if render_puml_file(file):
            success_count += 1
    print(f"[+] Completed: {success_count}/{len(puml_files)} rendered successfully.\n")
    return success_count
