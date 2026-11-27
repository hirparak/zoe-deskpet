#!/usr/bin/env bash
# Thirteenth pass: circling before bed and a tail chase that actually goes round.
# The old "spin" was one clip from standing back to standing and came out as a turntable with still legs.
# Here each half-turn is its own clip between a pose and its mirror image, so the model has to step her round.
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame, always exactly two ears, one single tail and four legs."
K=gen/keys
w(){ python3 tools/comfy_wan.py "$@" < /dev/null | tail -1; }
for s in ${SEEDS:-221 222 223}; do
  w $K/stand_side.png --end $K/sniffdown.png --frames 13 --seed $s --outdir gen/parts/ci_in$s --prompt "$PRE lowers her head down to sniff the ground in front of her paws. $POST"
  w $K/sniffdown.png --end $K/sniffdown.png --mirror-end --frames 25 --seed $s --outdir gen/parts/ci_a$s --prompt "$PRE keeps her nose down sniffing the ground and slowly walks round in a tight half circle on the spot, turning away from the viewer so that we see her back and tail, stepping one paw at a time, until she faces the other way. $POST"
  w $K/sniffdown.png --mirror-start --end $K/sniffdown.png --frames 25 --seed $s --outdir gen/parts/ci_b$s --prompt "$PRE keeps her nose down sniffing the ground and slowly walks round in a tight half circle on the spot, turning towards the viewer so that we see her face, stepping one paw at a time, until she faces the other way. $POST"
  w $K/tailchase.png --end $K/tailchase.png --mirror-end --frames 17 --seed $s --outdir gen/parts/tc_a$s --prompt "$PRE excitedly chases her own tail, whipping round fast in a tight half circle away from the viewer so that we see her back, body bent round, nose reaching for her tail, paws scampering quickly, until she faces the other way. $POST"
  w $K/tailchase.png --mirror-start --end $K/tailchase.png --frames 17 --seed $s --outdir gen/parts/tc_b$s --prompt "$PRE excitedly chases her own tail, whipping round fast in a tight half circle towards the viewer so that we see her face, body bent round, nose reaching for her tail, paws scampering quickly, until she faces the other way. $POST"
done
echo all-done
