#!/usr/bin/env python3
"""Turn every generated clip in gen/vid/ into sprites/<anim>/ frames + sprites/sprites.json.

  python3 tools/build_sprites.py [only_this_anim ...]
"""
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
# clip -> (sprite folder, fps, loop, extra spriteize args)
PLAN = {
    "walk":        ("walk",        16, True,  ["--min-len", "8",  "--max-len", "24"]),
    "run":         ("run",         16, True,  ["--range", "0:999"]),     # start == end pose: whole clip loops
    "sit":         ("sit",         16, True,  ["--min-len", "24", "--max-len", "64"]),
    "look":        ("sit~look",    16, True,  ["--range", "0:999"]),
    # "rest" (lying awake) was a sit variant, so she snapped from sitting to lying with no transition: removed.
    "happy":       ("happy",       16, True,  ["--range", "0:999"]),
    "sniff":       ("sniff",       16, False, []),   # head down, snuffle, head up: plays once
    "yawn":        ("yawn",        16, False, []),   # sitting yawn (replaced scratch, then shake)
    "sleep":       ("sleep",       16, True,  ["--no-leg-gaps", "--range", "0:999"]),     # chin on paws, breathing
    "dangle":      ("dangle",      16, True,  ["--min-len", "8",  "--max-len", "32"]),
    "fall":        ("fall",        16, True,  ["--no-leg-gaps", "--range", "0:26"]),    # later frames have a tan blob on her ear
    "liedown":     ("liedown",     16, False, ["--no-leg-gaps"]),
    "getup":       ("getup",       16, False, ["--no-leg-gaps", "--range", "0:16"]),   # no standing about afterwards
    "stretch":     ("stretch",     16, False, ["--range", "0:48"]),
    "turn":        ("turn",        20, False, ["--drop", "13:23"]),   # cut the held front-facing pose mid-turn
    "jump":        ("jump",        16, False, ["--range", "20:25"]),   # the leap itself, shown while rising
    "jump@prep":   ("jumpprep",    24, False, ["--range", "6:20"]),    # crouch on the ground before take-off
    "wag":         ("wag",         16, True,  ["--range", "0:999"]),   # standing, tail going side to side
    "greet":       ("greet",       16, False, []),   # turn to face you, up on hind legs, paws going, back down
    "playbow":     ("playbow",     16, False, ["--no-leg-gaps"]),      # bow, wiggle, spring up: start of zoomies
    "carry":       ("carry",       16, True,  ["--range", "0:999"]),   # trotting with her ball
    "pickup":      ("pickup",      16, False, []),
    "pickup@drop": ("drop",        16, False, ["--reverse"]),
    "bark":        ("bark",        16, True,  ["--range", "0:15"]),   # at horses and dogs on the screen
    "paw":         ("paw",         16, False, ["--range", "0:22"]),    # "throw it!": a paw on your knee
    "bellydown":   ("bellydown",   20, False, ["--no-leg-gaps", "--drop", "15:24"]),      # lie down and roll onto her back
    "bellydown@up": ("bellyup",    20, False, ["--no-leg-gaps", "--drop", "15:24", "--reverse"]),
    "belly":       ("belly",       16, True,  ["--no-leg-gaps", "--range", "0:999"]),   # belly rub
    # "scratch" is retired: legs were finally right (front view, key pose gen/keys/scratch.png, tools/redo12.sh) but
    # the motion read as waving a leg in the air rather than scratching. The app skips it when the folder is absent.
    # "scratch":   ("scratch",     20, False, ["--holes-left", "0.45"]),
    "dig":         ("dig",         16, False, ["--range", "0:19"]),
    "spin":        ("spin",        18, False, []),                     # chases her tail: two full circles
    "spin@slow":   ("circle",      10, False, []),                     # the same two circles, slowly: turning round before bed
    "restdown":    ("restdown",    16, False, ["--no-leg-gaps", "--range", "0:18"]),   # down into a sploot
    "rest":        ("rest",        12, True,  ["--no-leg-gaps", "--range", "0:999"]),  # splooted, awake, looking about
    "restup":      ("restup",      16, False, ["--no-leg-gaps", "--range", "0:17"]),
    "sitdown":     ("sitdown",     20, False, ["--range", "0:17"]),    # trimmed: no frozen frames after she's down
    "standup":     ("standup",     20, False, ["--range", "2:13"]),    # trimmed: no half-second freeze before she sets off
}
MARGIN = 2  # must match spriteize.MARGIN


def main():
    only = set(sys.argv[1:])
    meta_path = ROOT / "sprites/sprites.json"
    meta = json.loads(meta_path.read_text()) if meta_path.exists() else {"animations": {}}
    meta["footOffset"] = MARGIN
    for clip, (folder, fps, loop, extra) in PLAN.items():
        if only and clip.split("@")[0] not in only:
            continue
        src = ROOT / "gen/vid" / clip.split("@")[0]
        if not (src / "000.png").exists():
            print(f"-- {clip}: no clip yet, keeping existing sprites/{folder}")
            continue
        args = [sys.executable, str(ROOT / "tools/spriteize.py"), str(src), str(ROOT / "sprites" / folder)]
        args += (["--loop"] if loop else []) + extra
        subprocess.run(args, check=True)
        meta["animations"][folder] = {"fps": fps, "loop": loop}
    meta_path.write_text(json.dumps(meta, indent=2) + "\n")


if __name__ == "__main__":
    main()
