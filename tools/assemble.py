#!/usr/bin/env python3
"""Join clip parts into one gen/vid/<name>/ clip.

  python3 tools/assemble.py OUT PART[:hold=N][:drop_first][:drop_last][:range=A-B][:reverse] ...
hold=N repeats the part's last frame N extra times; drop_first/drop_last avoid doubled frames at joins;
range=A-B keeps frames A..B-1 of the part; reverse plays the (ranged) part backwards.
"""
import shutil, sys
from pathlib import Path
out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
for f in out.glob("[0-9]*.png"): f.unlink()
i = 0
for spec in sys.argv[2:]:
    path, *opts = spec.split(":")
    fs = sorted(Path(path).glob("[0-9]*.png"))
    rng = next((o[6:] for o in opts if o.startswith("range=")), None)
    if rng: a, b = (int(v) for v in rng.split("-")); fs = fs[a:b]
    if "reverse" in opts: fs = fs[::-1]
    if "drop_first" in opts: fs = fs[1:]
    if "drop_last" in opts: fs = fs[:-1]
    hold = next((int(o[5:]) for o in opts if o.startswith("hold=")), 0)
    for f in fs + [fs[-1]] * hold:
        shutil.copy(f, out / f"{i:03d}.png"); i += 1
print(out, i, "frames")
