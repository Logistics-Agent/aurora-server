"""
Master script to render all PlantUML diagrams across all documentation subdirectories:
- docs/erd/
- docs/sequence/
- docs/class/
- docs/use_case/
- docs/activity/
- docs/architecture/
"""

import sys
from pathlib import Path
from diagram_utils import render_directory

DOCS_DIR = Path(__file__).resolve().parent

SUBDIRS = [
    "erd",
    "sequence",
    "class",
    "use_case",
    "activity",
    "architecture",
]

def main():
    print("==================================================")
    print(" Aurora Platform - Diagram Renderer Pipeline")
    print("==================================================\n")
    
    total_rendered = 0
    for subdir_name in SUBDIRS:
        subdir_path = DOCS_DIR / subdir_name
        if subdir_path.exists() and subdir_path.is_dir():
            count = render_directory(subdir_path)
            total_rendered += count
            
    print("==================================================")
    print(f" Summary: {total_rendered} diagram image(s) processed.")
    print("==================================================")

if __name__ == "__main__":
    main()
