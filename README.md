# The Drowned Reliquary

A fast, fluid **2D action-platformer roguelite** built in Unity 6. Gameplay follows *Dead Cells* (tight platforming, an invulnerable roll, two weapon slots, procedurally assembled levels, permadeath). The art direction targets *Hades* (bold outlines, saturated mythic palettes, painterly backdrops).

You are a bell-ringer's apprentice who died sealing a sunken cathedral-city. Each death returns you to a lantern-lit boathouse. The tone is melancholy but warm, with dry humour from a ferryman and an archivist.

> **Status: in active development. The game is a playable graybox with placeholder art.** Movement, combat and level generation are built and tested; the Hades-style visuals, loot, hub and story arrive in later milestones (see the [roadmap](#roadmap)).

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

## Requirements

- **Unity 6000.5.6f1** (Unity 6), Universal Render Pipeline with the 2D Renderer
- Packages (already in `Packages/manifest.json`): Input System, Cinemachine 3, 2D Animation, Addressables, Test Framework, uGUI

## Getting started

1. Install Unity **6000.5.6f1** via Unity Hub.
2. Clone this repo and open the folder as a project in Unity Hub.
3. Open one of the scenes in `Assets/Scenes/` and press Play:

| Scene | What it is |
|---|---|
| `M3_Run.unity` | **Start here.** A generated run: explore, fight, reach the exit |
| `M2_CombatArena.unity` | Combat sandbox: dummy, grunt, spitter, weapon swapping |
| `M1_Graybox.unity` | Movement course: slopes, ladders, ledges, roll tunnel, wall-jump shaft, moving platforms |

The scenes can be rebuilt from the menu `Roughlike > Build` (hand-tuned data assets are kept).

## Controls

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | WASD / arrows | Left stick / D-pad |
| Jump | Space / C | A |
| Roll (also in the air) | L / Left Shift | B / right shoulder |
| Weapon slot 1 (sword) | J / left mouse | X |
| Weapon slot 2 (shield: tap = parry, hold = block) | K / right mouse | Y |
| Climb ladder / use exit door | Up / Down | Up / Down |
| Drop through a one-way platform | Down + Jump | Down + A |
| Wall jump (needs the rune) | Hold toward a wall, tap Jump | same |

Gamepad bindings are implemented but have not been tested on hardware yet.

**Dev keys**
- M1: `F1` hide HUD, `F2` double jump, `F3` wall jump, `R` reset.
- M2: `F4`/`F5` cycle weapons, `F6` spawn grunt, `F7` spawn spitter, `F8` god mode.
- M3: `F9` new random run, `F10` retry this seed, `F11` seed box, `F12` reveal the whole map.

## Seeds

Every run has a seed shown at the top of the screen. Press `F11` to type one in. Valid codes decode exactly; any other text (like `drowned bells`) is hashed into a seed, so words work too. Same seed, same level.

## Project layout

```
Assets/
  Scripts/
    Core/      layers, seeded RNG, seed codes, event bus, the kinematic motor
    Player/    movement controller, input, camera target, presentation
    Combat/    health and hit filters, weapons, shield/parry, feedback (hitstop, shake, VFX)
    Enemies/   enemy definitions, AI state machine, factory, sandbox overlay
    Rooms/     level generator, room builder, run manager, run HUD
    Utils/     object pool, procedural sprites
    Editor/    scene and asset builders
  Data/        ScriptableObjects: movement config, weapons, enemies, biomes
  Tests/       EditMode and PlayMode tests
  Scenes/      M1, M2 and M3 scenes
DESIGN.md      pillars, decisions and architecture
PROGRESS.md    milestone log with how-to-test checklists
ART_TODO.md    every placeholder asset, with sizes and style notes
```

Modules are separate assemblies (`Roughlike.*`) with a one-way dependency direction (Utils, Core, Player, Combat, Enemies, Rooms) and communicate through a typed event bus. Everything tunable lives in ScriptableObjects or the Inspector. See [DESIGN.md](DESIGN.md).

## Tests

44 PlayMode tests and 9 EditMode tests cover movement feel (jump height, roll distance, coyote time, wall-jump chains), combat (combos, parry, block, i-frames, telegraph timing), level generation (determinism and structural validity across 1000 seeds) and run flow.

Run them in the Unity Test Runner (`Window > General > Test Runner`), or from the command line with the Unity CLI:

```bash
unity command run_tests --mode playmode --async_tests
```

## Art

All art is currently procedurally generated placeholders (a stylised vector look with thick outlines and cel shading); it is **not** the final Hades-style art. Every character, enemy, prop, tile and VFX sits behind a prefab, sprite or ScriptableObject slot so better art drops in without code changes. [ART_TODO.md](ART_TODO.md) lists each placeholder with dimensions and style notes.

## Roadmap

| Milestone | Status |
|---|---|
| M0 Setup | done |
| M1 Movement prototype | done |
| M2 Combat prototype | done |
| M3 Level generation | done |
| M4 Loot and stats: scrolls, rarity and affixes, skills, mutations, amulets and outfits, shops, gold | next |
| M5 Hades-style visual slice (biome 1) | planned |
| M6 Content expansion, special rooms, challenge doors | planned |
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
