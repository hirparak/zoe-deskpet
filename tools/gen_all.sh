#!/usr/bin/env bash
# Generate every Wan clip listed in tools/animations.tsv into gen/vid/<name>/. Skips clips already made.
set -u
cd "$(dirname "$0")/.."
PRE="2D pixel art video game sprite animation of a small fluffy black and tan dog. The dog"
POST="The dog stays in the centre of the frame, the camera is completely still, plain flat white background, crisp pixel art style, the same pixel art dog in every frame."
grep -v '^#' tools/animations.tsv | while IFS=$'\t' read -r name start end frames prompt; do
  [ -z "$name" ] && continue
  [ -f "gen/vid/$name/000.png" ] && [ -z "${FORCE:-}" ] && { echo "skip $name"; continue; }
  args=(); [ "$end" = "-" ] && end=""
  [[ "$start" == *! ]] && { start=${start%!}; args+=(--mirror-start); }
  if [ -n "$end" ]; then [[ "$end" == *! ]] && { end=${end%!}; args+=(--mirror-end); }; args+=(--end "gen/keys/$end.png"); fi
  python3 tools/comfy_wan.py "gen/keys/$start.png" "${args[@]}" --frames "$frames" --seed 7 \
    --outdir "gen/vid/$name" --prompt "$PRE $prompt $POST" < /dev/null || echo "FAILED $name"
done
