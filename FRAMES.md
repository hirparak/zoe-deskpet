# Making Zoe's frames

The app reads `sprites/<animation>/000.png, 001.png, …` plus `sprites/sprites.json`.
Swap the placeholder folders for real ones; no rebuild needed (restart Zoe to load new frames).

## Rules every frame must follow

- **Transparent PNG** (RGBA), no background, no drop shadow baked onto a floor.
- **Facing right.** The app mirrors her for walking left.
- **Feet on the bottom edge, body centred horizontally.** The bottom-centre pixel is where she
  stands. If the art has padding under the paws, set `footOffset` (in source pixels) in `sprites.json`.
- **Same canvas size within an animation**, and roughly the same across animations, so she
  doesn't jump in size when switching. ~120×90 at `"scale": 1` looks right on a 1080p/1440p screen.
- Pixel art: keep `"pixelArt": true`. Painterly or photo-style: set it to `false`.

## Animations (required: `sit`; everything else falls back gracefully)

| folder    | what                                     | frames | fps | loop |
|-----------|------------------------------------------|--------|-----|------|
| `sit`     | sitting, small tail wag / blink          | 4      | 4   | yes  |
| `walk`    | walk cycle                               | 6–8    | 10  | yes  |
| `run`     | gallop / zoomies                         | 6      | 14  | yes  |
| `sniff`   | nose to the ground                       | 4      | 6   | yes  |
| `scratch` | sitting, back leg scratching ear         | 4      | 12  | yes  |
| `stretch` | play-bow stretch                         | 6–7    | 6   | no   |
| `liedown` | sit → lie down                           | 4      | 6   | no   |
| `sleep`   | curled up asleep, breathing (+ "z"s)     | 4      | 2   | yes  |
| `getup`   | lying → standing                         | 4      | 6   | no   |
| `happy`   | clicked: tail going, tongue out, heart   | 4      | 8   | yes  |
| `dangle`  | being carried by the mouse               | 2–4    | 4   | yes  |
| `jump`    | leaping up                               | 1–2    | 8   | yes  |
| `fall`    | falling / landing pose                   | 1–2    | 8   | yes  |

Fallbacks if a folder is missing: run→walk→sit, liedown→sleep→sit, dangle→fall→sit, the rest →sit.
So a first version only needs **sit, walk, sleep**.

## From photos to frames: suggested route

1. **Collect photos** into `photos/` (git-ignored). Most useful: a clear side-on photo
   standing, sitting, lying down/curled up, plus a couple showing her markings and face up close.
2. **Make one reference sprite** of Zoe: side view, standing, facing right, plain background.
   Iterate until it's unmistakably her (markings, ear shape, tail). This is the design every
   other frame copies.
3. **Generate each pose from that reference**, not from the photos (image-edit model, e.g. the local
   Qwen Image Edit workflow: "same dog, same style, now sitting / lying curled up / mid-stride…").
   Keeping one reference stops her drifting between frames.
4. **Pixel-art it**: downscale to ~40×30, reduce to a small palette, clean up by hand
   (Aseprite / LibreSprite / Piskel). Walk and run cycles are the fiddliest; consider drawing the
   legs by hand on top of a generated body.
5. **Background removal** if needed (`rembg`), then export frames into the folders above.

`tools/make_placeholder_sprites.py` shows the expected layout and writes `sprites.json`.

## How the current frames were made (2026-11-27)

All on a ComfyUI server reached through an SSH tunnel to local port 18188:

1. `tools/comfy_edit.py`: Qwen-Image-Edit 2511 (4-step lightning) turned two photos into the reference
   sprite `gen/keys/stand_side.png`, then every key pose (`tools/key_poses*.tsv`) from that reference.
2. `tools/gen_all.sh` + `tools/animations.tsv`: Wan 2.2 I2V 14B (lightx2v 4-step) animates each pose;
   transitions use first/last-frame so they start and end exactly on key poses. Loops whose start and end
   pose are the same repeat seamlessly. Multi-part moves are joined with `tools/assemble.py`.
3. `tools/build_sprites.py` → `tools/spriteize.py`: white background knocked out, downscaled 5× onto the
   pixel grid, every pixel snapped to one palette from the reference, loop cut, cropped with her feet at
   the bottom-centre. Writes `sprites/` and `sprites/sprites.json`.
4. `tools/preview.py`: self-contained preview page of every animation.

`gen/` and `photos/` are git-ignored.
