#!/usr/bin/env bash
# Twelfth pass: scratch, front view. Side-on the editor always kept the folded hind leg AND added a raised one;
# facing the viewer the hind leg visibly lifts from her hip while both front legs stay planted.
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame, always exactly two ears, one single tail and four legs."
K=gen/keys
w(){ python3 tools/comfy_wan.py "$@" < /dev/null | tail -1; }
for s in 181 182 183; do
  w $K/sit_front.png --end $K/scratch.png --frames 13 --seed $s --outdir gen/parts/scrf_in$s --prompt "$PRE sitting facing the viewer leans to one side and lifts one hind leg up from the floor beside her body to her ear, tilting her head towards it. Both front legs stay still on the ground in the middle. $POST"
  w $K/scratch.png --end $K/scratch.png --frames 25 --seed $s --outdir gen/parts/scrf$s --prompt "$PRE sits facing the viewer scratching behind her ear with one raised hind leg: only that hind paw moves, kicking rapidly up and down against her ear, her head stays tilted with eyes shut. Both front legs stay completely still on the ground. $POST"
  w $K/scratch.png --end $K/sit_front.png --frames 13 --seed $s --outdir gen/parts/scrf_out$s --prompt "$PRE stops scratching, lowers her hind leg back down to the floor beside her and straightens her head, sitting normally facing the viewer. Both front legs stay still. $POST"
done
echo all-done
# Key pose: scratchf_148 (fast model, correct legs) refined by the full 30-step model (scratchr_174: ear fixed, no puff).
# python3 tools/assemble.py gen/vid/scratch gen/parts/scrf_in182:range=0-10 gen/parts/scrf183:drop_first gen/parts/scrf183:drop_first gen/parts/scrf_out182:range=1-9
