#!/usr/bin/env bash
# Ninth pass (review of the original set): snappier bark, a happy with a real wag, a sleep that just breathes.
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame, always exactly two ears, one single tail and four legs."
K=gen/keys
w(){ python3 tools/comfy_wan.py "$@" < /dev/null | tail -1; }
for s in 91 92 93; do
  w $K/stand_side.png --end $K/stand_side.png --frames 17 --seed $s --outdir gen/parts/bark$s --prompt "$PRE stands with all four feet planted firmly on the ground and gives two short sharp barks: her mouth snaps open and shut quickly twice and her head bobs up with each bark. Her legs do not move and no paw leaves the ground. $POST"
  w $K/sit_front.png --end $K/sit_front.png --frames 33 --seed $s --outdir gen/parts/happy$s --prompt "$PRE sits facing the viewer, very happy and excited: her tail sweeps fast and wide from side to side across the floor behind her the whole time, mouth open panting with her pink tongue out, bouncing slightly. Her one tail stays on the floor. $POST"
  w $K/sleep_flat.png --end $K/sleep_flat.png --frames 49 --seed $s --outdir gen/parts/zzz$s --prompt "$PRE is fast asleep lying down with her chin resting on her front paws and her eyes closed. She stays completely still, only her back gently rises and falls with slow breathing. Her head stays down on her paws and her eyes stay closed the whole time. $POST"
done
echo all-done
# Chosen: bark92 -> gen/vid/bark, happy91 -> gen/vid/happy, zzz93 -> gen/vid/sleep
