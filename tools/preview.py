#!/usr/bin/env python3
"""Write gen/zoe-sprites.html: every animation in sprites/ playing at its real fps (self-contained page)."""
import base64, json
from pathlib import Path
ROOT = Path(__file__).resolve().parent.parent
meta = json.loads((ROOT / "sprites/sprites.json").read_text())
GROUPS = [
    ("Newest: sploot, cleaner gallop", ["restdown", "rest", "restup", "run"]),
    ("Small idles, and redone bark / happy / sleep", ["dig", "spin", "circle", "bark", "happy", "sleep"]),
    ("Belly rubs and asking to play", ["bellydown", "belly", "bellyup", "paw"]),
    ("Playtime", ["wag", "greet", "playbow", "pickup", "carry", "drop", "ball"]),
    ("On the move", ["walk", "run", "sniff", "jump (whole)", "fall", "turn"]),
    ("Sitting about", ["sit", "sit~look", "sit~rest", "happy", "yawn", "sitdown", "standup"]),
    ("Naps", ["liedown", "sleep", "rollover", "sleep~belly", "rollback", "getup", "stretch"]),
    ("Picked up", ["dangle"]),
]
LABEL = {"restdown": "sploot (lie down)", "rest": "sploot", "restup": "up from sploot", "spin": "chase tail", "circle": "circling before bed", "bellydown": "roll over", "belly": "belly rub", "bellyup": "roll back up", "paw": "paw on your knee",
         "wag": "tail wag", "greet": "greeting (paws up)", "playbow": "play bow", "pickup": "pick up ball",
         "carry": "carry ball", "drop": "drop ball",
         "sit~look": "sit (looking at you)", "sit~rest": "rest", "sleep~belly": "sleep (belly up)", "liedown": "lie down",
         "getup": "get up", "rollover": "roll over", "rollback": "roll back", "sitdown": "sit down", "standup": "stand up", "happy": "happy (clicked)", "turn": "turn around"}
NOTE = {"bellydown": "when her owner rubs her with the mouse", "belly": "for as long as the rubbing lasts",
        "bark": "at horses and dogs on screen (silent)", "paw": "when the ball isn't thrown",
        "wag": "standing about, happy", "greet": "when her owner comes back, or double-clicks her",
        "playbow": "then the zoomies start", "carry": "brings it to the mouse pointer", "ball": "bounces and rolls; her owner can throw it",
        "sniff": "head down, snuffle, head up", "stretch": "yawn into a play bow", "turn": "steps round through facing you",
        "liedown": "settles down, chin onto paws", "happy": "when her owner clicks her", "dangle": "when her owner drags her",
        "sleep": "chin on paws, slow breathing", "rollover": "mid-nap, onto her back", "rollback": "before getting up", "sit~look": "head tilt", "yawn": "big sleepy yawn"}
anims = {}
for d in sorted(p for p in (ROOT / "sprites").iterdir() if p.is_dir()):
    frames = ["data:image/png;base64," + base64.b64encode(f.read_bytes()).decode() for f in sorted(d.glob("*.png"))]
    m = meta["animations"].get(d.name, {"fps": 8, "loop": True})
    anims[d.name] = {"frames": frames, "fps": m["fps"], "loop": m["loop"], "label": LABEL.get(d.name, d.name), "note": NOTE.get(d.name, "")}
# Show the jump as the app plays it: crouch on the ground, leap, then a few falling frames.
if all(k in anims for k in ("jumpprep", "jump", "fall")):
    seq = anims["jumpprep"]["frames"] + anims["jump"]["frames"] + [anims["jump"]["frames"][-1]] * 4 + anims["fall"]["frames"][:8]
    anims["jump (whole)"] = {"frames": seq, "fps": 18, "loop": False, "label": "jump", "note": "crouch, leap, fall"}
    for k in ("jumpprep", "jump"):
        anims.pop(k)
groups = [(t, [n for n in names if n in anims]) for t, names in GROUPS]
total = sum(len(a["frames"]) for a in anims.values())

html = """<title>Zoe Sprite Sheet</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Silkscreen&family=Inter:wght@400;600&display=swap">
<style>
/* Layout: a field guide of animation tiles, grouped by what Zoe is doing; each tile has a floor line at her feet */
:root{
  --bg:#f3f1ec; --card:#fffdf8; --ink:#2b2622; --muted:#7a6f66; --floor:#d9cfc2; --accent:#b8743a;
  --display:"Silkscreen","Courier New",monospace; --body:"Inter",system-ui,-apple-system,sans-serif;
}
@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){--bg:#1d1b19;--card:#292623;--ink:#efe9e2;--muted:#a89c90;--floor:#463f38;--accent:#e0a165;color-scheme:dark}}
:root[data-theme="dark"]{--bg:#1d1b19;--card:#292623;--ink:#efe9e2;--muted:#a89c90;--floor:#463f38;--accent:#e0a165;color-scheme:dark}
body{background:var(--bg);color:var(--ink);font:15px/1.5 var(--body)}
.wrap{max-width:1040px;margin:0 auto;padding-block:20px 40px;padding-left:max(16px,env(safe-area-inset-left,0px));padding-right:max(16px,env(safe-area-inset-right,0px))}
h1{font:400 28px/1.1 var(--display);margin:0 0 6px;letter-spacing:.02em;text-wrap:balance}
.lede{color:var(--muted);margin:0 0 18px;max-width:62ch}
.ctl{display:flex;gap:8px;flex-wrap:wrap;margin:0 0 22px}
button{font:600 13px var(--body);color:var(--ink);background:var(--card);border:1px solid var(--floor);border-radius:999px;padding:7px 14px;cursor:pointer}
button[aria-pressed="true"]{background:var(--accent);border-color:var(--accent);color:var(--bg)}
button:focus-visible{outline:2px solid var(--accent);outline-offset:2px}
h2{font:400 15px var(--display);color:var(--accent);margin:26px 0 10px;letter-spacing:.06em;text-transform:uppercase}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:10px}
.tile{background:var(--card);border-radius:8px;padding:10px 10px 12px;display:flex;flex-direction:column;min-width:0;cursor:pointer}
.stage{height:var(--stage,150px);display:flex;align-items:flex-end;justify-content:center;border-bottom:2px solid var(--floor);overflow:hidden}
.stage img{image-rendering:pixelated;width:calc(var(--w)*var(--z)*1px);height:auto;max-width:none}
.name{font-weight:600;margin-top:8px}
.meta{color:var(--muted);font-size:12px;font-variant-numeric:tabular-nums}
.note{color:var(--muted);font-size:12px}
footer{color:var(--muted);font-size:12px;margin-top:28px}
</style>
<div class="wrap">
  <h1>Zoe 🐾 sprite sheet</h1>
  <p class="lede">Every animation for her owner's desk pet, playing at its real speed. Loops run continuously; one-off moves replay every couple of seconds. Tap a tile to restart it.</p>
  <div class="ctl" role="group" aria-label="Zoom">
    <button id="z1" data-z="1">1×</button><button id="z15" data-z="1.5" aria-pressed="true">1.5× (desktop size)</button><button id="z2" data-z="2">2×</button>
  </div>
  <div id="groups"></div>
  <footer>__TOTAL__ frames across __COUNT__ animations · generated from photos of Zoe.</footer>
</div>
<script>
const A=__DATA__, G=__GROUPS__;
let zoom=1.5; const players=[];
const root=document.getElementById('groups');
for(const [title,names] of G){
  const h=document.createElement('h2'); h.textContent=title; root.appendChild(h);
  const grid=document.createElement('div'); grid.className='grid'; root.appendChild(grid);
  for(const n of names){
    const a=A[n]; const t=document.createElement('div'); t.className='tile'; t.tabIndex=0;
    t.innerHTML=`<div class="stage"><img alt="${a.label}"></div><div class="name"></div><div class="meta"></div><div class="note"></div>`;
    t.querySelector('.name').textContent=a.label;
    t.querySelector('.meta').textContent=`${a.frames.length} frames · ${a.fps} fps · ${a.loop?'loop':'once'}`;
    t.querySelector('.note').textContent=a.note;
    const img=t.querySelector('img'); const p={a,img,i:0,hold:0}; players.push(p);
    const probe=new Image(); probe.onload=()=>{img.style.setProperty('--w',probe.naturalWidth);}; probe.src=a.frames[0];
    img.src=a.frames[0];
    t.addEventListener('click',()=>{p.i=0;p.hold=0}); t.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){p.i=0;p.hold=0;e.preventDefault()}});
    grid.appendChild(t);
  }
}
function setZoom(z){zoom=z;document.documentElement.style.setProperty('--z',z);document.documentElement.style.setProperty('--stage',Math.round(105*z)+'px');
  document.querySelectorAll('[data-z]').forEach(b=>b.setAttribute('aria-pressed',String(+b.dataset.z===z)));}
document.querySelectorAll('[data-z]').forEach(b=>b.addEventListener('click',()=>setZoom(+b.dataset.z)));
setZoom(1.5);
const still=matchMedia('(prefers-reduced-motion: reduce)').matches;
let last=performance.now();
function tick(now){const dt=(now-last)/1000; last=now;
  for(const p of players){p.t=(p.t||0)+dt; const step=1/p.a.fps; if(still) continue;
    while(p.t>=step){p.t-=step; const n=p.a.frames.length;
      if(p.i<n-1)p.i++; else if(p.a.loop)p.i=0; else if(++p.hold>p.a.fps*1.5){p.i=0;p.hold=0}}
    const src=p.a.frames[p.i]; if(p.img.src!==src)p.img.src=src;}
  requestAnimationFrame(tick);}
requestAnimationFrame(tick);
</script>
"""
html = (html.replace("__DATA__", json.dumps(anims)).replace("__GROUPS__", json.dumps(groups))
        .replace("__TOTAL__", str(total)).replace("__COUNT__", str(len(anims))))
out = ROOT / "gen/zoe-sprites.html"
out.write_text(html)
print(out, f"{out.stat().st_size // 1024} KB")
