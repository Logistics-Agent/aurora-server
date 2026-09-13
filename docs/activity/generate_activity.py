"""
Generate / Render all Activity diagrams in this directory to PNG.
"""
import sys
from pathlib import Path

# Add parent docs directory to sys.path to import diagram_utils
DOCS_DIR = Path(__file__).resolve().parent.parent
if str(DOCS_DIR) not in sys.path:
    sys.path.insert(0, str(DOCS_DIR))

from diagram_utils import render_directory

def main():
    current_dir = Path(__file__).resolve().parent
    render_directory(current_dir)

if __name__ == "__main__":
    main()
