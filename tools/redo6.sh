#!/usr/bin/env bash
# Sixth pass: sit~look without the ears fanning out during the head tilt.
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame, always exactly two ears, one single tail and four legs."
w(){ python3 tools/comfy_wan.py "$@" < /dev/null | tail -1; }
for s in 41 42 43; do
  w gen/keys/sit_front.png --end gen/keys/sit_front.png --frames 49 --seed $s --outdir gen/parts/look$s --prompt "$PRE sits facing the viewer and looks at the viewer, gently tilts her head a little to one side and back, blinks. Her ears keep the same shape and size and stay close to her head the whole time, they do not spread out or grow. Her one tail stays curled on the floor beside her. $POST"
done
echo all-done
# Chosen: look41, frames 0-9 + 25-48 (skips the ear lift at 10-24; 9->25 joins as smoothly as a normal step).
# fs=(gen/parts/look41/[0-9]*.png); i=0; for n in $(seq 0 9) $(seq 25 48); do cp "${fs[$n]}" gen/vid/look/$(printf %03d $i).png; i=$((i+1)); done
