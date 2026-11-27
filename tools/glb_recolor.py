#!/usr/bin/env python3
"""Fix the side the image-to-3D model never saw: on a flat-coloured (toy-style) model it comes out grey.
Snap unsaturated mid-grey vertex colours to the coat colour, in place in the GLB.

  python3 tools/glb_recolor.py in.glb out.glb
"""
import json, struct, sys
import numpy as np
b = bytearray(open(sys.argv[1], "rb").read())
jl = struct.unpack("<I", b[12:16])[0]; j = json.loads(b[20:20 + jl]); bin0 = 20 + jl + 8
prim = j["meshes"][0]["primitives"][0]; acc = j["accessors"][prim["attributes"]["COLOR_0"]]; bv = j["bufferViews"][acc["bufferView"]]
ncomp = {"VEC3": 3, "VEC4": 4}[acc["type"]]; dt = {5126: np.float32, 5121: np.uint8, 5123: np.uint16}[acc["componentType"]]
off = bin0 + bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
col = np.frombuffer(b, dt, acc["count"] * ncomp, off).reshape(-1, ncomp).copy()
scale = {np.float32: 1.0, np.uint8: 255.0, np.uint16: 65535.0}[dt]
rgb = col[:, :3].astype(np.float32) / scale
sat = rgb.max(axis=1) - rgb.min(axis=1); lum = rgb.mean(axis=1)
grey = (sat < 0.10) & (lum > 0.08)                     # unsaturated and lighter than the black coat
coat = np.median(rgb[(sat < 0.10) & (lum <= 0.08)], axis=0) if ((sat < 0.10) & (lum <= 0.08)).any() else np.array([0.02, 0.02, 0.025])
rgb[grey] = coat
col[:, :3] = (rgb * scale).astype(dt)
b[off:off + col.nbytes] = col.tobytes()
open(sys.argv[2], "wb").write(b)
print(f"{sys.argv[2]}: {acc['count']} vertices, {int(grey.sum())} recoloured ({100 * grey.mean():.0f}%), colour type {acc['type']}/{dt.__name__}")
