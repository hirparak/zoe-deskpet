# Starting prompt: build your own desk pet

Paste everything below the line into a fresh Claude Code session, in an empty project folder.
Fill in the three bracketed bits first.

---

I want to build a desktop pet for Windows based on my own pet: [NAME, SPECIES/BREED, e.g. "Biscuit, a
ginger tabby cat"]. It's for [WHO IT'S FOR AND THEIR PC, e.g. "my partner's Windows 11 laptop, as a
surprise"]. I have photos of [NAME] in [FOLDER PATH]. A friend built one of these for his dog with
Claude Code; the spec and the lessons from that build are below so we don't repeat his mistakes.
Start by reading the photos and asking me about my pet's real habits and quirks, because the
behaviours that make it feel like *my* pet come from those. Then propose a plan before building.

## What it is

A little pixel-art animal that lives on the Windows desktop. It walks along the taskbar and the top
edges of open windows, jumps up onto windows, rides along when a window is dragged, and falls when
one closes. It can be clicked, picked up with the mouse and thrown. It reacts to what the person is
doing: it naps out of the way while they work, wakes when they stop, dozes when they're away from
the keyboard, and hides during full-screen video and screen sharing. It must never get in the way:
it never takes keyboard focus, clicks on empty space around it pass through to whatever is
underneath, and it is silent.

## How the program is built (this worked well; change it only for a good reason)

- C# / .NET, WinForms, published as one self-contained `.exe` so nothing needs installing on the
  target PC. It can be built on Linux in the official .NET SDK Docker image.
- The pet is a borderless always-on-top window exactly the size of the current frame, drawn with
  per-pixel alpha (`UpdateLayeredWindow`), with the no-activate and tool-window styles so it never
  steals focus or shows in the taskbar. Fully transparent pixels click through.
- Exclude the window from screen capture (`SetWindowDisplayAffinity`) so it doesn't appear in Teams
  or Zoom screen shares, and hide it while a full-screen app is running.
- Split the code in two. A pure "core" library with no Windows dependencies holds the behaviour
  state machine, simple platformer physics, the rules, and the logic that turns the list of open
  windows into "platforms" she can stand on. A thin Windows shell does the drawing, window
  enumeration, mouse handling and tray icon. The core is unit tested on any OS; this caught most
  bugs before they reached a real screen.
- Platforms: each monitor's work area is a floor; the top edge of each visible window is a ledge,
  minus the parts covered by windows above it. Side-by-side monitors are separate floors at the
  same height, so anything that plans a route (run to X) must treat them as one continuous run.
- Sprites are folders of PNG frames (`sprites/<animation>/000.png ...`), drawn facing right with
  the feet at the bottom-centre, plus a small JSON file giving each animation's frame rate and
  whether it loops. The program flips them for left. A missing animation falls back to a simpler
  one, so the program runs from day one with placeholder art.
- A settings file next to the exe, reloaded live: size, nap corner, idle time, energy level, and
  rules of the form "when the active window's process name / title / browser address contains X,
  nap (or doze, or hide)". Web apps often don't put their name in the tab title, so rules also need
  to match the browser's address bar (read with UI Automation, only for the foreground browser,
  never logged).
- A tray icon with a small menu (come here, nap, hide, throw the ball, quit).
- Install script: copy to `Program Files` (admin-write-only, so nothing can be planted next to the
  exe) and register a scheduled task that starts it at logon *inside the user's interactive
  session*. A service can't draw on the desktop. Log mood changes by rule name only, never window
  titles.

## Behaviours worth having

Movement: walk, run, turn round, sit down / stand up as real transitions, jump onto windows, fall.
Idles: sit, look at you, yawn, stretch, sniff, scratch, dig, chase tail, tail wag, lie down awake.
Sleep: lie down, sleep (just breathing), get up. Play: a ball as its own little window with
bounce-and-roll physics that the pet chases, bats about, carries and drops, and that the person can
grab and throw; running off-screen and coming back with the ball; sitting and staring, then pawing,
when it isn't thrown; zoomies. Social: a greeting when the person comes back from being away,
following the mouse pointer and pouncing when it stops, asking for attention after being ignored,
a belly rub when the mouse is rubbed over it. Ask me which of these fit my pet and what's missing.

Rule that matters: every change of posture goes through a transition animation. An animation that
snaps from sitting to lying looks broken, even if every frame is perfect.

## Making the art (the hard part)

The frames were generated from photos with open image and video models running locally in ComfyUI.
If I don't have a GPU machine for that, tell me the options before choosing one.

1. An image-edit model (Qwen-Image-Edit was used) turns a couple of photos into one reference
   pixel-art sprite, standing, side-on, on a plain white background. Everything else is made from
   that one reference so the animal stays the same animal.
2. The same edit model makes one still "key pose" per posture from the reference: sitting, lying,
   play bow, begging, and so on. Generate several candidates of each and pick by eye.
3. An image-to-video model with first-and-last-frame conditioning (Wan 2.2 was used) animates
   between key poses. Same pose at both ends gives a seamless loop; different poses give a
   transition. Generate three or four attempts of each clip.
4. A script turns video frames into sprites: knock out the white background by flood fill from the
   border (so white fur survives), shrink onto a pixel grid, snap every pixel to one shared small
   palette taken from the reference (stops colours shimmering between frames), force a dark
   outline on the silhouette edge (kills pale fringes), and crop so the feet stay put.

Lessons that cost the most time:

- **Check every frame at large size, on a coloured background, before accepting a clip.** Thumbnails
  hide doubled tails, extra legs and see-through holes. Build a contact-sheet tool first and use it
  on every candidate. Don't report an animation as good from a quick look.
- **Most errors come from the key pose, not the video.** If the still has two tails, every clip made
  from it has two tails. Fix the still.
- **Small motions work; big 3D pose changes morph.** Getting from A to B in one clip fails when A and
  B are very different. Break it into steps through intermediate key poses.
- **Side-on poses can't hide a limb.** A side view of "one hind leg lifted" kept the folded leg and
  added a raised one. The same action facing the viewer worked.
- **Trim the frozen frames.** Clips often end with half a second of stillness, which shows up as an
  odd pause before the next action. Cut them.
- **Watch for a discoloured frame** near the end of short clips (second- or third-to-last); cut it.
- Colours the reference doesn't contain (a pink tongue, a ball) need adding to the palette by hand,
  guarded so they only apply where the source pixel really is that colour.
- Keep every generation scriptable and note which attempt was chosen, so any one animation can be
  redone later without touching the rest.

## How I'd like you to work

- Get a placeholder-art version running end to end early, then improve the art and behaviours.
- Write tests for the core as you go, especially: never walks off-screen, crosses between monitors,
  posture changes always use a transition, long random sessions never throw or get stuck.
- I can't easily see local image files, so publish previews of the animations somewhere I can open
  them (a web page with the animations playing), and update it as things change.
- Before testing on the real PC, confirm with me. Don't send any keyboard or mouse input to it, and
  if it's a surprise, only test while nobody is using it. Clean up test files afterwards.
- Tell me plainly what you have and haven't seen working on a real screen. Tests passing is not the
  same as it looking right.
- Don't put keys, passwords, machine names or network addresses in the repository.
