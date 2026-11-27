#!/usr/bin/env bash
# Seventh pass: personality. Tail wag, hind-legs greeting, play bow (zoomies), ball carry and pick-up.
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame, always exactly two ears, one single tail and four legs."
K=gen/keys
w(){ python3 tools/comfy_wan.py "$@" < /dev/null | tail -1; }
for s in 61 62 63; do
  w $K/stand_side.png --end $K/stand_side.png --frames 33 --seed $s --outdir gen/parts/wag$s --prompt "$PRE stands still on all four legs, happily wagging her tail fast from side to side the whole time, mouth open panting, her feet stay planted on the ground. $POST"
  w $K/stand_front.png --end $K/beg_front.png --frames 21 --seed $s --outdir gen/parts/begup$s --prompt "$PRE facing the viewer rears up to stand upright on her two hind legs, lifting both front paws up in front of her chest. $POST"
  w $K/beg_front.png --end $K/beg_front.png --frames 33 --seed $s --outdir gen/parts/beg$s --prompt "$PRE stands upright on her two hind legs facing the viewer, excitedly pawing the air with both front paws, bouncing a little, tail wagging from side to side, happy open mouth. $POST"
  w $K/beg_front.png --end $K/stand_front.png --frames 21 --seed $s --outdir gen/parts/begdown$s --prompt "$PRE drops from standing on her hind legs back down onto all four paws, facing the viewer. $POST"
  w $K/stand_side.png --end $K/playbow.png --frames 17 --seed $s --outdir gen/parts/bowin$s --prompt "$PRE quickly drops her chest and front legs down to the ground into a play bow with her bottom up in the air. $POST"
  w $K/playbow.png --end $K/playbow.png --frames 25 --seed $s --outdir gen/parts/bow$s --prompt "$PRE holds a playful play bow, chest down on the ground and bottom up in the air, wagging her tail fast from side to side and wiggling her bottom excitedly. $POST"
  w $K/playbow.png --end $K/stand_side.png --frames 17 --seed $s --outdir gen/parts/bowout$s --prompt "$PRE springs up from a play bow to standing on all four legs. $POST"
  w $K/ball_mouth.png --end $K/ball_mouth.png --frames 33 --seed $s --outdir gen/parts/carry$s --prompt "$PRE walks forward proudly on the spot, a steady trotting walk cycle with all four legs stepping, carrying a yellow-green tennis ball in her mouth the whole time, tail wagging. Side view, the dog walks in place like on a treadmill. $POST"
  w $K/sniffdown.png --end $K/ball_mouth.png --frames 21 --seed $s --outdir gen/parts/pickup$s --prompt "$PRE with her nose down at the ground grabs a yellow-green tennis ball in her mouth and lifts her head up, holding the ball in her mouth. $POST"
done
echo all-done
# Chosen and assembled:
# cp -r gen/parts/wag61/. gen/vid/wag/ ; carry61 -> gen/vid/carry ; pickup62 -> gen/vid/pickup
# python3 tools/assemble.py gen/vid/greet gen/vid/turn:range=0-14 gen/parts/begup61:drop_first gen/parts/beg61:drop_first gen/parts/beg61:drop_first gen/parts/begdown62:range=1-12 gen/vid/turn:range=0-14:reverse:drop_first
# python3 tools/assemble.py gen/vid/playbow gen/parts/bowin63 gen/parts/bow61:drop_first gen/parts/bowout61:drop_first
