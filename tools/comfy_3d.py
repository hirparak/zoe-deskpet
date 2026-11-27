#!/usr/bin/env python3
"""Image -> 3D test with TripoSplat on the ComfyUI server: gaussian splat, mesh (GLB), and turntable renders of both.

  python3 tools/comfy_3d.py input.png outdir [--seed N] [--invert-mask]
"""
import argparse, json, sys, time, urllib.request, urllib.parse
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent))
from comfy_edit import upload

def workflow(image, seed, tag, invert, level=0.6, smooth=0, res=384):
    cam = lambda yaw: {"class_type": "CreateCameraInfo", "inputs": {
        "mode": "orbit", "mode.yaw": yaw, "mode.pitch": 12.0, "mode.distance": 2.6,
        "target_x": 0.0, "target_y": 0.0, "target_z": 0.0, "roll": 0.0, "fov": 35.0, "zoom": 1.0, "camera_type": "perspective"}}
    wf = {
        "1": {"class_type": "LoadImage", "inputs": {"image": image}},
        "2": {"class_type": "LoadBackgroundRemovalModel", "inputs": {"bg_removal_name": "birefnet.safetensors"}},
        "3": {"class_type": "RemoveBackground", "inputs": {"bg_removal_model": ["2", 0], "image": ["1", 0]}},
        "4": {"class_type": "InvertMask", "inputs": {"mask": ["3", 0]}},
        "5": {"class_type": "TripoSplatPreprocessImage", "inputs": {"image": ["1", 0], "mask": ["4" if invert else "3", 0], "erode_radius": 1, "size": 1024}},
        "6": {"class_type": "CLIPVisionLoader", "inputs": {"clip_name": "dino_v3_vit_h.safetensors"}},
        "7": {"class_type": "VAELoader", "inputs": {"vae_name": "flux2-vae.safetensors"}},
        "8": {"class_type": "VAELoader", "inputs": {"vae_name": "triposplat_vae_decoder_fp16.safetensors"}},
        "9": {"class_type": "UNETLoader", "inputs": {"unet_name": "triposplat_fp16.safetensors", "weight_dtype": "default"}},
        "10": {"class_type": "TripoSplatConditioning", "inputs": {"clip_vision": ["6", 0], "vae": ["7", 0], "image": ["5", 0]}},
        "11": {"class_type": "KSampler", "inputs": {"model": ["9", 0], "positive": ["10", 0], "negative": ["10", 1], "latent_image": ["10", 2],
               "seed": seed, "steps": 20, "cfg": 3.0, "sampler_name": "dpmpp_2m", "scheduler": "simple", "denoise": 1.0}},
        "12": {"class_type": "VAEDecodeTripoSplat", "inputs": {"samples": ["11", 0], "vae": ["8", 0], "num_gaussians": 262144, "seed": seed}},
        "13": {"class_type": "SaveImage", "inputs": {"images": ["5", 0], "filename_prefix": f"{tag}_pre"}},
        # splat turntable, 8 views
        "20": cam(35.0),
        "21": {"class_type": "RenderSplat", "inputs": {"splat": ["12", 0], "width": 768, "height": 768, "frames": 8, "splat_scale": 1.0, "sharpen": 2.0,
               "headlight_shading": 0.0, "opacity_threshold": 0.0, "render_style": "color", "background": "#6ea56e", "camera_info": ["20", 0]}},
        "22": {"class_type": "SaveImage", "inputs": {"images": ["21", 0], "filename_prefix": f"{tag}_splat"}},
        # mesh
        "30": {"class_type": "SplatToMesh", "inputs": {"splat": ["12", 0], "resolution": res, "kernel": 5, "smooth": smooth, "level": level,
               "min_component": 500, "min_opacity": 0.02, "color_sharpen": 2.0}},
        "31": {"class_type": "SaveGLB", "inputs": {"mesh": ["30", 0], "filename_prefix": f"3d/{tag}_mesh"}},
        # a real-time-sized copy: millions of triangles down to tens of thousands
        "34": {"class_type": "DecimateMesh", "inputs": {"mesh": ["30", 0], "target_face_count": 80000, "placement_mode": "midpoint"}},
        "35": {"class_type": "SaveGLB", "inputs": {"mesh": ["34", 0], "filename_prefix": f"3d/{tag}_low"}},
        "32": {"class_type": "SplatToFile3D", "inputs": {"splat": ["12", 0], "format": "ply"}},
        "33": {"class_type": "SaveGLB", "inputs": {"mesh": ["32", 0], "filename_prefix": f"3d/{tag}_splat"}},
    }
    for i, yaw in enumerate((35.0, 125.0, 215.0, 305.0)):
        wf[f"4{i}0"] = cam(yaw)
        wf[f"4{i}1"] = {"class_type": "RenderMesh", "inputs": {"mesh": ["30", 0], "mode": "auto", "width": 768, "height": 768, "background": "#6ea56e", "camera_info": [f"4{i}0", 0]}}
        wf[f"4{i}2"] = {"class_type": "RenderMesh", "inputs": {"mesh": ["30", 0], "mode": "solid", "width": 768, "height": 768, "background": "#6ea56e", "camera_info": [f"4{i}0", 0]}}
        wf[f"4{i}3"] = {"class_type": "SaveImage", "inputs": {"images": [f"4{i}1", 0], "filename_prefix": f"{tag}_mesh{i}"}}
        wf[f"4{i}4"] = {"class_type": "SaveImage", "inputs": {"images": [f"4{i}2", 0], "filename_prefix": f"{tag}_clay{i}"}}
    return wf

def main():
    ap = argparse.ArgumentParser(); ap.add_argument("image"); ap.add_argument("outdir")
    ap.add_argument("--seed", type=int, default=46); ap.add_argument("--invert-mask", action="store_true")
    ap.add_argument("--level", type=float, default=0.6); ap.add_argument("--smooth", type=int, default=0); ap.add_argument("--res", type=int, default=384)
    ap.add_argument("--server", default="http://127.0.0.1:18188")
    a = ap.parse_args()
    out = Path(a.outdir); out.mkdir(parents=True, exist_ok=True)
    tag = "zoe3d_" + out.name
    wf = workflow(upload(a.server, a.image), a.seed, tag, a.invert_mask, a.level, a.smooth, a.res)
    req = urllib.request.Request(f"{a.server}/prompt", json.dumps({"prompt": wf}).encode(), {"Content-Type": "application/json"})
    try: pid = json.load(urllib.request.urlopen(req))["prompt_id"]
    except urllib.error.HTTPError as e: sys.exit(e.read().decode()[:3000])
    t0 = time.time()
    while True:
        h = json.load(urllib.request.urlopen(f"{a.server}/history/{pid}"))
        if pid in h: break
        if time.time() - t0 > 1800: sys.exit("timeout")
        time.sleep(3)
    h = h[pid]
    if h["status"]["status_str"] != "success": sys.exit(json.dumps(h["status"])[:3000])
    n = 0
    for node in h["outputs"].values():
        for kind in node.values():
            for f in kind if isinstance(kind, list) else []:
                if not isinstance(f, dict) or "filename" not in f: continue
                q = urllib.parse.urlencode({"filename": f["filename"], "subfolder": f.get("subfolder", ""), "type": f.get("type", "output")})
                (out / f["filename"]).write_bytes(urllib.request.urlopen(f"{a.server}/view?{q}").read()); n += 1
    print(f"{out}: {n} files, {time.time() - t0:.0f}s")

if __name__ == "__main__":
    main()
