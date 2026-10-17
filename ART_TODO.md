# ART_TODO

Placeholder art register. Each entry: asset, size, style notes, priority. Populated as milestones create placeholders.
All art sits behind prefab / Sprite Library / ScriptableObject slots so replacements need no code changes.

## Style guide (M5)
- Not pixel art; bilinear filtering. Characters are painted into 320x320 frames at 160 pixels per unit (feet 34 px up from the bottom, facing right); props 128x192 or 320x448.
- Thick dark outlines (about 3 px, ink colour `#120D1A`) on characters, enemies and props; thin edge lines between overlapping parts.
- Cel shading: three tone steps lit from the upper left (light, base, shade), shadows shifted toward cool purple, a warm rim light from behind, glowing flat colours for eyes, flames and magic.
- Backgrounds: layered parallax, softer and darker than the playfield; bloom, vignette, grading and film grain on top.
- Per-biome palette: Ember Catacombs is cool carved blue-grey stone with warm ember accents (torches, mortar seams); enemies are drowned teal, olive and violet with orange, yellow or cyan eyes so they stand out from the stone.
- Readability first: enemy silhouettes differ clearly (hunched club-bearer, hooded caster, floating bell, low boar, round bloat) and every telegraph colours the same way (orange box, violet for unparryable).

## How the generated art is made
`Roughlike > Art > Generate Characters` and `Generate Environment` (code in `Assets/Scripts/Editor/Art`). To replace any piece with authored art, keep the sheet layout (rows = clips, `CharacterArt` lists them) and overwrite the PNG, or point the `CharacterArt` / `EnvironmentArt` asset fields at new sprites.

| Asset | Size | Notes | Priority |
|-------|------|-------|----------|
| Player (`Assets/Art/Characters/BellRinger.png`) | 320x320 frames, pivot at the feet, 160 PPU | Painted and pose-animated (idle, run, jump, fall, roll, block, attack, hurt). Replace with a hand-animated or rigged bell-ringer apprentice; keep the clip names and the 1.6 unit height | Medium |
| Biome tiles (`Assets/Art/Environment/Wall.png`, `FloorTrim.png`, `Platform.png`) | 256x256, 256x64, 256x48 px, 128 PPU | Procedural stone, mossy lip and gold-inlaid ledge, tiled by sprite draw mode. Replace with authored tilesets or RuleTiles; the room geometry does not change | Medium |
| Ladder, one-way platform, moving platform visuals | n/a | Currently tinted squares; need ornate props per biome | Medium |
| Enemy sheets (`Assets/Art/Characters/*.png`: Drowned Grunt, Spitter, Wretch, Shell Guard, Reliquary Shade, Eel Archer, Bell-Wisp, Tideboar, Bloat) | 320x320 frames, pivot feet | Painted and pose-animated (idle, walk, telegraph, attack, hurt; the Shell Guard also has `shield` and `shield_broken` props). Replace with authored designs, keep the clip names. The training dummy and the old Placeholder sprites are still blocks | Medium |
| Combat VFX (slash crescent, hit spark, projectile, telegraph box) | runtime generated | `Utils/ProceduralSprites`. Replace with flipbook slashes, bold hit bursts, glowing projectile trails; keep the telegraph shape language | High |
| Hit flash / outline shader | n/a | Outlines are baked into the sprites; the hit flash is a colour lerp. A Shader Graph flash, dissolve and rim would be better | Medium |
| Backdrop layers (`Assets/Art/Environment/Backdrop*.png`) | 1280x768, 32 PPU, tile horizontally | Procedural windows, colonnade and hanging silhouettes for the Ember Catacombs. Biomes 2 and 3 need their own sets; keep door height 6 and shaft width 6 | Medium |
| Exit portal, chest, altar, merchant, torch, brazier (`WorldProps.png`, `Props.png`) | 320x448 and 128x192 frames | Painted set pieces. Replace with authored props; the merchant and altar could animate | Low |
| Item icons (weapons, skills, amulets, outfits) | 128x128 px, one per base item | Currently tinted shapes (`UiKit.FillIcon`). Rarity frames and glow come from the art pass | High |
| HUD, shop, altar and replace-slot menus | 1920x1080 reference | Panels and buttons use the generated ornamental skin (`UiSkin`) and a serif system font. HUD bars and slots, bold typography and glowing rarity colours still need the art pass; keep the layout and navigation | Medium |
| Mutation, status effect and scroll icons | 64x64 px | Coloured squares with letters. Need icons per mutation, status and scroll colour | Medium |
| Pickup props (chest, merchant, altar, scrolls, coins, Cells) | n/a | Shapes with labels. Need animated props | Medium |
| Skill entities (grenade, turret, trap) | n/a | Shapes. Need animated, readable props with explosion flipbooks | Medium |
| Player animation set | n/a | Idle, run, jump, fall, roll, block, attack, hurt exist. Missing: land, ladder, ledge hang/climb, wall slide (these reuse idle and fall), per-weapon attack animations | Medium |
