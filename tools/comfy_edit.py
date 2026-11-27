#!/usr/bin/env python3
"""Run one Qwen-Image-Edit-2511 (4-step lightning) edit on a ComfyUI server.

  python3 tools/comfy_edit.py --out out.png --prompt "..." ref1.jpg [ref2.jpg ...] [--seed N] [--angles 0.8]

Server defaults to http://127.0.0.1:18188 (SSH tunnel to the ComfyUI machine). Up to 3 reference images.
"""
import argparse
import json
import random
import time
import urllib.request
import uuid
from pathlib import Path

UNET = "qwen_image_edit_2511_fp8_e4m3fn_scaled_lightning_comfyui_4steps_v1.0.safetensors"
CLIP = "qwen_2.5_vl_7b_fp8_scaled.safetensors"
VAE = "qwen_image_vae.safetensors"
UNET_FULL = "qwen_image_edit_2511_fp8mixed.safetensors"   # not distilled: 30 steps with real CFG, follows the prompt more closely
ANGLES_LORA = "qwen-edit-2511-multiple-angles.safetensors"


def upload(server, path):
    boundary = uuid.uuid4().hex
    data = Path(path).read_bytes()
    name = f"zoe_{uuid.uuid4().hex[:8]}{Path(path).suffix}"
    body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"{name}\"\r\n"
            f"Content-Type: application/octet-stream\r\n\r\n").encode() + data + \
           f"\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"overwrite\"\r\n\r\ntrue\r\n--{boundary}--\r\n".encode()
    req = urllib.request.Request(f"{server}/upload/image", body, {"Content-Type": f"multipart/form-data; boundary={boundary}"})
    return json.load(urllib.request.urlopen(req))["name"]


def workflow(images, prompt, seed, width, height, angles, full=False, negative=""):
    wf = {
        "1": {"class_type": "UNETLoader", "inputs": {"unet_name": UNET_FULL if full else UNET, "weight_dtype": "default"}},
        "3": {"class_type": "CLIPLoader", "inputs": {"clip_name": CLIP, "type": "qwen_image"}},
        "4": {"class_type": "VAELoader", "inputs": {"vae_name": VAE}},
        "7": {"class_type": "EmptySD3LatentImage", "inputs": {"width": width, "height": height, "batch_size": 1}},
    }
    model = ["1", 0]
    if angles:
        wf["2"] = {"class_type": "LoraLoaderModelOnly", "inputs": {"model": model, "lora_name": ANGLES_LORA, "strength_model": angles}}
        model = ["2", 0]
    wf["5"] = {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": model, "shift": 3.1}}
    enc = {"clip": ["3", 0], "prompt": prompt, "vae": ["4", 0]}
    neg = {"clip": ["3", 0], "prompt": negative, "vae": ["4", 0]}
    for i, name in enumerate(images, 1):
        wf[f"10{i}"] = {"class_type": "LoadImage", "inputs": {"image": name}}
        wf[f"11{i}"] = {"class_type": "FluxKontextImageScale", "inputs": {"image": [f"10{i}", 0]}}
        enc[f"image{i}"] = neg[f"image{i}"] = [f"11{i}", 0]
    wf["20"] = {"class_type": "TextEncodeQwenImageEditPlus", "inputs": enc}
    wf["21"] = {"class_type": "TextEncodeQwenImageEditPlus", "inputs": neg}
    wf["30"] = {"class_type": "KSampler", "inputs": {
        "model": ["5", 0], "positive": ["20", 0], "negative": ["21", 0], "latent_image": ["7", 0],
        "seed": seed, "steps": 30 if full else 4, "cfg": 4.0 if full else 1.0, "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}}
    wf["31"] = {"class_type": "VAEDecode", "inputs": {"samples": ["30", 0], "vae": ["4", 0]}}
    wf["32"] = {"class_type": "SaveImage", "inputs": {"images": ["31", 0], "filename_prefix": "zoe"}}
    return wf


def run_all(server, wf, timeout=1800):
    """Queue a workflow and return the bytes of every output image, in order."""
    req = urllib.request.Request(f"{server}/prompt", json.dumps({"prompt": wf}).encode(), {"Content-Type": "application/json"})
    pid = json.load(urllib.request.urlopen(req))["prompt_id"]
    t0 = time.time()
    while time.time() - t0 < timeout:
        h = json.load(urllib.request.urlopen(f"{server}/history/{pid}"))
        if pid in h:
            st = h[pid].get("status", {})
            if st.get("status_str") == "error":
                raise SystemExit("ComfyUI error: " + json.dumps(st.get("messages", []))[-1500:])
            if st.get("completed"):
                out = []
                for node in h[pid]["outputs"].values():
                    for img in node.get("images", []):
                        q = f"filename={img['filename']}&subfolder={img['subfolder']}&type={img['type']}"
                        out.append(urllib.request.urlopen(f"{server}/view?{q}").read())
                return out
        time.sleep(2)
    raise SystemExit("timed out")


def run(server, wf, out, timeout=600):
    req = urllib.request.Request(f"{server}/prompt", json.dumps({"prompt": wf}).encode(), {"Content-Type": "application/json"})
    pid = json.load(urllib.request.urlopen(req))["prompt_id"]
    t0 = time.time()
    while time.time() - t0 < timeout:
        h = json.load(urllib.request.urlopen(f"{server}/history/{pid}"))
        if pid in h:
            st = h[pid].get("status", {})
            if st.get("status_str") == "error":
                raise SystemExit("ComfyUI error: " + json.dumps(st.get("messages", []))[-1500:])
            for node in h[pid]["outputs"].values():
                for img in node.get("images", []):
                    q = f"filename={img['filename']}&subfolder={img['subfolder']}&type={img['type']}"
                    Path(out).write_bytes(urllib.request.urlopen(f"{server}/view?{q}").read())
                    return time.time() - t0
        time.sleep(1)
    raise SystemExit("timed out")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("images", nargs="+")
    ap.add_argument("--prompt", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--seed", type=int, default=None)
    ap.add_argument("--width", type=int, default=1024)
    ap.add_argument("--height", type=int, default=1024)
    ap.add_argument("--angles", type=float, default=0.0, help="multiple-angles LoRA strength (0 = off)")
    ap.add_argument("--full", action="store_true", help="full 30-step model with CFG instead of the 4-step lightning one")
    ap.add_argument("--negative", default="")
    ap.add_argument("--server", default="http://127.0.0.1:18188")
    a = ap.parse_args()
    names = [upload(a.server, p) for p in a.images[:3]]
    seed = a.seed if a.seed is not None else random.randrange(2**32)
    secs = run(a.server, workflow(names, a.prompt, seed, a.width, a.height, a.angles, a.full, a.negative), a.out)
    print(f"{a.out}  seed={seed}  {secs:.0f}s")


if __name__ == "__main__":
    main()
