#!/usr/bin/env bash
# Eleventh pass: the sploot (lying awake, hind legs out behind), and another go at a cleaner gallop.
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame, always exactly two ears, one single tail and four legs."
K=gen/keys
w(){ python3 tools/comfy_wan.py "$@" < /dev/null | tail -1; }
for s in 131 132 133; do
  w $K/stand_side.png --end $K/sploot.png --frames 21 --seed $s --outdir gen/parts/splootin$s --prompt "$PRE lowers herself down onto her tummy and stretches her hind legs straight out behind her, lying flat on the floor in a sploot with her head up. $POST"
  w $K/sploot.png --end $K/sploot.png --frames 49 --seed $s --outdir gen/parts/sploot$s --prompt "$PRE lies flat on her tummy with her hind legs stretched out behind her, relaxed and awake: she slowly looks around, her ears twitch, she blinks, and her tail thumps gently on the floor. Her body and legs stay flat on the floor. $POST"
  w $K/sploot.png --end $K/stand_side.png --frames 21 --seed $s --outdir gen/parts/splootout$s --prompt "$PRE lying flat on her tummy pulls her hind legs under her, pushes up and stands up on all four legs. $POST"
done
for s in 131 132 133 134; do
  w $K/run_side.png --end $K/run_side.png --frames 33 --seed $s --outdir gen/parts/run$s --prompt "$PRE gallops fast to the right, running in place like on a treadmill, a clean simple two-beat gallop: first both front legs reach far forward while both hind legs stretch far back, then all four legs tuck together under her body, repeating. Each of her four legs stays clearly separate and readable in every frame, ears and tail streaming back. $POST"
done
echo all-done
# Chosen: splootin131 -> gen/vid/restdown, sploot132 -> gen/vid/rest, splootout133 -> gen/vid/restup, run131 -> gen/vid/run
