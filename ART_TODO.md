# ART_TODO

Placeholder art register. Each entry: asset, size, style notes, priority. Populated as milestones create placeholders.
All art sits behind prefab / Sprite Library / ScriptableObject slots so replacements need no code changes.

## Style guide (draft, finalized in M5)
- Not pixel art; bilinear filtering, high-res sprites (characters ~512px tall source).
- Thick variable-width dark outlines on characters, enemies, key props.
- Cel shading: 2-3 tone steps, sharp rim light, glowing emissive accents.
- Backgrounds: layered parallax, softer and desaturated; dark foreground framing and vignette.
- Per-biome palette: one dominant hue plus one contrasting accent.

| Asset | Size | Notes | Priority |
|-------|------|-------|----------|
| Player body (`Assets/Art/Placeholder/PlayerBody.png`) | 128x256 px, pivot bottom-centre, 160 PPU | Procedural capsule with outline. Replace with rigged bell-ringer apprentice (2D Animation); keep pivot at feet, 1.6 units tall | High |
| Graybox square (`Assets/Art/Placeholder/Square.png`) | 64x64 px | White tile tinted per surface. Replace with biome tilesets and RuleTiles | High |
| Ladder, one-way platform, moving platform visuals | n/a | Currently tinted squares; need ornate props per biome | Medium |
| Enemy sprites (`EnemyGrunt.png` 144x240, `EnemyShooter.png` 112x240, `Dummy.png`) | 160 PPU, pivot feet | Light base tinted per `EnemyDefinition`. Need real designs and rigs per archetype | High |
| Combat VFX (slash crescent, hit spark, projectile, telegraph box) | runtime generated | `Utils/ProceduralSprites`. Replace with flipbook slashes, bold hit bursts, glowing projectile trails; keep the telegraph shape language | High |
| Hit flash / outline shader | n/a | Currently a sprite colour lerp. Shader Graph hit flash + outline (M5) | Medium |
| Room geometry (floors, walls, platforms, backdrops) | 30x18 unit rooms | Tinted blocks from `BiomeData` palette. Replace with tilemaps/RuleTiles, ornate trim, layered parallax backdrops per biome; keep door height 6 and shaft width 6 | High |
| Exit door, treasure chest, rune shrine | n/a | Tinted blocks with labels. Need ornate props with glow | Medium |
| Item icons (weapons, skills, amulets, outfits) | 128x128 px, one per base item | Currently tinted shapes (`UiKit.FillIcon`). Rarity frames and glow come from the art pass | High |
| HUD, shop, altar and replace-slot menus | 1920x1080 reference | Plain panels and the built-in font. Need ornate mythic frames, bold typography, glowing rarity colours; keep the layout and navigation | High |
| Mutation, status effect and scroll icons | 64x64 px | Coloured squares with letters. Need icons per mutation, status and scroll colour | Medium |
| Pickup props (chest, merchant, altar, scrolls, coins, Cells) | n/a | Shapes with labels. Need animated props | Medium |
| Skill entities (grenade, turret, trap) | n/a | Shapes. Need animated, readable props with explosion flipbooks | Medium |
| Player animation set | n/a | Idle, run, jump, fall, land, roll, ladder, ledge hang/climb, wall slide. Currently procedural squash and stretch only | High |
