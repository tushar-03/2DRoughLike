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
| Player animation set | n/a | Idle, run, jump, fall, land, roll, ladder, ledge hang/climb, wall slide. Currently procedural squash and stretch only | High |
