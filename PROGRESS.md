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
  - Rune abilities behind flags (off in base kit): Double Jump and Wall Slide/Wall Jump work now. Air Dash and Wall Climb are reserved for M8.
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

## Next
M3 — Level generation (chunk-based generator, seeded RNG, exits, doors, death and restart).
