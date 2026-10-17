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

## Next
M2 — Combat prototype (weapon swap, shield and parry, 3 weapon types, hitboxes, hitstop, training dummy, 2 enemies with telegraphs).
