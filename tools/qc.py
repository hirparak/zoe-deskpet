#!/usr/bin/env python3
"""Large QC sheet for checking anatomy (tails/ears/legs): tight crop on the dog, big frames.
  python3 tools/qc.py OUT.png [--step N] [--size PX] DIR [DIR ...]"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
args = sys.argv[1:]; out = args.pop(0)
def opt(name, default):
    if name in args:
        i = args.index(name); v = int(args[i + 1]); del args[i:i + 2]; return v
    return default
step = opt("--step", 2)
size = opt("--size", 170)
rows = []
for d in args:
    fs = sorted(Path(d).glob("[0-9]*.png"))[::step]
    ims = [Image.alpha_composite(Image.new("RGBA", Image.open(f).size, (110, 165, 110, 255)), Image.open(f).convert("RGBA")).convert("RGB") for f in fs]
    union = None
    for im in ims:
        a = np.abs(np.array(im).astype(int) - [110, 165, 110]).sum(axis=2) > 12 if im.getpixel((0, 0)) == (110, 165, 110) else np.array(im.convert("L")) < 225
        ys, xs = np.nonzero(a)
        b = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
        union = b if union is None else (min(union[0], b[0]), min(union[1], b[1]), max(union[2], b[2]), max(union[3], b[3]))
    rows.append((Path(d).name, [im.crop(union) for im in ims], [f.stem for f in fs]))
cols = max(len(r[1]) for r in rows)
cols = min(cols, 9)
lines = sum((len(r[1]) + cols - 1) // cols for r in rows)
sheet = Image.new("RGB", (cols * size + 90, lines * (size + 14)), (215, 228, 215)); d = ImageDraw.Draw(sheet)
y = 0
for name, ims, stems in rows:
    d.text((3, y + size // 2), name, fill="black")
    for i, (im, st) in enumerate(zip(ims, stems)):
        if i and i % cols == 0: y += size + 14
        k = min(size / im.width, size / im.height)
        t = im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.NEAREST)
        x = 90 + (i % cols) * size
        sheet.paste(t, (x, y)); d.text((x + 2, y + size), st, fill="black")
    y += size + 14
sheet.save(out)
