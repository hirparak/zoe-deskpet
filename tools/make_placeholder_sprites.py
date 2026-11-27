#!/usr/bin/env python3
"""Draw a simple pixel-art placeholder dog for every animation Zoe uses.

Real frames (generated from photos of Zoe) replace these: same folder layout,
see FRAMES.md. Usage:  python3 tools/make_placeholder_sprites.py [out_dir]
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

W, H, UP = 40, 30, 3  # drawn at 40x30, saved 3x -> 120x90
FUR, DARK, LINE, NOSE, WHITE = (205, 150, 90, 255), (130, 85, 45, 255), (55, 35, 20, 255), (30, 20, 15, 255), (255, 255, 255, 255)
GROUND = H - 1


def dog(body, head, legs, tail, eye_closed=False, tongue=False, extra=None):
    img = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(img)
    tx0, ty0, tx1, ty1 = tail
    d.line([(tx0, ty0), (tx1, ty1)], fill=DARK, width=2)
    for (x, y0, y1) in legs:
        d.rectangle([x, y0, x + 1, y1], fill=FUR)
        d.point((x, y1), fill=DARK); d.point((x + 1, y1), fill=DARK)
    d.ellipse(body, fill=FUR)
    hx0, hy0, hx1, hy1 = head
    d.ellipse(head, fill=FUR)
    d.rectangle([hx1 - 2, hy0 + (hy1 - hy0) // 2, hx1 + 3, hy1 - 1], fill=FUR)  # snout
    d.point((hx1 + 3, hy0 + (hy1 - hy0) // 2), fill=NOSE)
    d.polygon([(hx0 + 1, hy0 + 1), (hx0 + 4, hy0 - 1), (hx0 + 3, hy0 + 6)], fill=DARK)  # ear
    ex, ey = hx1 - 2, hy0 + 3
    if eye_closed:
        d.line([(ex - 1, ey), (ex + 1, ey)], fill=LINE)
    else:
        d.point((ex, ey), fill=NOSE)
    if tongue:
        d.point((hx1 + 1, hy1), fill=(230, 90, 110, 255))
    if extra:
        extra(d)
    # 1px outline around the silhouette
    alpha = img.getchannel("A").filter(ImageFilter.MaxFilter(3))
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.paste(Image.new("RGBA", (W, H), LINE), mask=alpha)
    out.alpha_composite(img)
    return out.resize((W * UP, H * UP), Image.NEAREST)


def standing(phase=None, bob=0, tail_up=0, head_dy=0, run=False, **kw):
    y = 13 + bob
    if phase is None:
        offs = [0, 0, 0, 0]
    else:
        a = math.sin(phase) * (3 if run else 2)
        offs = [round(a), round(-a), round(-a), round(a)]
    legs = [(10 + offs[0], y + 4, GROUND), (14 + offs[1], y + 4, GROUND),
            (23 + offs[2], y + 4, GROUND), (27 + offs[3], y + 4, GROUND)]
    return dog((8, y, 31, y + 8), (25, y - 8 + head_dy, 34, y + 1 + head_dy), legs,
               (9, y + 2, 4, y - 3 - tail_up), **kw)


def sitting(tail=0, head_dy=0, **kw):
    legs = [(24, 18, GROUND), (27, 18, GROUND)]
    body = (11, 12, 28, GROUND)
    t = (12, GROUND - 2, 5 + tail, GROUND - 4 - abs(tail))
    return dog(body, (21, 3 + head_dy, 30, 12 + head_dy), legs, t, **kw)


def lying(eye_closed=True, z=0, breathe=0):
    def zs(d):
        if z:
            for i in range(z):
                x, y = 30 + i * 3, 10 - i * 4
                d.line([(x, y), (x + 2, y), (x, y + 2), (x + 2, y + 2)], fill=(90, 120, 200, 255))
    return dog((6, 21 - breathe, 28, GROUND), (22, 18, 31, 27), [], (7, 25, 2, 27),
               eye_closed=eye_closed, extra=zs)


def frames():
    tau = 2 * math.pi
    yield "sit", [sitting(tail=t) for t in (0, 1, 0, -1)], 4, True
    yield "walk", [standing(phase=i / 6 * tau, bob=(i % 3 == 0)) for i in range(6)], 10, True
    yield "run", [standing(phase=i / 6 * tau, bob=-(i % 2), run=True, tail_up=3, tongue=True) for i in range(6)], 14, True
    yield "sniff", [standing(head_dy=5 + (i % 2)) for i in range(4)], 6, True
    yield "scratch", [sitting(head_dy=1 + (i % 2), eye_closed=True) for i in range(4)], 12, True
    yield "stretch", [standing(head_dy=d, tail_up=u) for d, u in ((0, 0), (3, 2), (6, 4), (6, 4), (6, 4), (3, 2), (0, 0))], 6, False
    yield "liedown", [sitting(), standing(head_dy=3), lying(eye_closed=False), lying()], 6, False
    yield "sleep", [lying(z=z, breathe=b) for z, b in ((0, 0), (1, 1), (2, 1), (3, 0))], 2, True

    def heart(d):
        d.point([(34, 2), (36, 2), (33, 3), (34, 3), (35, 3), (36, 3), (37, 3), (34, 4), (35, 4), (36, 4), (35, 5)], fill=(230, 60, 90, 255))
    yield "happy", [sitting(tail=t, tongue=True, extra=heart if i % 2 else None) for i, t in enumerate((3, -3, 3, -3))], 8, True
    yield "getup", [lying(eye_closed=False), sitting(), standing(head_dy=3), standing()], 6, False
    yield "dangle", [dog((14, 8, 26, 24), (16, 0, 25, 9), [(15, 22, GROUND - o), (23, 22, GROUND - 2 + o)], (19, 23, 18 + o, GROUND))
                     for o in (0, 2, 0, 2)], 4, True
    yield "jump", [standing(phase=math.pi / 2, run=True, tail_up=5)], 1, True
    yield "fall", [standing(phase=-math.pi / 2, run=True, tail_up=6, tongue=True)], 1, True


def main():
    out = Path(sys.argv[1] if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent / "sprites")
    meta = {"footOffset": 0, "animations": {}}
    for name, imgs, fps, loop in frames():
        d = out / name
        d.mkdir(parents=True, exist_ok=True)
        for old in d.glob("*.png"):
            old.unlink()
        for i, im in enumerate(imgs):
            im.save(d / f"{i:03d}.png")
        meta["animations"][name] = {"fps": fps, "loop": loop}
    (out / "sprites.json").write_text(json.dumps(meta, indent=2) + "\n")
    print(f"wrote {len(meta['animations'])} animations to {out}")


if __name__ == "__main__":
    main()
