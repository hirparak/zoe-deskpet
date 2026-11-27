#!/usr/bin/env bash
# Tenth pass: small idles. Scratch (from a proper one-hind-leg key pose), tail chase, dig.
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame, always exactly two ears, one single tail and four legs."
K=gen/keys
w(){ python3 tools/comfy_wan.py "$@" < /dev/null | tail -1; }
for s in 111 112 113; do
  w $K/sit_side.png --end $K/scratch.png --frames 17 --seed $s --outdir gen/parts/scr_in$s --prompt "$PRE sitting down lifts one hind leg up to her neck and tilts her head down towards it, ready to scratch. Both front legs stay still on the ground. $POST"
  w $K/scratch.png --end $K/scratch.png --frames 25 --seed $s --outdir gen/parts/scr$s --prompt "$PRE sits and scratches her neck fast with one raised hind leg: only that one hind paw moves, kicking rapidly back and forth against her neck, her head stays tilted with eyes shut. Both front legs stay completely still on the ground. $POST"
  w $K/scratch.png --end $K/sit_side.png --frames 17 --seed $s --outdir gen/parts/scr_out$s --prompt "$PRE stops scratching, lowers her hind leg back down to the ground and lifts her head, sitting normally again. $POST"
  w $K/stand_side.png --end $K/stand_side.png --frames 25 --seed $s --outdir gen/parts/dig$s --prompt "$PRE lowers her head and digs fast at the ground in front of her with both front paws, paws scrabbling one after the other, bottom up, tail wagging, then stands back up. $POST"
done
for s in 111 112 113 114; do
  w $K/stand_side.png --end $K/stand_side.png --frames 33 --seed $s --outdir gen/parts/spin$s --prompt "$PRE playfully chases her own tail, spinning round in one full tight circle on the spot, stepping round with all four paws, then ends facing right again. $POST"
done
echo all-done
# Chosen and assembled (frame 15 of several 17-frame clips and ~23 of 25-frame clips come out brown-tinted: avoid):
# python3 tools/assemble.py gen/vid/scratch gen/parts/scr_in113 gen/parts/scr113:drop_first gen/parts/scr113:drop_first gen/parts/scr_out113:range=1-11
# python3 tools/assemble.py gen/vid/spin gen/parts/spin114:range=0-24 gen/parts/spin114:range=0-26
# dig112 -> gen/vid/dig (frames 0-18 used)
