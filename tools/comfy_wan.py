#!/usr/bin/env python3
"""Animate a sprite key pose with Wan 2.2 I2V 14B (lightx2v 4-step) on ComfyUI.

  python3 tools/comfy_wan.py start.png --prompt "..." --outdir gen/vid/walk [--end end.png] [--frames 49]

With --end, the clip runs from the start pose to the end pose (first/last frame) - used for
transitions so animations chain cleanly. Writes outdir/000.png ... one PNG per video frame.
"""
import argparse, io, random, sys
from pathlib import Path
from PIL import Image
sys.path.insert(0, str(Path(__file__).parent))
from comfy_edit import upload, run_all

NEG = ("two tails, extra tail, four ears, extra ears, duplicate head, front paw raised, two legs moving at once, five legs, six legs, extra paws, camera movement, zoom, pan, background change, scenery, floor, shadow, 3D render, realistic photo, blurry, "
       "smooth gradients, extra legs, extra limbs, deformed, morphing, flicker, text, watermark, static, frozen")
SIZE = 640
BASELINE = int(SIZE * 0.88)   # every clip has her feet on this row
REF_SPRITE = Path(__file__).resolve().parent.parent / "gen/keys/stand_side.png"
REF_FILL = 0.62               # the standing reference fills this much of the frame; all poses share its scale


def _fg_box(im):
    return im.convert("L").point(lambda v: 255 if v < 235 else 0).getbbox() or (0, 0, im.width, im.height)


def _scale():
    """Pixels-per-source-pixel that makes the standing reference fill REF_FILL of the canvas."""
    im = Image.open(REF_SPRITE).convert("RGB")
    b = _fg_box(im)
    return SIZE * REF_FILL / max(b[2] - b[0], b[3] - b[1])


def pad(path, tmp, mirror=False):
    """Centre a key pose on a white SIZExSIZE canvas, feet on BASELINE, at the shared scale
    (so a curled-up sleeping Zoe stays smaller than a standing one)."""
    im = Image.open(path).convert("RGB")
    im = im.crop(_fg_box(im))
    if mirror:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    k = _scale()
    im = im.resize((round(im.width * k), round(im.height * k)), Image.NEAREST)
    canvas = Image.new("RGB", (SIZE, SIZE), "white")
    canvas.paste(im, ((SIZE - im.width) // 2, BASELINE - im.height))
    canvas.save(tmp)
    return tmp


def workflow(start, end, prompt, seed, frames):
    wf = {
        "1": {"class_type": "UNETLoader", "inputs": {"unet_name": "wan2.2_i2v_high_noise_14B_fp16.safetensors", "weight_dtype": "fp8_e4m3fn"}},
        "2": {"class_type": "UNETLoader", "inputs": {"unet_name": "wan2.2_i2v_low_noise_14B_fp16.safetensors", "weight_dtype": "fp8_e4m3fn"}},
        "3": {"class_type": "LoraLoaderModelOnly", "inputs": {"model": ["1", 0], "lora_name": "wan2.2_i2v_lightx2v_4steps_lora_v1_high_noise.safetensors", "strength_model": 1.0}},
        "4": {"class_type": "LoraLoaderModelOnly", "inputs": {"model": ["2", 0], "lora_name": "wan2.2_i2v_lightx2v_4steps_lora_v1_low_noise.safetensors", "strength_model": 1.0}},
        "5": {"class_type": "ModelSamplingSD3", "inputs": {"model": ["3", 0], "shift": 5.0}},
        "6": {"class_type": "ModelSamplingSD3", "inputs": {"model": ["4", 0], "shift": 5.0}},
        "7": {"class_type": "CLIPLoader", "inputs": {"clip_name": "umt5_xxl_fp8_e4m3fn_scaled.safetensors", "type": "wan"}},
        "8": {"class_type": "VAELoader", "inputs": {"vae_name": "wan_2.1_vae.safetensors"}},
        "9": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["7", 0], "text": prompt}},
        "10": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["7", 0], "text": NEG}},
        "11": {"class_type": "LoadImage", "inputs": {"image": start}},
    }
    common = {"positive": ["9", 0], "negative": ["10", 0], "vae": ["8", 0], "width": SIZE, "height": SIZE, "length": frames, "batch_size": 1}
    if end:
        wf["12"] = {"class_type": "LoadImage", "inputs": {"image": end}}
        wf["13"] = {"class_type": "WanFirstLastFrameToVideo", "inputs": {**common, "start_image": ["11", 0], "end_image": ["12", 0]}}
    else:
        wf["13"] = {"class_type": "WanImageToVideo", "inputs": {**common, "start_image": ["11", 0]}}
    ks = {"positive": ["13", 0], "negative": ["13", 1], "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple", "steps": 4, "noise_seed": seed}
    wf["20"] = {"class_type": "KSamplerAdvanced", "inputs": {**ks, "model": ["5", 0], "latent_image": ["13", 2], "add_noise": "enable",
                "start_at_step": 0, "end_at_step": 2, "return_with_leftover_noise": "enable"}}
    wf["21"] = {"class_type": "KSamplerAdvanced", "inputs": {**ks, "model": ["6", 0], "latent_image": ["20", 0], "add_noise": "disable",
                "start_at_step": 2, "end_at_step": 4, "return_with_leftover_noise": "disable"}}
    wf["22"] = {"class_type": "VAEDecode", "inputs": {"samples": ["21", 0], "vae": ["8", 0]}}
    wf["23"] = {"class_type": "SaveImage", "inputs": {"images": ["22", 0], "filename_prefix": "zoe_vid"}}
    return wf


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("start"); ap.add_argument("--end")
    ap.add_argument("--prompt", required=True); ap.add_argument("--outdir", required=True)
    ap.add_argument("--frames", type=int, default=49); ap.add_argument("--seed", type=int)
    ap.add_argument("--mirror-start", action="store_true"); ap.add_argument("--mirror-end", action="store_true")
    ap.add_argument("--server", default="http://127.0.0.1:18188")
    a = ap.parse_args()
    out = Path(a.outdir); out.mkdir(parents=True, exist_ok=True)
    s = upload(a.server, pad(a.start, out / "_start.png", a.mirror_start))
    e = upload(a.server, pad(a.end, out / "_end.png", a.mirror_end)) if a.end else None
    seed = a.seed if a.seed is not None else random.randrange(2**32)
    frames = run_all(a.server, workflow(s, e, a.prompt, seed, a.frames))
    for f in out.glob("[0-9]*.png"): f.unlink()
    for i, b in enumerate(frames):
        Image.open(io.BytesIO(b)).save(out / f"{i:03d}.png")
    print(f"{out}: {len(frames)} frames, seed={seed}")


if __name__ == "__main__":
    main()
