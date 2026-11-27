#!/usr/bin/env bash
# Fourth pass: front sits from the single-tailed pose; sleeping becomes head-on-paws (no curl/roll morphs).
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame, always exactly two ears, one single tail and four legs."
w(){ python3 tools/comfy_wan.py "$@" < /dev/null | tail -1; }
for s in 21 22 23; do
  w gen/keys/sit_front.png --end gen/keys/sit_front.png --frames 49 --seed $s --outdir gen/parts/look$s --prompt "$PRE sits facing the viewer and looks at the viewer, slowly tilts her head to one side and back, blinks, ears twitch. Her one tail stays curled on the floor beside her. $POST"
  w gen/keys/sit_front.png --end gen/keys/sit_front.png --frames 33 --seed $s --outdir gen/parts/happyf$s --prompt "$PRE sits facing the viewer, very happy, mouth open panting with tongue out, her one tail sweeping side to side on the floor beside her. $POST"
  w gen/keys/stand_side.png --end gen/keys/lie_side.png --frames 21 --seed $s --outdir gen/parts/down$s --prompt "$PRE lies down: front legs bend and her chest lowers to the floor, back legs fold underneath, and she settles on her belly with her head up. Her one tail stays attached and lowers to the floor. $POST"
  w gen/keys/sleep_flat.png --end gen/keys/stand_side.png --frames 25 --seed $s --outdir gen/parts/up$s --prompt "$PRE wakes up, lifts her head off her paws, pushes up on her front legs and stands up, then looks ahead. $POST"
done
for s in 21 22; do
  w gen/keys/lie_side.png --end gen/keys/sleep_flat.png --frames 17 --seed $s --outdir gen/parts/doze$s --prompt "$PRE lying on her belly, gets sleepy: slowly lowers her head to rest her chin on her front paws and closes her eyes. Only her head moves. $POST"
  w gen/keys/sleep_flat.png --end gen/keys/sleep_flat.png --frames 49 --seed $s --outdir gen/parts/zzz$s --prompt "$PRE is fast asleep with her chin resting on her front paws, eyes closed, slow deep breathing so her body gently rises and falls, an ear twitches once. Otherwise completely still. $POST"
done
echo all-done
