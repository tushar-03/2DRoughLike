# Two Souls

A fast, fluid **2D action-platformer roguelite** built in Unity 6. Gameplay follows *Dead Cells* (tight platforming, an invulnerable roll, two weapon slots, procedurally assembled levels, permadeath). The art direction targets *Hades* (bold outlines, saturated mythic palettes, painterly backdrops).

You are a bell-ringer's apprentice who died sealing a sunken cathedral-city. Each death returns you to a lantern-lit boathouse. The tone is melancholy but warm, with dry humour from a ferryman and an archivist.

> **Status: in active development. A playable vertical slice with a main menu, saves, eight enemy types and a procedurally painted first biome.** Movement, combat, level generation, loot, menus and the first art pass are built and tested; the boss, the hub, meta-progression, audio and the story arrive in later milestones (see the [roadmap](#roadmap)).

| Main menu (M5) | Shell Guard and its tower shield (M5) |
|---|---|
| ![Main menu](docs/images/m5_main_menu.png) | ![Shell Guard](docs/images/m5_gameplay_guard.png) |

| Exit portal (M5) | Bell-Wisp dive telegraph (M5) |
|---|---|
| ![Exit portal](docs/images/m5_exit_portal.png) | ![Bell-Wisp](docs/images/m5_combat_wisp.png) |

| Hag of Bells (boss, world 1) | Rust Warden (boss, world 2) |
|---|---|
| ![Witch boss](docs/images/m5_boss_witch.png) | ![Warden boss](docs/images/m5_boss_warden.png) |

The cast, all painted and animated by code ([how](#art)):

![Roster, part 1](docs/images/m5_roster_1.png)
![Roster, part 2](docs/images/m5_roster_2.png)

| HUD, loot and item card (M4) | Shop menu (M4) |
|---|---|
| ![HUD and loot](docs/images/m4-hud-and-loot.png) | ![Shop](docs/images/m4-shop.png) |

| Wall-jump shaft (M1) | Generated run with minimap (M3) |
|---|---|
| ![Wall jump shaft](docs/images/m1-wall-jump-shaft.png) | ![Generated run](docs/images/m3-generated-run.png) |

## What works today

**Movement (M1)**
- Custom kinematic controller (no Rigidbody solver): deterministic, never tunnels or sticks on seams.
- Run with tuned acceleration, variable-height jump, coyote time, jump and roll buffering, fast-fall.
- **Roll with i-frames that also works in mid-air**, passes under low gaps, and cancels attack recovery.
- Ladders (through floor holes), ledge grab and climb, one-way platforms (Down + Jump to drop), slopes up to 55 degrees, moving platforms.
- Traversal runes (off in the base kit): **wall jump** (kick between walls without sliding first), double jump. Air dash and wall climb are reserved.

**Combat (M2)**
- Two weapon slots, one button each (instant swap). Data-driven combos with input buffering: Bellringer's Sword (3-hit), Rusted Dagger (fast 3-hit), Tower Shield.
- **Timed parry and block**: tap the shield for a parry window (negates damage, stuns the attacker, bonus damage on stunned enemies, reflects projectiles); hold to block.
- Enemies with a consistent telegraph language (a danger box that fills exactly as the hit lands): a melee Drowned Grunt and a ranged Drowned Spitter, with poise, knockback and a clean state machine.
- Game feel: hitstop, Cinemachine screen shake, hit sparks, slash trails, damage numbers (all with accessibility sliders), training dummy with a DPS readout.

**Procedural levels (M3)**
- Seeded generation of interconnected rooms on a grid: a main path from Start to Exit that moves right and sometimes up or down, plus dead-end branches ending in Treasure rooms.
- **Shareable seeds** (e.g. `3J7-XQS0`, or any text). The same seed gives the same layout and enemy placement.
- Every connection is verified passable with the base movement kit. Difficulty ramps toward the exit.
- Run flow: permadeath, end screens, retry the same seed or start a new one, fog-of-war minimap.

**Loot and builds (M4)**
- **Three scroll colours** (Ember, Brine, Moss) level up the weapons and skills that share their colour, and add health.
- **Rarity, tiers and affixes**: Common to Legendary items with random affixes (crit, attack speed, on-hit burn / poison / bleed / slow / stun, cooldowns, health ...), upgradable tiers, and **cursed items** that trade a bad affix for extra power.
- **Gear slots**: one amulet and one outfit, picked up from chests and shops. **Two skill slots** (grenades, a turret, traps) with cooldowns. **Mutations**: three passive perks chosen at altars (heal on kill, parry riposte, momentum, a second wind ...).
- **Status effects** (burn, poison, bleed, slow, stun, weaken), a **health flask**, a **merchant** in every level, treasure chests, and gold and Cells drops.
- A full **uGUI HUD** and menus with keyboard, mouse and gamepad navigation. Permadeath resets everything.

**Menus, saves and settings (M5)**
- **Main menu** over an animated painted backdrop: Continue (with a summary of the saved run), New Game, New Game from Seed, Settings, Controls, Quit. **Pause menu** on `Esc` / Start.
- **Saves**: the run autosaves on every room change and when you save and quit. A save stores the seed plus your progress (health, items, mutations, flask, cleared rooms, opened chests, altars, shop stock); the level is rebuilt from the seed. Writes are atomic with a `.bak` fallback. Dying or winning deletes the run (permadeath).
- **Settings** (saved as JSON): resolution, display mode, vsync, quality; master, music, sfx, ui and ambient volumes (applied when audio arrives); screen shake, hitstop and flash intensity, damage numbers, toggle block, and an assist mode that takes 40% less damage.
- **Rebindable controls** for keyboard and mouse and for gamepad, with conflict handling and reset to defaults.

**Eight enemy types (M5)**, each a different problem to solve, all with the same telegraph language:

| Enemy | Archetype | How to beat it |
|---|---|---|
| Drowned Grunt | Melee | Parry the swing or roll through it |
| Drowned Spitter | Ranged | Close in or reflect the bolt |
| Bell-Wisp | Flyer | Hovers above you, shows a dive line that tracks you and then locks; roll out of the line |
| Shell Guard | Shield bearer | A frontal tower shield soaks hits; a heavy blow or a parry breaks it for a few seconds, and hits from behind do extra damage |
| Tideboar | Charger | Paws the ground, then charges the whole lane; jump or roll through it and it stuns itself on a wall |
| Reliquary Shade | Summoner | Keeps its distance and calls Drowned Wretches; kill it first and they die with it |
| Bloat | Exploder | Swells and bursts (a growing circle shows the blast); also bursts when killed, so pop it from range |
| Eel Archer | Sniper | A laser line tracks you, locks, then a fast bolt follows it; break the line or parry the bolt |

Later enemies appear deeper in a level (`minProgress`), with per-room caps, so early rooms stay readable.

**Art (M5)**
- Hand-built **painting pipeline**: every character, prop and tile is drawn by code (shaded, outlined, rim-lit) into sprite sheets with a swap-in `CharacterArt` asset, so final art can replace any of it.
- Animated characters (idle, run, jump, fall, roll, block, attack, hurt; enemies telegraph, attack and flinch in sync with their AI timing).
- **Ember Catacombs biome**: tiled stone, parallax backdrop (windows, colonnade, hanging banners), 2D lighting (ambient, a lantern on the player, flickering torches and braziers) and post-processing (bloom, vignette, grading, film grain). Painted exit portal, chest, altar and merchant. An ornamental UI skin.

**Difficulty, two worlds and bosses**
- **Pick a difficulty** when you start a new game: Story, Normal (the default, already tougher than the first versions), Hard and Nightmare. They change enemy health, damage, speed, how long warnings last, how many enemies fill a room and how much gold drops. Enemies also get tougher deeper in a level and in the second world.
- **Two worlds**: the **Ember Catacombs** (cool stone, torchlight) and the **Ashen Promenade** (rust-brown ruins under a burning sunset). A run is both, one after the other, and you keep everything you carry between them.
- **A boss at the end of each world.** The room seals behind you; beat it to open the door and the exit portal.
  - **The Hag of Bells** (Catacombs) floats and scatters magic around the room: a ring of bolts thrown every which way, hex circles erupting all over the floor, slow homing orbs you can parry back, a blink beside you for a melee sweep, and in the last phase summoned wretches.
  - **The Rust Warden** (Promenade) is a towering knight with a greatsword whose blows leave danger areas: an overhead slam with floor-running shockwaves, a huge sweep you can parry, a leap that lands in a blast, a line of eruptions marching toward you, and a sweep-into-slam combo.
  - Both get faster and add moves as their health drops (phase ticks on the boss bar), take extra damage when stunned by a parry, and pay out gold, Cells and a good item.
- **Dotted warnings**: every attack shows a dotted outline (a box, a line or a ring) that lights up exactly as the hit lands.
- The world is carved into solid rock, so rooms are never slabs floating in the void.

**Depth and gear**
- **Parallax in every world.** Several background planes drift at different speeds (haze, colonnades or ruined towers, hanging lanterns or floating ruins), and two **foreground planes** (rubble, roots and chains in the Catacombs; weeds, broken posts and torn banners in the Promenade) slide past in front of you faster than the world, along with drifting dust or embers at several depths.
- **Torches stand on stone posts** instead of hanging in the air.
- **Weapons are painted and visible.** You see the sword, dagger, hammer or boathook you are holding (it swings with the attack animation), and the shield you carry rides on your back until you raise it. Swap a weapon at a chest and the sprite changes. The same art shows in the HUD slots, item cards, shop rows and as items on the ground. Weapons are European arms: arming sword, rondel dagger, warhammer, halberd-style boathook, heater/kite shield and round buckler. Enemies carry matching flanged maces, nailed clubs, bows and shields.

**Two Souls: loadouts, new foes, sound and depth**
- **Practice room and loadout.** A new game starts in a quiet practice hall with two training dummies. Pick **Bow**, **Dual Swords**, **Greatsword** or **Sword & Shield** (click, 1-4, or the shoulder buttons), try it, then walk into the portal to begin. It appears once per New Game; Continue skips it.
- **New weapons.** The Hunter's Bow (arrows, a piercing heavy shot), Twin Blades (a sword in each hand) and the Greatsword join the loot pool too.
- **Each world has its own enemies.** The Ashen Promenade fields Ash Footmen, Ember Imps, Ember Wisps, Rust Sentinels, Cinder Boars, Ash Bombers, Bone Archers and Ash Priests (with Ash Husks), the same eight behaviours in new bodies and a little tougher.
- **The Hag lands.** She regularly dives at a marked spot, blasts the floor and stays on the ground for a few seconds, taking extra damage, before floating up again.
- **Sound for everything**, synthesised in code: movement, every weapon, hits, parries, enemies, bosses, items, doors, menus, plus ambient drones and torch crackle, each on its own volume slider (effects, interface, ambience).
- **Fonts and logo.** Cinzel Decorative and Almendra (both free OFL fonts) throughout, and a new TWO SOULS logo with two flickering soul flames.
- **More parallax**: mist planes, distant arches or broken columns, and heavy columns sweeping past in front, plus more drifting motes.
- **Fire on the posts** instead of torches in the air.
- **Touch layout editor** (Settings > Touch): move and resize every on-screen button, set their opacity, reset.
- **Telegraphs** are animated dots: melee swings are one dotted arc, shots a dotted line with an arrowhead, blasts a ring.
- Fixed: the tutorial's blue barrier after the chest, and the boss health bar lingering after you die and replay.

**Browser, phone and controller support**
- **Plays in a browser** (WebGL 2) on desktop and phones, hostable for free on Vercel; progress is saved locally in the browser. See [docs/WEB.md](docs/WEB.md).
- **Touch controls** (stick and action buttons) appear automatically on phones and tablets; they drive a virtual gamepad so every binding and menu works.
- **Haptics**: controller rumble on hits, parries, explosions and death; phone vibration on Android browsers (iPhone Safari has no vibration API). Strength and an off switch are in Settings.
- Prompts and the tutorial name the right controls for whatever you are using (keyboard, gamepad or touch).
- **Tutorial**: ten guided stations (move, jump, roll, attack, parry, ledges, loot, skills, mutations, exit). Offered on your first New Game; always in the main menu.
- Levels are checked against the real jump physics: every generated level can be finished with jumping alone (no roll, wall jump or double jump needed).

## Requirements

- **Unity 6000.5.6f1** (Unity 6), Universal Render Pipeline with the 2D Renderer
- Packages (already in `Packages/manifest.json`): Input System, Cinemachine 3, 2D Animation, Addressables, Test Framework, uGUI

## Getting started

1. Install Unity **6000.5.6f1** via Unity Hub.
2. Clone this repo and open the folder as a project in Unity Hub.
3. Open one of the scenes in `Assets/Scenes/` and press Play:

| Scene | What it is |
|---|---|
| `MainMenu.unity` | **Start here** (scene 0 in the build). New Game and Continue load `M4_LootRun` |
| `Tutorial.unity` | The guided tutorial hall (also reachable from the main menu) |
| `M4_LootRun.unity` | The game: a generated run in the painted Ember Catacombs with loot, a merchant, mutation altars, skills and the full HUD. You can also open it directly and press Play |
| `M3_Run.unity` | A generated run without loot: explore, fight, reach the exit |
| `M2_CombatArena.unity` | Combat sandbox: dummy, grunt, spitter, weapon swapping |
| `M1_Graybox.unity` | Movement course: slopes, ladders, ledges, roll tunnel, wall-jump shaft, moving platforms |

The scenes can be rebuilt from the menu `Roughlike > Build` (hand-tuned data assets are kept). Art is regenerated from `Roughlike > Art > Generate Characters` and `Generate Environment`.

## Controls

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | WASD / arrows | Left stick / D-pad |
| Jump | Space / C | A |
| Roll (also in the air) | L / Left Shift | B / right shoulder |
| Weapon slot 1 (sword) | J / left mouse | X |
| Weapon slot 2 (shield: tap = parry, hold = block) | K / right mouse | Y |
| Skill 1 / Skill 2 | U / I | Left / right trigger |
| Interact (pick up, open, buy) | E | Left shoulder |
| Health flask | Q | Right stick click |
| Climb ladder / use exit door | Up / Down | Up / Down |
| Drop through a one-way platform | Down + Jump | Down + A |
| Wall jump (needs the rune) | Hold toward a wall, tap Jump | same |

All of these can be changed in **Controls** (main or pause menu). Gamepad bindings are implemented but have not been tested on hardware yet.

**Dev keys**
- M1: `F1` hide HUD, `F2` double jump, `F3` wall jump, `R` reset.
- M2: `F4`/`F5` cycle weapons, `F6` spawn grunt, `F7` spawn spitter, `F8` god mode.
- M3/M4: `F9` new random run, `F10` retry this seed, `F11` seed box, `F12` reveal the whole map.
- Menus: stick / D-pad / arrows to move, South / Enter to confirm, East / Esc to cancel. `Esc` / Start pauses during a run; `M` returns to the menu from the end screen.

## Seeds

Every run has a seed shown at the top of the screen. Press `F11` to type one in. Valid codes decode exactly; any other text (like `drowned bells`) is hashed into a seed, so words work too. Same seed, same level.

## Project layout

```
Assets/
  Scripts/
    Core/      layers, seeded RNG, seed codes, event bus, stat sheet, kinematic motor, save service, settings
    Player/    movement controller, input and rebinding, camera target, animation selection
    Combat/    health and hit filters, weapons, shield/parry, feedback (hitstop, shake, VFX), player combat animation
    Enemies/   enemy definitions, the AI state machine and one behaviour per archetype, factory, animation driver
    Items/     rarity, affixes, loot roller, inventory, player stats, skills, pickups, shops, altars
    Rooms/     level generator, room builder, run manager and saves, run HUD, atmosphere (lights, post, parallax, decor)
    UI/        uGUI HUD, menus (main, pause, settings, controls), skin, shop / altar / replace screens
    Utils/     object pool, procedural sprites, CharacterArt / EnvironmentArt, SpriteAnimator
    Editor/    scene and asset builders; Editor/Art holds the painting pipeline
  Art/         generated sprite sheets (Characters, Environment) and the art assets that point at them
  Data/        ScriptableObjects: movement config, weapons, skills, gear, affixes, mutations, enemies, biomes
  Tests/       EditMode and PlayMode tests
  Scenes/      MainMenu, M4_LootRun (the game) and the M1 to M3 test scenes
DESIGN.md      pillars, decisions and architecture
PROGRESS.md    milestone log with how-to-test checklists
ART_TODO.md    every placeholder asset, with sizes and style notes
```

Modules are separate assemblies (`Roughlike.*`) with a one-way dependency direction (Utils, Core, Player, Combat, Enemies, Items, Rooms, UI) and communicate through a typed event bus. Everything tunable lives in ScriptableObjects or the Inspector. See [DESIGN.md](DESIGN.md).

## Tests

173 PlayMode tests and 38 EditMode tests cover movement feel (jump height, roll distance, coyote time, wall-jump chains), combat (combos, parry, block, i-frames, telegraph timing), each of the eight enemy archetypes (dive, shield break, wall stun, summoning, explosions, sniper lock), level generation (determinism and structural validity across 1000 seeds, spawn rules), loot, status effects, skills, mutations, run flow, saves and loading, settings and rebinding, the menus, sprite animation, that painted levels have exactly the same solid geometry as graybox ones, that every generated level is traversable, the menu-stacking regression, haptics, touch controls, the tutorial flow, difficulty scaling, both bosses, multi-biome runs and saves, dotted telegraphs and the swap-drop fix.

Run them in the Unity Test Runner (`Window > General > Test Runner`), or from the command line with the Unity CLI:

```bash
unity command run_tests --mode playmode --async_tests
```

## Art

There is no hand-drawn art in the repo: everything you see is **painted by code** with a small rasteriser (`Assets/Scripts/Editor/Art/`):

- `Painter` draws capsules, ellipses and polygons back to front with three-tone cel shading lit from the upper left, a rim light, an edge line and a thick dark outline, 2x oversampled.
- `HumanoidArt` (pose-driven two-legged characters with IK limbs), `CreatureArt` (wisp, boar, bloat, shield prop), `PropArt` and `WorldPropArt` (torches, door, chest, altar, merchant) and `EnvironmentPainter` (stone tiles, parallax layers) describe the look; `CharacterLibrary` lists every character, palette and animation.
- `Roughlike > Art > Generate Characters` and `Generate Environment` write the sheets into `Assets/Art/` and the `CharacterArt` / `EnvironmentArt` assets that reference them. Characters, enemies, tiles and the backdrop all load from those assets, so **final art replaces any of it by swapping a sheet or an asset field; no code changes**.
- Playback is `SpriteAnimator` (loop, one-shot, or scrubbed by progress, which is how enemy telegraph poses stay locked to their timing). Lighting and post-processing come from the biome's `EnvironmentArt`.

Not final: it is a coherent, readable stand-in rather than Hades-quality illustration. [ART_TODO.md](ART_TODO.md) has the style guide and lists what still needs real art (item icons, the HUD, VFX flipbooks, a hit-flash and outline shader).

## Roadmap

| Milestone | Status |
|---|---|
| M0 Setup | done |
| M1 Movement prototype | done |
| M2 Combat prototype | done |
| M3 Level generation | done |
| M4 Loot and stats: scrolls, rarity and affixes, skills, mutations, amulets and outfits, shops, gold | done |
| Main menu, pause menu, settings, rebindable controls, run saves | done |
| M5 Hades-style visual slice (biome 1), animation, and more enemy types | done (awaiting review) |
| M6 Content expansion, special rooms, challenge doors | next |
| M7 Boss and a complete first biome | planned |
| M8 Meta-progression: hub, Cells, blueprints, runes, save/load, story dialogue | planned |
| M9 Biomes 2 and 3, branching paths, difficulty tiers | planned |
| M10 Polish: audio, UI, accessibility, performance, build pipeline | planned |

## Design pillars

1. Movement and combat feel first.
2. Speed and flow: death resets the run, unlocks carry over.
3. Build variety through loot.
4. A Hades-style visual identity.
5. Readability: telegraphs, hazards and pickups stand out.

Singleplayer only. Target: Windows PC, 60 FPS minimum, 1920x1080, gamepad as a first-class input.
