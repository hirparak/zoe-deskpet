#!/usr/bin/env bash
# Third pass: rest (doubled ears) and happy (two tails). Start == end pose so they loop seamlessly.
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame, always exactly two ears, one tail and four legs."
w(){ python3 tools/comfy_wan.py "$@" < /dev/null | tail -1; }
for s in 11 12 13; do
  w gen/keys/lie_side.png --end gen/keys/lie_side.png --frames 49 --seed $s --outdir gen/parts/rest$s --prompt "$PRE lies on her belly resting with her head up, calmly breathing, blinks slowly, one ear twitches once, very small gentle movements only. $POST"
  w gen/keys/sit_front.png --end gen/keys/sit_front.png --frames 33 --seed $s --outdir gen/parts/happy$s --prompt "$PRE sits facing the viewer, very happy, mouth open panting with tongue out, her single tail wagging fast behind her, little happy wiggle of the shoulders. $POST"
done
echo all-done
