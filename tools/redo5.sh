#!/usr/bin/env bash
# Fifth pass: gallop (blobby frames) and a yawn to replace the shake (extra limb).
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame, always exactly two ears, one single tail and four legs."
w(){ python3 tools/comfy_wan.py "$@" < /dev/null | tail -1; }
for s in 31 32 33; do
  w gen/keys/run_side.png --end gen/keys/run_side.png --frames 33 --seed $s --outdir gen/parts/run$s --prompt "$PRE runs fast to the right in a smooth bounding gallop, running in place like on a treadmill: front legs reach forward together, back legs push off together, body stretching long then gathering, ears flapping back, a clear readable gallop cycle that repeats. $POST"
  w gen/keys/sit_side.png --end gen/keys/sit_side.png --frames 41 --seed $s --outdir gen/parts/yawn$s --prompt "$PRE sitting, does a big sleepy yawn: tilts her head up a little, opens her mouth very wide showing her tongue, eyes squeeze shut, then closes her mouth, smacks her lips and blinks. Her body and legs stay still. $POST"
done
echo all-done
