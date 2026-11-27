#!/usr/bin/env bash
# Second pass on turn, lie-down and scratch (feedback: statue spin, extra legs, two legs scratching).
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame, always exactly four legs."
w(){ python3 tools/comfy_wan.py "$@" < /dev/null | tail -1; }
for s in 1 2; do
  w gen/keys/stand_side.png --end gen/keys/stand_front.png --frames 21 --seed $s --outdir gen/parts/turn_a$s --prompt "$PRE turns on the spot to face the viewer, walking her paws around in small steps, front paws step sideways and back paws shuffle round, body pivoting naturally like a real dog turning. $POST"
  w gen/keys/stand_front.png --end gen/keys/stand_side.png --mirror-end --frames 21 --seed $s --outdir gen/parts/turn_b$s --prompt "$PRE keeps turning round to face left, walking her paws around in small steps, front paws step sideways and back paws shuffle round, body pivoting naturally like a real dog turning. $POST"
  w gen/keys/stand_side.png --end gen/keys/lie_side.png --frames 21 --seed $s --outdir gen/parts/lie_a$s --prompt "$PRE lies down: she bends her front legs and lowers her chest to the floor, then her back legs fold under her and she settles on her belly with her head up. $POST"
  w gen/keys/lie_side.png --end gen/keys/sleep_curl.png --frames 25 --seed $s --outdir gen/parts/lie_b$s --prompt "$PRE lying on her belly, rolls onto her side and curls up into a ball, tucking her nose against her tail, and closes her eyes. $POST"
done
for s in 3 4 5; do
  w gen/keys/sit_side.png --end gen/keys/sit_side.png --frames 49 --seed $s --outdir gen/parts/scratch$s --prompt "$PRE sitting, scratches an itch behind her ear with her back leg like a real dog: one hind leg lifts up and kicks rapidly against her ear while both front legs stay straight and planted on the floor and do not move, head tilted towards the hind paw, then the hind leg goes back down. $POST"
done
echo all-done
