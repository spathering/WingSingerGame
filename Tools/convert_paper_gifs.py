"""Convert the two supplied paper GIFs without changing the character atlases."""
from pathlib import Path
import sys
from convert_character_gifs import convert_files

source = Path(sys.argv[1])
convert_files([source / "craneFlat (1).gif", source / "star.gif"],
              Path("Assets/Art/Paper"), "PaperManifest.json")
