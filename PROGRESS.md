# PROGRESS

## M0 — Setup (approved)
- Unity 6000.5.6f1 + `com.unity.pipeline` driven through the `unity` CLI (`unity command <tool>`); Claude Desktop MCP configured.
- Removed: `com.coplaydev.unity-mcp`, `visualscripting`, `multiplayer.center`, `ai.assistant`, Terrain / TerrainPhysics / XR / Vehicles / Wind / Cloth / Umbra modules.
- Folder structure, per-module asmdefs (`Roughlike.*`), DESIGN.md, PROGRESS.md, ART_TODO.md.
- Decisions: roll works mid-air; amulet/outfit slots with pickup items (planned M4); no content restrictions; target 1920x1080, GTX 1060-class GPU, 60 FPS min; art quality and smooth mechanics are the priority.

## M1 — Movement prototype (awaiting review)
Built and verified (18/18 PlayMode tests pass, console clean):
- **Custom kinematic motor** (`KinematicMotor2D`): raycast-based, no Rigidbody solver. Chosen over a dynamic Rigidbody because the result is deterministic, never tunnels or sticks on seams/slopes, gives exact control over jump height, roll distance and i-frames, and ticks identically in tests. Handles slopes up to 55 degrees (climb, descend, crest snapping), one-way platforms, ceilings, walls.
- **PlayerController** state machine: Grounded, Airborne, Rolling, Ladder, LedgeHang, LedgeClimb, WallSlide.
  - Run with separate accel / decel / turn-around rates, air control.
  - Variable-height jump, apex hang, fast-fall, coyote time (0.1s), jump buffer (0.12s), roll buffer.
  - **Roll**: 0.45s, 5.2 units, speed curve, i-frames 0.03-0.38s, collider shrinks to 0.8 so it passes under 1.2-high gaps, crawls back out if wedged, works mid-air (1 per airtime by default, refunded on landing/ladder/ledge/wall), jump cancels a ground roll and keeps momentum. Passes through enemies (`PassesThroughEnemies`, `IsInvulnerable`).
  - Ladders (including through floor holes capped with a one-way platform), ledge grab and climb, one-way platforms with Down+Jump drop-through, moving platforms (riders carried).
  - Rune abilities behind flags (off in base kit): Double Jump and Wall Jump. Air Dash and Wall Climb are reserved for M8.
  - **Wall jump (reworked after playtest feedback):** probe-based wall contact (works whether or not you are pushing into the wall), wall coyote time (0.12s) so a jump just before or after touching still kicks, no need to slide first (works while rising or falling), short input lock so the kick carries you off, brief stick time on the wall, and a same-wall lockout so chains alternate walls. A rune shrine in the M1 course grants it (no debug key needed); `F3` still toggles it.
- **Input**: `PlayerInputReader` (keyboard + gamepad, built in code, rebindable later) behind `IIntentSource`; `ScriptedIntentSource` for tests.
- **Camera**: Cinemachine 3 (`CinemachinePositionComposer`) following `PlayerCameraTarget` (look-ahead, fall look-down, damping).
- **Presentation**: `PlayerVisual` (flip, squash and stretch, roll spin, breathing), placeholder capsule sprite with outline and cel shading.
- **Hooks for later systems**: controller events (Jumped, Landed(impact), RollStarted/Ended, LedgeGrabbed, LadderEntered, StateChanged...).
- **Tuning**: everything is in `Assets/Data/Player/PlayerMovement.asset` (`MovementConfig`).
- **Tools**: menu `Roughlike/Build/M1 Graybox Scene` rebuilds the test level, config-safe. `Roughlike/Setup/*` creates layers and placeholder sprites.
- Not in M1 on purpose: ground pound / downward strike (weapon-dependent, M2), air dash and wall climb (runes, M8).

### How to test M1
1. Open `Assets/Scenes/M1_Graybox.unity` and press Play (Game view focused).
2. Controls: move WASD/arrows/left stick, jump Space/C/A, roll Shift/L/B/RB, Down+Jump drops through one-way platforms, Up/Down uses ladders.
3. Debug: F1 hides the HUD, F2 toggles Double Jump, F3 toggles Wall Jump, R resets.
4. Walk the level left to right; signs mark each test: slopes (30 degrees up/down), one-way platforms, ladder through a floor hole, ledges (jump toward a wall, Up/Jump climbs, Down drops), roll under the low ceiling, wall-jump shaft (press F3 first), moving platforms over the pit, elevator. Falling below -20 respawns you.
5. Tune live: select `PlayerMovement` and edit values in Play Mode (keep or revert after).
6. Tests: Window > General > Test Runner > PlayMode > run all (18 movement tests), or `unity command run_tests --mode playmode`.

### Feel notes / open decision
- Air rolls default to 1 per airtime (`airRollsPerAirtime`). Set higher or to a large number for unlimited.
- Gamepad was not tested on hardware here.

## M2 — Combat prototype (awaiting review)
Built and verified (34/34 PlayMode tests pass: 18 movement + 16 combat; console clean; arena scene smoke-tested live in Play Mode with injected input).

**Architecture**
- `Roughlike.Combat`: `Health` + `IHitFilter` chain (i-frames first, then shield), `DamageInfo` (all hit data in one struct), `HitReport`/`HitEvent` on a new `EventBus<T>` (Core) so feedback/audio/UI never get called directly by combat code.
- Weapons are data: `WeaponData` ScriptableObject (combo `AttackStep[]` with windup/active/recovery/chain/hitbox/knockback/stagger/hitstop/shake/lunge, plus `ShieldSettings`). No per-weapon code.
- `KinematicMotor2D` moved to `Roughlike.Core` so enemies reuse it. Combat references Player; Enemies reference Combat/Core.

**Player**
- Two weapon slots, each with its own button (Dead Cells style; pressing the other slot's button is the instant swap). J / LMB / Gamepad X = slot 1, K / RMB / Gamepad Y = slot 2. Default: Bellringer's Sword + Tower Shield.
- Data-driven combos with input buffering (0.25s), chain windows, lunge, movement slow while attacking, facing lock during windup/active. Hits once per target per swing.
- Shield: tap = parry window (0.18s, from the front only), hold = block (85% reduction, small pushback). Perfect parry: no damage, attacker stunned 1.2s, 0.35s invulnerability, stunned enemies take 1.5x damage, projectiles are reflected. Unblockable / unparryable tags supported.
- Roll cancels any attack or shield (and keeps its i-frames). Getting hit: knockback, 0.2s control lock, 0.8s invulnerability, attack interrupted.

**Enemies** (`EnemyDefinition` assets, shared `EnemyController` state machine: patrol, chase, telegraph, attack, recover, stagger, dead)
- Drowned Grunt (melee): 0.55s telegraph, 0.65s punish window.
- Drowned Spitter (ranged): keeps a distance band, 0.7s telegraph with aim line, projectile (pooled).
- Poise/stagger accumulation, knockback, ledge and wall awareness, sight cone + line of sight, being hit alerts them.
- Training Dummy: shows last hit, total and DPS.

**Game feel and telegraphs**
- `CombatFeedbackHub` (one per scene): hitstop, Cinemachine impulse screen shake, hit sparks, slash crescents, pooled damage numbers (toggleable). Sliders for hitstop, shake and flash for accessibility.
- Consistent telegraph language: outlined danger box that fills toward the far edge and finishes exactly when the hit lands, plus a pulsing body tint (readable without colour).
- Hit flash, squash on hit, i-frame blink, enemy world health bars.

**Tools**: `Roughlike/Build/M2 Combat Arena` rebuilds the scene (keeps hand-tuned assets). In-game: F4/F5 cycle slot weapons (sword, dagger, shield), F6 spawn grunt, F7 spawn spitter, F8 god mode. Dying respawns you at the start (placeholder for the run loop in M3).

### How to test M2
1. Open `Assets/Scenes/M2_CombatArena.unity`, press Play.
2. Dummy (x=9): hit it with J, mash for the 3-hit combo; watch dps readout. F4 to swap to the dagger and compare.
3. Grunt (x=20): watch the orange telegraph box fill. Tap K just before it finishes to parry (gold PARRY!, grunt stunned, hit it for bonus damage). Hold K to block, or roll (Shift/L) through the swing.
4. Spitter (x=46): roll its shots, block them, or parry to reflect.
5. Try rolling out of a sword swing recovery, attacking in mid-air, and getting hit mid-combo.
6. Tests: Test Runner > PlayMode (34 tests), or `unity command run_tests --mode playmode --async_tests`.

### Notes and open decisions
- Downward strike / ground pound is deferred (weapon-dependent; adds to the loot pool in M6).
- Gamepad bindings are untested on hardware. Hit numbers (damage, hitstop, shake) are first-pass values for tuning.
- Placeholder VFX are procedural (see ART_TODO.md); real flipbooks and a shader-based flash come in M5.

## M3 — Level generation (awaiting review)
Built and verified: 44 PlayMode + 9 EditMode tests pass (including a 1000-seed structural validation of the generator and a physical door/shaft passability check on 30 built levels); generated run smoke-tested live in Play Mode.

**Generation**
- `SeededRng` (SplitMix64, with `Fork` for independent sub-streams) and `SeedCode` (shareable Crockford base-32 codes like `3J7-XQS0`; any text is accepted and hashed). Nothing uses `UnityEngine.Random`.
- `LevelGenerator` (pure logic): main path from Start to Exit on an 8x4 grid, moving right and sometimes up/down, 7-10 rooms, exit at least 5 columns away; 1-2 dead-end branches ending in Treasure rooms. Every room gets a variant (flat, blocks, one-way platforms, roll tunnel). Fully deterministic per seed.
- Rooms are 30x18 units with 6-unit side doors and 6-unit shafts. `ChunkBuilder` assembles each room from its openings: side doors, floor/ceiling holes, a stair of one-way platforms in rooms that open upward (rise <= 2.6, jump is 3.2). All connections are traversable with the base kit.
- `RoomChunk` + marker points (enemy spawns, player spawn, exit and loot anchors) are the hook for hand-authored chunk prefabs: author a prefab with that component and it can replace a procedural room.

**Run flow**
- `RunManager`: generates and builds the level, places the player, tracks kills, damage taken, rooms visited and time. Permadeath: dying ends the run; the end screen offers a new seed (R) or retry (T). Reaching the exit door (Up) completes the biome (placeholder until M9). Camera snaps on restart.
- Population: enemies per combat room scale from `minEnemies` to `maxEnemies` with distance along the path; placement is deterministic per seed. `BiomeData` holds generator settings, the enemy pool and the graybox palette.
- `RunHud`: seed, current room, stats, fog-of-war minimap of the room graph (F12 reveals everything: this is the level-generator visualiser), seed entry box (F11).

### How to test M3
1. Open `Assets/Scenes/M3_Run.unity` and press Play (a random run starts).
2. Explore right: side doors, rooms with platforms and blocks, a roll tunnel (gap 1.3, roll through it), and rooms with a stair of orange platforms leading up or a hole in the floor leading down. Dead-end branches have a TREASURE marker (loot arrives in M4).
3. Reach the glowing EXIT door and press Up. Die on purpose to see the death screen. R = new seed, T = same seed (the layout and enemies repeat exactly).
4. F11 enter a seed (try `drowned bells`), F12 reveal the minimap, F9/F10 new/retry at any time.
5. Tests: PlayMode (44) and EditMode (9) in the Test Runner.

### Notes
- Camera is not confined to rooms yet. Per-room camera bounds and room transitions come with the visual pass (M5).
- Doors do not lock yet; challenge doors, shops and secret rooms arrive in M6.

## M4 - Loot and stats (awaiting review)
Built and verified: 88 PlayMode + 25 EditMode tests pass (stats, loot rolling, status effects, skills, pickups, shops, mutations, UI menus, loot in generated levels); the M4 scene was smoke-tested live in Play Mode (HUD, item cards, shop, altar and replace menus driven through their buttons). Console clean.

**Stats and scaling**
- `StatSheet` (Core): base value, flat, additive % and multiplicative % modifiers, removed together by source; clamped to sane ranges. Stats: max health, damage dealt/taken, move speed, attack speed, crit chance/damage, gold find, health and gold per kill, parry window, skill cooldown rate.
- **Scroll stats** in three colours: **Ember** (red), **Brine** (cyan) and **Moss** (green). Each scroll is +1 level and +8 max health. Weapons and skills have a colour affinity and gain +12% damage per level in each of their colours (multi-colour items use every colour; colourless items scale weakly with all).
- `PlayerStats` feeds everything (scrolls, gear, mutations) into the sheet and pushes the result to health, movement and combat through `IPlayerModifiers`. Combat has no dependency on the item system.

**Items** (rarity Common/Rare/Epic/Legendary, tiers 1-4, affixes, curses)
- Data-driven ScriptableObjects, one source of truth in `DefaultContent` saved as assets by the builder: 6 weapons (dagger, sword, hammer, boathook reach weapon, tower shield, buckler), 4 skills, 6 amulets, 5 outfits, 24 affixes (damage, crit, on-hit burn/poison/bleed/slow/stun, cooldown, health, speed, gold, heal on kill ...) and 8 mutations.
- `LootRoller` is fully seeded. Rarity improves toward the exit, chests skew rare, and rare+ items can be **cursed** (a bad affix, one extra good affix and +20% power). Every roll is deterministic per seed.
- **Amulet and outfit slots**: pick-ups auto-equip and drop the replaced piece. Weapons and skills fill empty slots or open a **replace-slot menu**.

**Combat additions**: crits, attack speed, damage-taken scaling, parry-window scaling, and **status effects** (Burn, Poison, Bleed, Slow, Stun, Weaken) that tick through `StatusEffects`, bypass shields and i-frames, tint the sprite and credit kills.

**Skills** (two slots, cooldown bar on the HUD): Powder Keg (grenade, burn), Bell Turret (auto-fires for 9s), Snare Trap (stuns), Venom Flask (poison grenade). Damage scales with scrolls, rarity, tier and affixes.

**Mutations** (3 slots, chosen at altars): stat mutations plus Bloodletter (heal on kill), Counterstrike (2.5x swing after a perfect parry), Momentum (stacking damage on kills) and Second Wind (survive one lethal hit per run).

**World**
- Generated levels now have exactly one **merchant** (dead-end branch, or a main-path room as fallback), **treasure chests** with pre-rolled loot (seeded), and up to three **mutation altars** on the main path (at 25%, 50% and 75% progress). Enemies drop gold (and rare Cells); clearing a room can drop a scroll (seeded per room).
- **Shop**: 3 items (weapon, skill, gear), a scroll, a flask refill and two weapon upgrades. Cancelling the replace dialog refunds the gold.
- **Flask**: 2 charges, heals 45% of max health, refills in shops.
- **Run flow**: permadeath wipes gold, scrolls, mutations, gear and flask and restores the starting loadout (sword, shield, apprentice robes). The exit door banks your Cells; dying loses them.

**UI (uGUI, generated in code, controller-ready)**: HUD (health with damage lag, flask pips, mutations, gold and Cells, scroll levels, weapon and skill slots with rarity borders and cooldowns, interaction prompt with an item card, toasts); shop, mutation altar and replace-slot menus that pause the game, stack, and navigate with keyboard, mouse or gamepad (stick/D-pad, South confirm, East/Esc cancel).

### How to test M4
1. Open `Assets/Scenes/M4_LootRun.unity` and press Play.
2. Fight your way right. Enemies drop gold that flies to you; clear a room for a chance at a scroll (walk into it). Top-right shows gold, Cells and your Ember / Brine / Moss levels.
3. **Altar** (purple, glowing): press E, pick one of three mutations. **Chest** (dead-end rooms): E to open, then E on the item that drops; the card shows rarity, stats and curses (red).
4. **Shop** (a dead-end branch, marked SALVAGE): E to open, spend gold on items, scrolls, upgrades and a flask refill. Buy a weapon to see the replace-slot menu.
5. Skills on U and I, flask on Q. Weapons J/K as before.
6. Die on purpose: gold, scrolls, mutations and gear reset on the next run (R / T).
7. Tests: PlayMode (88) and EditMode (25) in the Test Runner.

### Notes
- Cells are counted per run and shown on the end screen ("banked" at the exit, "lost" on death); they are saved and spent in M8.
- Status effects Shock, Freeze and Oil-and-fire, and more weapon types (bows, whips, throwables, magic) arrive with M6.
- Item icons, the HUD and the menus are placeholder shapes and plain text; they get a full art pass in M5.

## M4.5 - Menus, saves and settings (awaiting review)
Built and verified in Play Mode and with tests: the main menu, pause menu, settings and controls screens, autosave and Continue.

- **Scenes**: `MainMenu.unity` is scene 0. New Game, New Game from Seed and Continue go through `GameSession` and load `M4_LootRun`. Build settings order: MainMenu, M4_LootRun, M3_Run, M2_CombatArena, M1_Graybox.
- **Saves** (`SaveService`, `RunSave`, `RunSaver`): atomic write (temp file then replace) with a `.bak` fallback when the main file is unreadable. A run save holds the seed, the room you are in and your progress; loading rebuilds the level from the seed and then restores state (cleared rooms, opened chests, used altars, shop stock, health, gold, Cells, scrolls, gear, weapons, skills, mutations, flask, stats). It autosaves on each room change and on save-and-quit, and is deleted on death or victory.
- **Settings** (`GameSettings`, `SettingsService`): video, audio buses, gameplay toggles (screen shake, hitstop, flash, damage numbers, toggle block, assist mode) saved as JSON; `SettingsApplier` pushes them to the game on change.
- **Controls**: `GameInput` is one shared, code-built action map with Keyboard and Mouse and Gamepad groups; all eight actions are rebindable in the Controls screen, with interactive capture, conflict swapping, and reset. Overrides are stored as plain (action, group, path) entries, not Input System IDs, because those change between sessions.
- Tests use a `[SetUpFixture]` that redirects saves to a temp folder, so running them never touches a real save.

### How to test M4.5
1. Open `Assets/Scenes/MainMenu.unity` and press Play. Use arrows / Enter or the mouse.
2. New Game, play a room or two, press `Esc`: Resume, Settings, Controls, Save and quit to menu.
3. The menu now shows Continue with your seed and progress. Continue puts you back in the same level, room, with the same items.
4. Settings: change a slider or toggle, quit to menu and reopen to see it persisted. Controls: select Jump, press a key, then try it in game; Reset restores defaults.
5. Die: Continue disappears (the save is gone).

## M5 - Eight enemy types and the art pass (awaiting review)
Built and verified: 122 PlayMode + 38 EditMode tests pass; the scene was played live (combat against every archetype, loot, shop, altar, menus). Console clean.

**Enemies**
- `EnemyController` keeps the shared state machine (patrol, chase, telegraph, attack, recover, stagger, dead), poise, knockback, perception and death. What differs lives in an `EnemyBehavior` strategy chosen from `EnemyDefinition.archetype`: `MeleeBehavior`, `RangedBehavior`, `FlyerBehavior`, `ShieldBehavior`, `ChargerBehavior`, `SummonerBehavior`, `ExploderBehavior`, `SniperBehavior`. A behaviour decides when to attack, what the telegraph looks like (box, rotated line, or circle) and what the attack does.
- New archetypes are mostly data: stat blocks live in `DefaultEnemies`, the editor builder saves them as assets, and a new enemy of an existing archetype is a new asset.
- Spawn rules: `minProgress` (only deeper in a level), `maxPerRoom`, `rewardMultiplier` (summoned Wretches drop nothing). Summoned minions register with the room's clear tracker, so a room is not clear while one lives.
- Tuning knobs per archetype are in `EnemyDefinition` (hover height and dive speed, shield reduction and break threshold, charge speed and wall stun, summon count and cap, explosion radius, sniper aim-lock fraction).

**Art** (see the README for how it works)
- Characters: the player (idle, run, jump, fall, roll, block, attack, hurt) and all nine enemies and minions, each with idle / walk / telegraph / attack / hurt. Enemy clips are driven by `EnemyAnimDriver`: the telegraph pose is scrubbed by telegraph progress, the attack by the attack timer.
- Environment: tiled stone walls, floors with a mossy lip and gold-inlaid ledges; three parallax layers; flickering torches and braziers; the player carries a warm lantern; bloom, vignette, grading and grain from a global volume.
- Props: exit portal, chest (closed and open), mutation altar, merchant, Shell Guard's separate shield that swaps to a cracked sprite when broken. The ornamental UI skin (`UiSkin`) restyles every panel and button, and the main menu uses the painted backdrop.
- Gameplay is unchanged by art: a test builds the same seeds with and without art and compares every solid collider.

### How to test M5
1. Play from `MainMenu.unity` (or open `M4_LootRun.unity`). The first rooms use the Drowned Grunt and Spitter; later rooms add the rest.
2. Watch for the telegraphs: a line from the Bell-Wisp, a long lane from the Tideboar, a growing circle on the Bloat, a laser on the Eel Archer. Each has a counter (see the README table).
3. Hit a Shell Guard from the front (blocked), from behind (extra damage), with a heavy swing or a parry (shield breaks).
4. `Roughlike > Art > Generate Characters` / `Generate Environment` regenerates the art; rebuild the scene with `Roughlike > Build > M4 Loot Run`.

### Notes
- Characters are posed, not hand-animated (limbs are driven by a pose function and IK), so motion reads clearly but is not final quality. Replace the sheets (same layout) with authored art when it exists.
- Still placeholder: item icons and dropped-item visuals, the HUD widgets inside the new frames, VFX (hit sparks, slashes), and there is no custom shader yet (hit flash is a colour lerp; outlines are baked into the sprites).
- `Assets/Screenshots` is ignored by git; the README images live in `docs/images`.

## M5.1 - Bug fixes, traversal rules, web build, touch, haptics, tutorial (awaiting review)
**Fixed**
- **Shop and altar menus not closing.** Cause: while paused the player controller returned early without refreshing its input, so a stale "Interact pressed" made the interaction sensor re-open the menu every frame (a stack of duplicates; closing one revealed the next). The controller now clears its intent when paused, the sensor ignores paused frames, and closing a menu swallows gameplay input for three frames so A or B does not also jump or roll.
- **Impossible parkour.** `TraversalValidator` links every standable surface in a built level with walk, jump and drop edges using the real movement config (with safety margins) and sweeps the player's body along each arc against the real colliders; it then searches from the spawn to the exit, chests, merchant and altars. Rolls, wall jumps and double jumps are not counted. It found and we fixed: 6-unit gaps in the shaft stairs (now overlapping diagonal steps), a roll tunnel that sealed the room (now an optional low block with a walkable top), and chests, merchants and enemies placed over floor holes. A test checks 60 seeds and proves the validator rejects an impossible level.
- **Controllers.** The run-over panel and minimap were editor-only IMGUI and keyboard-only (R/T/M); they are now uGUI (`EndScreenView`, `MinimapView`) with real buttons for mouse, gamepad and touch, and exist in release builds. Prompts and menu hints follow the active device.

**Added**
- **Haptics** (`Haptics`): gamepad rumble and phone vibration, driven by combat events; settings for on/off and strength.
- **Touch controls** (`TouchControls`): on-screen stick and buttons via the Input System's on-screen controls (virtual gamepad); automatic on touch devices; Settings > Gameplay > Touch controls.
- **Tutorial** (`TutorialDirector`, `TutorialHud`, `Tutorial.unity`): ten stations, gates, device-aware text, safe health, skip from the pause menu, offered on first New Game, "Tutorial" in the main menu.
- **Web build** (`WebBuild`, custom page template, `Roughlike.jslib`, `WebBridge`): Roughlike > Build > WebGL; Brotli, hashed files, `vercel.json`; saves flushed to IndexedDB after each write; browser-friendly settings (fullscreen toggle instead of resolution; no Quit button). `tools/serve-web.py` serves it locally with the right headers; `docs/WEB.md` has the Vercel steps.

### How to test M5.1
1. Altars and shops: open one, pick or leave; it closes, once. Also with a gamepad (A / B).
2. Tutorial: main menu > Tutorial, or New Game on a fresh profile.
3. Touch: Settings > Gameplay > Touch controls > Always on.
4. Web: build, `python tools/serve-web.py`, open <http://localhost:8080> (verified loading, playing and saving in a browser here).
5. Rumble: plug in a controller and get hit.

### Notes
- The traversal check is conservative but not a full physics bot; a real playthrough is still the final word.
- iPhone: no vibration API in Safari (touch tick only). Touch layout was verified in the editor and in a desktop browser; it has not been tried on a physical phone.

## M5.2 - Difficulty, a second biome, bosses, grounded world, polish (awaiting review)
**Fixed**
- **Chest weapon "stuck".** Swapping a weapon dropped the old one on the exact spot of the new one, so it became the nearest thing to interact with and the next press swapped them straight back. Replaced items now land 2.6 units to the side and cannot be grabbed for 1.2 seconds.
- **Settings menu layout.** The Gameplay tab had grown past the window and hid rows behind the buttons. The rows now live in a scrolling list with the focused row kept in view (gamepad and keyboard), shorter labels, and a darker backdrop on the title screen.
- **Floating world.** `TerrainFill` packs dark rock into every empty grid cell around and between rooms, so floors read as ground.

**Added**
- **Haptics** for jump and attack (hit and parry already existed).
- **Dotted telegraphs** (`TelegraphIndicator`): perimeter dots for boxes, a single dotted line for lasers, a dotted ring with an inner sweep for explosions; they light from the attacker's side and flash white at the moment of impact.
- **Difficulty** (`DifficultyService`, `EnemyScaling`): Story, Normal, Hard, Nightmare, chosen in a picker on New Game (remembered, stored in the run save). Scales enemy health, damage, speed, telegraph time, cooldown, extra enemies per room and gold. Depth in the level and the biome add more. The default is tougher than before and rooms hold more enemies.
- **Multi-biome runs**: `RunManager` takes a list of biomes; the boss portal advances to the next one (keeping gear, healing 40%); saves store level index and difficulty; the last biome ends the run.
- **Ashen Promenade**: second palette (rust basalt, orange lichen), sunset sky with a huge sun, ruined towers and gates, beams and chains, warm lighting.
- **Bosses** (`BossController`, `BossBrain`, `WitchBrain`, `WardenBrain`, `BossEncounter`, `BossHealthBar`): attack coroutines with dotted telegraphs, phases at 66% and 33%, stun on parry, a sealed arena, a camera that frames the whole room, a health bar with phase ticks, big payout. Art: `HagOfBells` and `RustWarden`, painted at 1.7x.
- The boss room is always a flat dead end entered from the side (`GeneratorSettings.horizontalFinish`).

### How to test M5.2
1. New Game, pick a difficulty (try Nightmare for a shock). Compare the number of enemies and how fast they strike.
2. Open a shop chest weapon swap: the old weapon lands beside you and waits a moment.
3. Settings > Gameplay scrolls with the stick or arrow keys.
4. Reach the end of the first world: the room seals, the Hag fights, the portal takes you to the second world, then the Warden.
5. Dev shortcut while playing: nothing needed; or use the probes in `AgentScripts` (not committed).

### Notes
- Boss numbers are a first pass (Hag 1000 health, Warden 1400, before difficulty); expect tuning after real play.
- The second world reuses the first world's enemy roster; its own bestiary is the natural next step.

## M5.3 - Depth, grounded torches, weapon art (awaiting review)
**Added**
- **Parallax planes**: `BackdropLayer.follow` may now be negative (a foreground plane that moves against the camera faster than the world). Both biomes gained a mid-distance plane (lanterns / floating ruins) and two foreground planes (bottom rubble or weeds, top roots or banners, drawn above the action). `ParallaxBackdrop` also spawns drifting motes (dust, embers) at several depths.
- **Torch posts**: `RoomDecor` builds a stone post from the floor to each wall torch.
- **Weapon art**: `WeaponArt` paints European arms with the cel painter (one routine for item sprites and for weapons baked into enemy sheets). `WeaponArtBuilder` writes `Assets/Art/Weapons/*.png` and assigns `WeaponData.sprite` / `heldScale` (it runs as part of the item content build).
- **Held gear**: the player sheet no longer has a sword baked in. It stores per-frame `Anchor`s (hand position, blade angle, shield pose); `HeldGear` draws the equipped melee weapon and shield from those anchors, so every weapon follows every pose. A lowered shield is carried on the back.
- **Icons**: `UiKit.FillIcon` shows the weapon sprite (blades laid diagonally) in HUD slots, item cards, shop rows; dropped items use the art too.
- Enemies: the Shell Guard carries a heater shield, the Eel Archer's bow is redrawn as a recurve with a grip and nocked arrow, daggers, maces and clubs are redrawn.

### How to test M5.3
1. Walk through a level: far planes barely move, the rubble/roots in front slide past quickly, motes drift.
2. Look at the torches: they stand on posts.
3. Swing the sword (J): the blade follows the animation. Hold K to block: the shield comes up in front. Swap a weapon at a chest and watch the sprite change.
4. Check the HUD slots, a chest item card and the shop list for weapon icons.

### Notes
- Weapon art is generated like all other art (see ART_TODO); final art replaces the PNGs and keeps the pivot at the grip (shield: centre).
- Only the starting weapons are painted; a new `WeaponData` without a sprite falls back to the old placeholder shapes and shows nothing in the hand.

## Next
M6 - content expansion: more weapons, special rooms and challenge doors, more mutations and status effects.
