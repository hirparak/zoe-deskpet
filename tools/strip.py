#!/usr/bin/env python3
"""Contact strip of frames: python3 tools/strip.py DIR OUT [step] [thumb]"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw
d, out = Path(sys.argv[1]), sys.argv[2]
step = int(sys.argv[3]) if len(sys.argv) > 3 else 4
T = int(sys.argv[4]) if len(sys.argv) > 4 else 200
fs = sorted(d.glob("[0-9]*.png"))[::step]
cols = 8; rows = (len(fs) + cols - 1) // cols
s = Image.new("RGB", (cols * T, rows * (T + 12)), (200, 220, 200)); dr = ImageDraw.Draw(s)
for i, f in enumerate(fs):
    im = Image.open(f).convert("RGBA"); im.thumbnail((T, T), Image.NEAREST)
    x, y = (i % cols) * T, (i // cols) * (T + 12)
    s.paste(im, (x, y), im); dr.text((x + 2, y + T), f.stem, fill="black")
s.save(out)
