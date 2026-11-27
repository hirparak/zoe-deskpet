#!/usr/bin/env python3
"""Turn Wan video frames (white background, 640x640, feet on a common baseline) into sprite frames.

  python3 tools/spriteize.py gen/vid/walk sprites/walk --loop
  python3 tools/spriteize.py gen/vid/liedown sprites/liedown            # play once, keep every frame
  python3 tools/spriteize.py gen/vid/x sprites/x --range 10:30 --mirror

Steps: knock out the white background (flood fill from the border, so her white chest patch
survives), downscale by FACTOR onto the pixel grid, snap every pixel to one shared palette taken
from the reference sprite (no colour shimmer between frames), crisp 1-bit alpha, then either cut
the most seamless loop or keep the whole clip, and crop to a canvas whose bottom-centre is her feet.
"""
import argparse
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).parent))
from comfy_wan import SIZE, BASELINE, pad  # noqa: E402

FACTOR = 5                 # 640 / 5 = 128 px canvas
MARGIN = 2                 # rows kept below the feet baseline
REF = Path(__file__).resolve().parent.parent / "gen/keys/stand_side.png"
PALETTE_SIZE = 16


def background_mask(rgb: np.ndarray, thresh=38, leg_gaps=True) -> np.ndarray:
    """True where the pixel is background: near-white AND connected to the image border."""
    h, w, _ = rgb.shape
    near_white = (255 - rgb.astype(int)).sum(axis=2) < thresh * 3
    bg = np.zeros((h, w), bool)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if near_white[y, x] and not bg[y, x]:
                bg[y, x] = True; q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if near_white[y, x] and not bg[y, x]:
                bg[y, x] = True; q.append((y, x))
    while q:
        y, x = q.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and near_white[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True; q.append((ny, nx))
    # Background enclosed between her legs can't be reached from the border. Her white chest patch is
    # enclosed too, but always ends above ~76% of her height; leg gaps reach the bottom fifth.
    lab, n = ndimage.label(~bg)
    if n and leg_gaps:
        body = lab == (np.argmax(np.bincount(lab.ravel())[1:]) + 1)
        ys = np.nonzero(body.any(axis=1))[0]
        top, height = ys.min(), max(1, ys.max() - ys.min())
        # Only grey-white like the backdrop (not pale tan fur highlights), and big enough to be a real gap.
        grey = (rgb.max(axis=2).astype(int) - rgb.min(axis=2)) < 14
        holes, m = ndimage.label(near_white & grey & ~bg)
        sizes = np.bincount(holes.ravel())
        for i in range(1, m + 1):
            if sizes[i] < 250:
                continue
            rows = np.nonzero((holes == i).any(axis=1))[0]
            if (rows.max() - top) / height >= 0.79:
                bg[holes == i] = True
    return bg


def shrink(rgb: np.ndarray, fg: np.ndarray):
    """Downscale by FACTOR: colour = mean of foreground pixels in each cell, alpha = majority vote."""
    h, w = fg.shape
    H, W = h // FACTOR, w // FACTOR
    rgb = rgb[:H * FACTOR, :W * FACTOR].reshape(H, FACTOR, W, FACTOR, 3).astype(float)
    m = fg[:H * FACTOR, :W * FACTOR].reshape(H, FACTOR, W, FACTOR)
    cnt = m.sum(axis=(1, 3))
    col = (rgb * m[..., None]).sum(axis=(1, 3)) / np.maximum(cnt, 1)[..., None]
    alpha = cnt >= (FACTOR * FACTOR) * 0.5
    return col.astype(np.uint8), alpha


def palette_image():
    """Palette from the reference sprite, prepared exactly like a video frame."""
    tmp = Path("/tmp") if not Path("gen").exists() else Path("gen")
    ref = np.array(Image.open(pad(REF, tmp / "_palette_ref.png")).convert("RGB"))
    col, alpha = shrink(ref, ~background_mask(ref))
    pixels = col[alpha].reshape(-1, 3)
    # Her white chest patch is only a few pixels on the reference, so median-cut would drop white and the
    # patch would snap to tan. Weight a block of near-white into the sample so it keeps its own entry.
    pixels = np.concatenate([pixels, np.tile([[240, 238, 232]], (max(64, len(pixels) // 12), 1))]).astype(np.uint8)
    pal = Image.fromarray(pixels.reshape(1, -1, 3)).quantize(PALETTE_SIZE - 3, method=Image.Quantize.MEDIANCUT)
    # Tongue pink and tennis-ball green aren't on the reference sprite at all; add them as extra entries.
    entries = pal.getpalette()[:(PALETTE_SIZE - 3) * 3] + list(PINK) + list(BALL) + list(BALL_DARK)
    out = Image.new("P", (1, 1)); out.putpalette(entries + [0] * (768 - len(entries)))
    return out


PINK = (222, 105, 125)  # tongue
BALL = (196, 232, 46)   # her tennis ball
BALL_DARK = (128, 172, 24)
EXTRA = np.array([PINK, BALL, BALL_DARK])
OUTLINE = (24, 20, 22)  # darkest palette entry is close to this; used for the silhouette edge


def to_sprite(path, pal, leg_gaps=True):
    rgb = np.array(Image.open(path).convert("RGB"))
    col, alpha = shrink(rgb, ~background_mask(rgb, leg_gaps=leg_gaps))
    q = np.array(Image.fromarray(col).quantize(palette=pal, dither=Image.Dither.NONE).convert("RGB"))
    # White is only for genuinely white fur (her chest patch): pale tan highlights that snapped to white
    # go to the nearest non-white palette colour instead.
    pal_rgb = np.array(pal.getpalette()[:PALETTE_SIZE * 3]).reshape(-1, 3)
    # Pink is only for genuinely pink pixels (tongue), never shaded tan fur.
    ci = col.astype(int)
    pinkish = (ci[..., 0] > ci[..., 1] + 45) & (ci[..., 2] > ci[..., 1] - 5)
    greenish = (ci[..., 1] > ci[..., 0] + 20) & (ci[..., 1] > ci[..., 2] + 50)
    is_pink = (q == PINK).all(axis=2)
    is_ball = (q == BALL).all(axis=2) | (q == BALL_DARK).all(axis=2)
    wrong = (is_pink & ~pinkish) | (is_ball & ~greenish)
    if wrong.any():
        cand = pal_rgb[~(pal_rgb[:, None, :] == EXTRA[None]).all(axis=2).any(axis=1)]
        sel = wrong
        d = ((col[sel][:, None, :].astype(int) - cand[None, :, :]) ** 2).sum(axis=2)
        q[sel] = cand[d.argmin(axis=1)]
    whiteish = q.min(axis=2) > 215
    tinted = (col.max(axis=2).astype(int) - col.min(axis=2)) > 30
    fix = whiteish & tinted
    if fix.any():
        cand = pal_rgb[(pal_rgb.min(axis=1) <= 215) & ~(pal_rgb[:, None, :] == EXTRA[None]).all(axis=2).any(axis=1)]
        d = ((col[fix][:, None, :].astype(int) - cand[None, :, :]) ** 2).sum(axis=2)
        q[fix] = cand[d.argmin(axis=1)]
    out = np.zeros((*alpha.shape, 4), np.uint8)
    out[..., :3] = q
    out[..., 3] = np.where(alpha, 255, 0)
    # drop specks: isolated opaque pixels with < 2 opaque neighbours
    a = out[..., 3] > 0
    n = sum(np.roll(np.roll(a, dy, 0), dx, 1) for dy in (-1, 0, 1) for dx in (-1, 0, 1)) - a
    out[a & (n < 2), 3] = 0
    # Clean silhouette: the video's anti-aliased edges (black blended into white) quantize to tan/cream.
    # Pixel art has a solid dark outline there, so any light pixel touching transparency becomes outline.
    a = out[..., 3] > 0
    padded = np.pad(a, 1, constant_values=False)
    edge = a & ~(padded[:-2, 1:-1] & padded[2:, 1:-1] & padded[1:-1, :-2] & padded[1:-1, 2:])
    lum = out[..., :3].astype(int) @ np.array([299, 587, 114]) // 1000
    ball = (out[..., :3] == BALL).all(axis=2) | (out[..., :3] == BALL_DARK).all(axis=2)
    out[edge & (lum > 70), :3] = OUTLINE
    return out


def best_loop(frames, min_len, max_len, skip):
    """(start, length) whose last->first transition is smoothest."""
    def diff(a, b):
        return np.abs(a.astype(int) - b.astype(int)).mean()
    best = None
    for s in range(skip, len(frames)):
        for L in range(min_len, max_len + 1):
            if s + L >= len(frames):
                break
            d = diff(frames[s], frames[s + L])
            if best is None or d < best[0]:
                best = (d, s, L)
    if best is None:
        raise SystemExit("clip too short for the requested loop length")
    return best[1], best[2], best[0]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src"); ap.add_argument("dst")
    ap.add_argument("--loop", action="store_true", help="cut the most seamless loop")
    ap.add_argument("--min-len", type=int, default=10); ap.add_argument("--max-len", type=int, default=32)
    ap.add_argument("--skip", type=int, default=4, help="ignore the first N frames when searching for a loop")
    ap.add_argument("--range", help="explicit start:end frame range")
    ap.add_argument("--step", type=int, default=1, help="keep every Nth frame")
    ap.add_argument("--mirror", action="store_true", help="flip horizontally (source faces left)")
    ap.add_argument("--drop", help="start:end frames to cut out of the middle (e.g. a held pose)")
    ap.add_argument("--reverse", action="store_true", help="play backwards")
    ap.add_argument("--no-leg-gaps", action="store_true", help="lying/falling poses: chest sits low, keep enclosed white")
    a = ap.parse_args()

    pal = palette_image()
    paths = sorted(Path(a.src).glob("[0-9]*.png"))
    frames = [to_sprite(p, pal, leg_gaps=not a.no_leg_gaps) for p in paths]
    if a.mirror:
        frames = [f[:, ::-1] for f in frames]

    if a.range:
        s, e = (int(v) for v in a.range.split(":"))
        frames = frames[s:e]
    elif a.loop:
        s, L, d = best_loop(frames, a.min_len, a.max_len, a.skip)
        print(f"loop: frames {s}..{s + L - 1} ({L} frames), seam diff {d:.2f}")
        frames = frames[s:s + L]
    if a.drop:
        s, e = (int(v) for v in a.drop.split(":"))
        frames = frames[:s] + frames[e:]
    frames = frames[::a.step]
    if a.reverse:
        frames = frames[::-1]

    # Canvas: feet baseline at the bottom (minus MARGIN), symmetric about the centre column.
    base = BASELINE // FACTOR + MARGIN
    stack = np.stack(frames)[:, :base]
    opaque = stack[..., 3].max(axis=0) > 0
    ys, xs = np.nonzero(opaque)
    cx = stack.shape[2] // 2
    half = max(cx - xs.min(), xs.max() + 1 - cx) + 1
    top = max(0, ys.min() - 1)
    out = Path(a.dst)
    out.mkdir(parents=True, exist_ok=True)
    for f in out.glob("*.png"):
        f.unlink()
    for i, f in enumerate(stack):
        Image.fromarray(f[top:, max(0, cx - half):cx + half]).save(out / f"{i:03d}.png")
    print(f"{out}: {len(frames)} frames, {2 * half}x{base - top}px")


if __name__ == "__main__":
    main()
