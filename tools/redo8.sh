#!/usr/bin/env bash
# Eighth pass: belly rubs, barking, pawing for attention.
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame, always exactly two ears, one single tail and four legs."
K=gen/keys
w(){ python3 tools/comfy_wan.py "$@" < /dev/null | tail -1; }
for s in 71 72 73; do
  w $K/stand_side.png --end $K/stand_side.png --frames 25 --seed $s --outdir gen/parts/bark$s --prompt "$PRE stands on all four legs and barks loudly three times: with each bark her mouth snaps wide open, her head jerks forward and up and her front feet bounce slightly off the ground, mouth closes between barks. $POST"
  w $K/sit_front.png --end $K/sit_front.png --frames 33 --seed $s --outdir gen/parts/paw$s --prompt "$PRE sits facing the viewer staring at the viewer, lifts one front paw up high and reaches it forward towards the viewer, pawing at the viewer twice, then puts the paw back down on the ground. Her other front paw stays on the ground, her one tail stays curled on the floor beside her. $POST"
  w $K/belly.png --end $K/belly.png --frames 33 --seed $s --outdir gen/parts/belly$s --prompt "$PRE lies on her back with her belly up, happily wriggling from side to side, her four paws kicking gently in the air, tongue out, enjoying a belly rub. $POST"
done
for s in 71 72 73 74; do
  w $K/lie_side.png --end $K/belly.png --frames 25 --seed $s --outdir gen/parts/rollover$s --prompt "$PRE lying down on her tummy rolls over sideways onto her back in one smooth motion, ending belly up with her four paws in the air. $POST"
done
echo all-done
# Chosen and assembled:
# bark71 -> gen/vid/bark ; paw72 -> gen/vid/paw ; belly71 -> gen/vid/belly
# python3 tools/assemble.py gen/vid/bellydown gen/parts/down22 gen/parts/rollover73:drop_first
