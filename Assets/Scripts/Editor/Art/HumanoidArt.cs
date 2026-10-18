using UnityEngine;
using Roughlike.Utils;

namespace Roughlike.Editor.Art
{
    public enum AttackStyle { Swing, Spit, Shoot, Cast }

    /// <summary>Everything that makes one two-legged character look like itself: proportions, palette, props and how it attacks.</summary>
    public sealed class HumanoidSpec
    {
        public string Name = "Humanoid";
        public float Height = 250f;       // feet to top of head, in frame pixels
        public float HeadR = 30f;
        public float Bulk = 1f;           // body width multiplier
        public float Hunch = 0f;          // head pushed forward, px
        public float LegScale = 1f;
        public Color Skin = new Color(0.85f, 0.7f, 0.6f);
        public Color Cloth = new Color(0.5f, 0.5f, 0.55f);
        public Color Cloth2 = new Color(0.35f, 0.35f, 0.4f);
        public Color Trim = new Color(0.9f, 0.7f, 0.3f);
        public Color Metal = new Color(0.7f, 0.72f, 0.78f);
        public Color Glow = new Color(1f, 0.7f, 0.25f);
        public Color Boot = new Color(0.25f, 0.2f, 0.2f);
        public Color Rim = new Color(1f, 0.85f, 0.55f);
        public bool Hood, Cloak, Bell, Fins, Horns, Robe, Floating, ShieldBlock, Helmet, Mouth;
        public string Weapon = "sword";   // sword, dagger, club, mace, bow, staff, none
        public float WeaponLen = 95f;
        public AttackStyle Attack = AttackStyle.Swing;
        public float Outline = 3.4f;
        public float Scale = 1f;          // bosses are drawn bigger on a bigger canvas
        public bool Hat;                  // a tall pointed witch's hat
        public bool RuntimeGear;          // the player: weapon and shield are separate sprites; record hand anchors instead of painting them
        public bool EnemyShield;          // a heater shield strapped to the off arm, always up
    }

    /// <summary>A pose in frame-local pixels, origin at the centre of the feet on the floor, +x forward.</summary>
    public struct Pose
    {
        public Vector2 Hip, FootF, FootB, HandF, HandB, HeadOff;
        public float Lean, WeaponAngle, Sway, Lift, Draw;
        /// <summary>Where the shield is held (frame-local) when the pose sets it; otherwise the default guard position.</summary>
        public Vector2 ShieldPos;
        public bool HasShieldPos;
        /// <summary>Angle of the off-hand weapon (dual wielding); derived from the main hand when not set.</summary>
        public float WeaponAngleB;
        public bool HasAngleB;
        public bool EyesClosed, ShieldUp, MouthOpen;
    }

    public static partial class HumanoidArt
    {
        public const int FrameSize = 320;
        /// <summary>Anchors of the frame painted last (only meaningful for <see cref="HumanoidSpec.RuntimeGear"/> specs).</summary>
        public static CharacterArt.Anchor LastAnchor;
        public static bool HasAnchor;

        const float FloorY = 34f;
        const float CentreX = 140f;
        const float Ankle = 10f;

        // ------------------------------------------------------------------ geometry helpers

        static Vector2 Dir(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }

        /// <summary>Two-bone IK: the middle joint for a limb reaching from root toward target. bend picks the side it bows.</summary>
        static Vector2 Ik(Vector2 root, Vector2 target, float l1, float l2, float bend)
        {
            Vector2 d = target - root;
            float dist = Mathf.Clamp(d.magnitude, 0.01f, l1 + l2 - 0.5f);
            float baseAngle = Mathf.Atan2(d.y, d.x);
            float cosA = Mathf.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist), -1f, 1f);
            float a = Mathf.Acos(cosA) * bend;
            return root + new Vector2(Mathf.Cos(baseAngle + a), Mathf.Sin(baseAngle + a)) * l1;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>Foot path for one leg at phase 0..1: slides back along the floor, then swings forward lifted.</summary>
        static Vector2 FootCycle(float phase, float stride, float lift)
        {
            float p = Mathf.Repeat(phase, 1f);
            if (p < 0.5f) return new Vector2(Mathf.Lerp(stride, -stride, p * 2f), Ankle);
            float u = (p - 0.5f) * 2f;
            return new Vector2(Mathf.Lerp(-stride, stride, Smooth(u)), Ankle + lift * Mathf.Sin(Mathf.PI * u));
        }

        // ------------------------------------------------------------------ poses

        static float LegLen(HumanoidSpec s) => s.Height * 0.43f * s.LegScale;
        static float TorsoLen(HumanoidSpec s) => s.Height * 0.285f;
        static float RestHipY(HumanoidSpec s) => Ankle + LegLen(s) * 0.985f;

        static Pose Stand(HumanoidSpec s)
        {
            float hy = RestHipY(s);
            return new Pose
            {
                Hip = new Vector2(0f, hy),
                FootF = new Vector2(16f, Ankle), FootB = new Vector2(-14f, Ankle),
                HandF = new Vector2(24f, hy - 26f), HandB = new Vector2(-18f, hy - 30f),
                Lean = 2f, WeaponAngle = 55f, Sway = -3f
            };
        }

        public static Pose PoseFor(HumanoidSpec s, string clip, float t)
        {
            if (TryMovePose(s, clip, t, out Pose moved)) return moved;
            Pose p = Stand(s);
            float hy = p.Hip.y;
            float phase = 2f * Mathf.PI * t;

            switch (clip)
            {
                case "idle":
                {
                    float b = Mathf.Sin(phase);
                    p.Hip.y += b * 1.8f;
                    p.HeadOff.y = b * 1.2f;
                    p.HandF.y += b * 2.5f; p.HandB.y += b * 2f;
                    p.WeaponAngle = 55f + b * 2.5f;
                    p.Sway = -3f - b * 2.5f;
                    p.Lean = 2f + b * 0.8f;
                    break;
                }
                case "walk":
                case "run":
                {
                    bool run = clip == "run";
                    float stride = run ? 38f : 22f, lift = run ? 26f : 13f;
                    p.FootF = FootCycle(t, stride, lift);
                    p.FootB = FootCycle(t + 0.5f, stride, lift);
                    p.Hip.y = hy - (run ? 9f : 3f) + Mathf.Abs(Mathf.Cos(phase * 1f)) * (run ? 7f : 3f);
                    p.Lean = run ? 11f : 3f;
                    p.HandF = new Vector2(22f + Mathf.Sin(phase + Mathf.PI) * (run ? 34f : 16f), p.Hip.y + (run ? 12f : -18f) + Mathf.Cos(phase) * 6f);
                    p.HandB = new Vector2(-16f + Mathf.Sin(phase) * (run ? 34f : 16f), p.Hip.y + (run ? 8f : -22f) + Mathf.Cos(phase + Mathf.PI) * 6f);
                    p.WeaponAngle = run ? 30f : 50f;
                    p.Sway = run ? -26f : -9f;
                    p.HeadOff.y = Mathf.Sin(phase * 2f) * 1.2f;
                    break;
                }
                case "jump":
                    p.Hip.y = hy + 4f;
                    p.FootF = new Vector2(20f, Ankle + 36f); p.FootB = new Vector2(-10f, Ankle + 14f);
                    p.HandF = new Vector2(36f, hy + 66f); p.HandB = new Vector2(-30f, hy + 48f);
                    p.Lean = 5f; p.WeaponAngle = 70f; p.Sway = -8f; p.Lift = 10f;
                    break;
                case "fall":
                    p.Hip.y = hy + 2f;
                    p.FootF = new Vector2(14f, Ankle + 6f); p.FootB = new Vector2(-16f, Ankle + 22f);
                    p.HandF = new Vector2(40f, hy + 52f); p.HandB = new Vector2(-36f, hy + 44f);
                    p.Lean = 0f; p.WeaponAngle = 80f; p.Sway = 4f; p.Lift = 26f;
                    break;
                case "hurt":
                {
                    float k = t;
                    p.Hip.x = -8f - k * 3f; p.Lean = -18f;
                    p.FootF = new Vector2(20f, Ankle); p.FootB = new Vector2(-22f, Ankle);
                    p.HandF = new Vector2(34f, hy + 62f); p.HandB = new Vector2(-34f, hy + 46f);
                    p.HeadOff = new Vector2(-8f, 3f);
                    p.WeaponAngle = 110f; p.Sway = 12f; p.EyesClosed = true; p.MouthOpen = true;
                    break;
                }
                case "block":
                    p.Lean = 6f; p.ShieldUp = true;
                    p.HandF = new Vector2(48f, hy + 40f); p.HandB = new Vector2(40f, hy + 30f);
                    p.FootF = new Vector2(24f, Ankle); p.FootB = new Vector2(-18f, Ankle);
                    p.WeaponAngle = 80f; p.Sway = -6f;
                    break;
                case "telegraph":
                    return Attack(s, t * 0.34f, true);
                case "attack":
                    return Attack(s, s.Attack == AttackStyle.Swing ? 0.34f + t * 0.66f : 0.34f + t * 0.66f, false);
                case "player_attack":
                    return Attack(s, t, false);
                case "player_shoot":
                    return Shoot(s, t, 0f);
            }
            return p;
        }

        /// <summary>Raise the bow, draw the string back, release. Used by archers and by the player's bow.</summary>
        static Pose Shoot(HumanoidSpec s, float t, float shake, float aimEnd = 0.2f, float drawEnd = 0.34f, float lean = 6f, float crouch = 0f)
        {
            Pose p = Stand(s);
            float hy = p.Hip.y;
            float aim = Smooth(Mathf.Clamp01(t / aimEnd));
            float draw = t < drawEnd ? Smooth(Mathf.Clamp01((t - aimEnd * 0.5f) / (drawEnd - aimEnd * 0.5f))) : Mathf.Max(0f, 1f - (t - drawEnd) * 7f);
            p.Hip.y = hy - crouch * aim;
            p.Lean = Mathf.Lerp(2f, lean, aim);
            p.HandF = new Vector2(Mathf.Lerp(26f, 84f, aim), Mathf.Lerp(hy + 6f, hy + TorsoLen(s) * 0.8f, aim));
            p.HandB = new Vector2(Mathf.Lerp(-20f, 84f - draw * 62f, aim), p.HandF.y + shake);
            p.Draw = draw;
            p.FootF = new Vector2(26f, Ankle); p.FootB = new Vector2(-20f, Ankle);
            p.WeaponAngle = 90f; p.Sway = -4f;
            return p;
        }

        /// <summary>The windup-swing-recovery arc, t 0..1. Anticipation takes roughly the first third.</summary>
        static Pose Attack(HumanoidSpec s, float t, bool telegraphing)
        {
            Pose p = Stand(s);
            float hy = p.Hip.y;
            float shake = telegraphing ? Mathf.Sin(t * 90f) * 1.2f : 0f;

            switch (s.Attack)
            {
                case AttackStyle.Shoot:
                    return Shoot(s, t, shake);
                case AttackStyle.Spit:
                {
                    float back = Smooth(Mathf.Clamp01(t / 0.32f));
                    float fwd = Smooth(Mathf.Clamp01((t - 0.34f) / 0.2f));
                    float rec = Smooth(Mathf.Clamp01((t - 0.6f) / 0.4f));
                    float spitLean = Mathf.Lerp(2f, -16f, back);
                    spitLean = Mathf.Lerp(spitLean, 18f, fwd);
                    spitLean = Mathf.Lerp(spitLean, 4f, rec);
                    p.Lean = spitLean;
                    p.HeadOff.x = Mathf.Lerp(0f, -8f, back) + fwd * 14f * (1f - rec);
                    p.MouthOpen = t > 0.18f && t < 0.62f;
                    p.HandF = new Vector2(30f + fwd * 18f, hy + 20f); p.HandB = new Vector2(-4f, hy + 14f);
                    p.WeaponAngle = 55f; p.Sway = -5f + shake;
                    return p;
                }
                case AttackStyle.Cast:
                {
                    float up = Smooth(Mathf.Clamp01(t / 0.45f));
                    float down = Smooth(Mathf.Clamp01((t - 0.45f) / 0.2f));
                    float armH = Mathf.Lerp(hy + 10f, hy + TorsoLen(s) + 34f, up) - down * 40f;
                    p.HandF = new Vector2(34f + shake, armH); p.HandB = new Vector2(-22f - shake, armH - 4f);
                    p.Lean = Mathf.Lerp(2f, -4f, up) + down * 8f;
                    p.Draw = up * (1f - down);
                    p.Lift = up * 18f; p.Sway = -2f + up * 8f;
                    p.WeaponAngle = 90f;
                    return p;
                }
            }

            // swing: sword/club/mace
            float windUp = Smooth(Mathf.Clamp01(t / 0.3f));
            float swing = Smooth(Mathf.Clamp01((t - 0.3f) / 0.3f));
            float settle = Smooth(Mathf.Clamp01((t - 0.6f) / 0.4f));
            float wa = Mathf.Lerp(55f, 150f, windUp);
            wa = Mathf.Lerp(wa, -35f, swing);
            wa = Mathf.Lerp(wa, -50f, settle);
            float lean = Mathf.Lerp(2f, -10f, windUp);
            lean = Mathf.Lerp(lean, 16f, swing);
            lean = Mathf.Lerp(lean, 10f, settle);

            p.WeaponAngle = wa + shake * 3f;
            p.Lean = lean;
            float reachAngle = wa * 0.6f + 10f;
            Vector2 shoulder = new Vector2(0f, hy + TorsoLen(s) * 0.92f);
            p.HandF = shoulder + Dir(reachAngle) * (s.Height * 0.2f);
            p.HandB = new Vector2(Mathf.Lerp(-20f, 8f, windUp), hy + TorsoLen(s) * 0.55f);
            p.Hip.x = swing * 10f * (1f - settle * 0.6f) - windUp * 4f * (1f - swing);
            p.FootF = new Vector2(Mathf.Lerp(16f, 36f, swing), Ankle);
            p.FootB = new Vector2(Mathf.Lerp(-14f, -22f, windUp), Ankle);
            p.Sway = Mathf.Lerp(-3f, 14f, windUp) - swing * 30f;
            return p;
        }

        // ------------------------------------------------------------------ drawing

        public static Color32[] Render(HumanoidSpec s, string clip, float t)
        {
            int canvas = Mathf.RoundToInt(FrameSize * s.Scale);
            var painter = new Painter(canvas, canvas) { Scale = s.Scale };
            Draw(painter, s, PoseFor(s, clip, t), clip);
            return painter.Render(s.Outline * Mathf.Pow(s.Scale, 0.8f), new Color(0.07f, 0.05f, 0.1f));
        }

        public static Color32[] RenderRoll(HumanoidSpec s, float t)
        {
            var pt = new Painter(FrameSize, FrameSize);
            float ang = t * Mathf.PI * 2f;
            Vector2 c = new Vector2(CentreX, FloorY + 62f);
            float body = s.Height * 0.2f * Mathf.Max(0.9f, s.Bulk);
            Mat cloth = Mat.Of(s.Cloth, s.Rim);
            Mat cloth2 = Mat.Of(s.Cloth2);
            pt.Circle(c, body + 8f, cloth);
            pt.Capsule(c + Dir(ang * 57.3f + 200f) * (body * 0.3f), c + Dir(ang * 57.3f + 200f) * (body + 18f), 10f, 9f, Mat.Of(s.Boot));
            pt.Capsule(c + Dir(ang * 57.3f + 140f) * (body * 0.3f), c + Dir(ang * 57.3f + 140f) * (body + 14f), 10f, 9f, Mat.Of(s.Boot));
            pt.Circle(c + Dir(ang * 57.3f + 70f) * (body * 0.62f), s.HeadR * 0.9f, s.Hood ? cloth2 : Mat.Of(s.Skin, s.Rim));
            if (s.Bell) pt.Ellipse(c + Dir(ang * 57.3f - 20f) * (body * 0.8f), 11f, 13f, 0f, Mat.Of(s.Trim, s.Rim));
            return pt.Render(s.Outline, new Color(0.07f, 0.05f, 0.1f));
        }

        static void Draw(Painter pt, HumanoidSpec s, Pose p, string clip)
        {
            Vector2 O = new Vector2(CentreX, FloorY);
            float Rad = Mathf.Deg2Rad;
            float hipY = p.Hip.y;
            Vector2 hip = O + p.Hip;
            Vector2 up = new Vector2(Mathf.Sin(p.Lean * Rad), Mathf.Cos(p.Lean * Rad));
            Vector2 fwd = new Vector2(up.y, -up.x);
            float torso = TorsoLen(s);
            Vector2 chest = hip + up * torso;
            Vector2 neck = chest + up * 4f;
            Vector2 head = neck + up * (s.HeadR * 0.86f) + new Vector2(s.Hunch + p.HeadOff.x, p.HeadOff.y);
            float bulk = s.Bulk;
            float limb = 13f * Mathf.Sqrt(bulk);

            Mat cloth = Mat.Of(s.Cloth, s.Rim);
            Mat clothDark = Mat.Of(s.Cloth2, s.Rim * 0.7f);
            Mat trousers = Mat.Of(Color.Lerp(s.Cloth2, s.Cloth, 0.25f), s.Rim * 0.8f);
            Mat skin = Mat.Of(s.Skin, s.Rim);
            Mat skinDark = Mat.Of(Color.Lerp(s.Skin, s.Cloth2, 0.35f));
            Mat metal = Mat.Of(s.Metal, Color.white);
            Mat trim = Mat.Of(s.Trim, Color.white);
            Mat boot = Mat.Of(s.Boot, s.Rim * 0.8f);
            Mat glow = Mat.Glow(s.Glow);
            Mat sleeveBack = Mat.Of(Color.Lerp(s.Cloth, s.Cloth2, 0.45f));

            Vector2 shF = chest - up * 12f + fwd * 6f;
            Vector2 shB = chest - up * 12f - fwd * 5f;
            float armL = s.Height * 0.2f;
            Vector2 handF = O + p.HandF, handB = O + p.HandB;
            Vector2 elbowF = Ik(shF, handF, armL, armL, -1f);
            Vector2 elbowB = Ik(shB, handB, armL, armL, -1f);

            // ---- back arm
            Limb(pt, shB, elbowB, handB, limb * 0.9f, sleeveBack, skinDark);

            // ---- back leg
            float legL = LegLen(s) * 0.5f;
            if (!s.Floating && !s.Robe)
                Leg(pt, hip + new Vector2(-5f, 0f), O + p.FootB, legL, limb * 1.05f, s, Mat.Of(Color.Lerp(s.Cloth2, Color.black, 0.15f)), boot);

            // ---- cloak behind the body
            if (s.Cloak)
            {
                float hem = O.y + hipY - LegLen(s) * 0.62f;
                float sway = p.Sway;
                Vector2 nape = neck - fwd * 8f;
                pt.Poly(new[]
                {
                    nape + new Vector2(-4f, 6f),
                    nape + fwd * 26f + up * -4f,
                    chest - fwd * 8f - up * 30f,
                    new Vector2(hip.x - 12f * bulk + sway * 0.35f, hem + 16f + p.Lift * 0.6f),
                    new Vector2(hip.x - 34f * bulk + sway, hem - 8f + p.Lift * 1.3f),
                    new Vector2(hip.x - 56f * bulk + sway * 1.25f, hem + 22f + p.Lift * 1.7f),
                    chest - fwd * 40f * bulk + new Vector2(sway * 0.5f, 2f)
                }, clothDark);
                pt.Capsule(new Vector2(hip.x - 12f * bulk + sway * 0.35f, hem + 16f + p.Lift * 0.6f), new Vector2(hip.x - 34f * bulk + sway, hem - 8f + p.Lift * 1.3f), 3.4f, trim);
            }

            // ---- torso / robe / tunic
            float rHip = 20f * bulk, rChest = 24f * bulk;
            if (s.Robe)
            {
                float hover = s.Floating ? 18f : 0f;
                float bottom = O.y + 8f + hover + p.Lift;
                pt.Poly(new[]
                {
                    chest - fwd * rChest + up * 4f,
                    chest + fwd * rChest + up * 4f,
                    hip + fwd * (rHip + 6f),
                    new Vector2(hip.x + rHip * 2.2f + p.Sway * 0.2f, bottom + 6f),
                    new Vector2(hip.x + rHip * 1.0f, bottom - 8f),
                    new Vector2(hip.x - rHip * 0.2f + p.Sway * 0.4f, bottom + 6f),
                    new Vector2(hip.x - rHip * 1.2f + p.Sway * 0.6f, bottom - 6f),
                    new Vector2(hip.x - rHip * 2.1f + p.Sway * 0.8f, bottom + 10f),
                    hip - fwd * (rHip + 6f)
                }, cloth);
                pt.Capsule(hip - fwd * (rHip + 2f), hip + fwd * (rHip + 2f), 5.5f, trim);
            }
            else
            {
                // tunic: shoulders to a flared hem a little below the hip
                Vector2 hem = hip - up * 14f;
                pt.Poly(new[]
                {
                    chest - fwd * rChest + up * 2f,
                    chest + fwd * (rChest + 2f) + up * 2f,
                    hem + fwd * (rHip + 8f),
                    hem - fwd * (rHip + 6f)
                }, cloth);
                pt.Capsule(hip - fwd * (rHip + 2f) + up * 2f, hip + fwd * (rHip + 4f) + up * 2f, 5.5f, trim);   // belt
                if (s.Helmet || s.Bulk > 1.25f)
                    pt.Capsule(chest - up * 8f - fwd * 8f, chest - up * 8f + fwd * 10f, 18f * bulk, 17f * bulk, metal);   // pauldron
            }

            // ---- front leg
            if (!s.Floating && !s.Robe)
                Leg(pt, hip + new Vector2(5f, 0f), O + p.FootF, legL, limb * 1.1f, s, trousers, boot);
            else if (s.Robe && !s.Floating)
                pt.Capsule(O + p.FootF + new Vector2(2f, 0f), O + p.FootF + new Vector2(18f, -2f), 8f, boot);

            // ---- bell on the belt
            if (s.Bell)
            {
                Vector2 b = hip + fwd * (rHip * 0.85f) - up * 8f;
                pt.Circle(b + new Vector2(0f, 3f), 4f, metal);
                pt.Ellipse(b - new Vector2(0f, 10f), 13f, 14f, 0f, trim);
                pt.Circle(b - new Vector2(0f, 23f), 4.5f, metal);
            }

            // ---- head
            Head(pt, s, p, head, up, skin, cloth, clothDark, metal, glow);
            if (s.Cloak || s.Hood)
                pt.Ellipse(neck - fwd * 4f + up * 1f, 26f * Mathf.Sqrt(bulk), 12f, p.Lean * -0.6f, s.Hood ? clothDark : trim);   // collar / hood bunching

            // ---- front arm and what it holds
            WeaponPalette pal = new WeaponPalette
            {
                Metal = metal, Trim = trim, Wood = Mat.Of(s.Boot), Leather = Mat.Of(s.Boot * 0.8f), Glow = glow,
                Field = Mat.Of(Color.Lerp(s.Cloth2, s.Cloth, 0.2f), s.Rim, 3f)
            };
            if (s.EnemyShield) WeaponArt.Heater(pt, handB + new Vector2(20f, 4f), 36f * Mathf.Sqrt(bulk), 130f, pal);
            if (!s.RuntimeGear) Weapon(pt, s, p, handF, pal);
            Limb(pt, shF, elbowF, handF, limb, cloth, skin);
            if (p.ShieldUp && !s.RuntimeGear) WeaponArt.Heater(pt, O + new Vector2(62f, hipY + torso * 0.3f), 40f, 140f, pal);
            if (s.RuntimeGear)
            {
                LastAnchor = new CharacterArt.Anchor
                {
                    valid = true,
                    hand = handF * s.Scale,
                    angle = p.WeaponAngle,
                    handB = handB * s.Scale,
                    angleB = p.HasAngleB ? p.WeaponAngleB : p.WeaponAngle * 0.75f - 25f,
                    shieldUp = p.ShieldUp,
                    shield = (p.HasShieldPos ? O + p.ShieldPos : O + new Vector2(60f, hipY + torso * 0.3f)) * s.Scale,
                    stow = (O + new Vector2(-36f, hipY + torso * 0.5f)) * s.Scale
                };
                HasAnchor = true;
            }
            if (s.Attack == AttackStyle.Cast && p.Draw > 0.02f)
            {
                Vector2 orb = (handF + handB) * 0.5f + new Vector2(0f, 16f + 10f * p.Draw);
                pt.Circle(orb, 9f + 17f * p.Draw, glow);
                pt.Circle(orb, 4f + 8f * p.Draw, Mat.Glow(Color.Lerp(s.Glow, Color.white, 0.7f)));
            }
        }

        static void Limb(Painter pt, Vector2 root, Vector2 mid, Vector2 end, float r, Mat sleeve, Mat hand)
        {
            pt.Circle(root, r * 1.12f, sleeve);                       // shoulder
            pt.Capsule(root, mid, r, r * 0.9f, sleeve);
            Vector2 wrist = Vector2.Lerp(mid, end, 0.82f);
            pt.Capsule(mid, wrist, r * 0.9f, r * 0.74f, sleeve);
            pt.Circle(end, r * 0.78f, hand);
        }

        static void Leg(Painter pt, Vector2 hip, Vector2 foot, float half, float r, HumanoidSpec s, Mat trousers, Mat boot)
        {
            Vector2 knee = Ik(hip, foot + new Vector2(0f, 4f), half, half, 1f);
            pt.Capsule(hip, knee, r, r * 0.86f, trousers);
            pt.Capsule(knee, foot + new Vector2(-2f, 4f), r * 0.86f, r * 0.72f, trousers);
            pt.Capsule(foot + new Vector2(-6f, 2f), foot + new Vector2(18f, -1f), 9f, 8f, boot);   // boot
        }

        static void Head(Painter pt, HumanoidSpec s, Pose p, Vector2 head, Vector2 up, Mat skin, Mat cloth, Mat clothDark, Mat metal, Mat glow)
        {
            float R = s.HeadR;
            if (s.Fins)
            {
                pt.Poly(new[] { head + new Vector2(-R * 0.6f, R * 0.5f), head + new Vector2(-R * 1.5f, R * 1.3f), head + new Vector2(-R * 1.0f, -R * 0.2f), head + new Vector2(-R * 1.6f, -R * 0.6f), head + new Vector2(-R * 0.5f, -R * 0.6f) }, Mat.Of(s.Trim, s.Rim));
            }
            if (s.Hood)
            {
                pt.Ellipse(head + new Vector2(-3f, 3f), R * 1.12f, R * 1.22f, 0f, cloth);
                pt.Poly(new[] { head + new Vector2(-R * 0.6f, R * 0.5f), head + new Vector2(-R * 1.5f, -R * 0.5f), head + new Vector2(-R * 0.4f, -R * 1.0f) }, clothDark);
                pt.Ellipse(head + new Vector2(R * 0.28f, -1f), R * 0.74f, R * 0.84f, 0f, Mat.Of(new Color(0.07f, 0.05f, 0.1f), null));
            }
            else if (s.Helmet)
            {
                pt.Circle(head, R * 1.05f, metal);
                pt.Capsule(head + new Vector2(R * 0.1f, -2f), head + new Vector2(R * 1.0f, -2f), 4.5f, Mat.Of(new Color(0.07f, 0.05f, 0.1f), null));   // visor slit
            }
            else
            {
                pt.Circle(head, R, skin);
                if (s.Weapon != "bow")
                    pt.Ellipse(head + new Vector2(-R * 0.25f, R * 0.35f), R * 0.95f, R * 0.7f, 10f, clothDark);   // hair / cap
            }
            if (s.Hat)
            {
                Mat hat = Mat.Of(new Color(0.2f, 0.12f, 0.32f), new Color(0.8f, 0.6f, 1f), 5f);
                pt.Ellipse(head + new Vector2(0f, R * 0.78f), R * 1.75f, R * 0.32f, 8f, hat);                                              // brim
                pt.Poly(new[] { head + new Vector2(-R * 0.82f, R * 0.8f), head + new Vector2(R * 0.9f, R * 0.85f), head + new Vector2(R * 0.5f, R * 1.7f), head + new Vector2(R * 1.4f, R * 2.45f), head + new Vector2(-R * 0.1f, R * 1.95f) }, hat);   // bent cone
                pt.Capsule(head + new Vector2(-R * 0.82f, R * 0.95f), head + new Vector2(R * 0.9f, R * 1.0f), 4f, Mat.Of(new Color(0.75f, 0.5f, 0.95f), Color.white));   // band
            }
            if (s.Horns)
            {
                pt.Poly(new[] { head + new Vector2(-4f, R * 0.8f), head + new Vector2(-R * 0.9f, R * 1.9f), head + new Vector2(R * 0.3f, R * 0.95f) }, Mat.Of(new Color(0.9f, 0.85f, 0.7f), Color.white));
            }

            // eyes
            Vector2 eye = head + new Vector2(R * 0.42f, R * 0.08f);
            if (p.EyesClosed) pt.Capsule(eye + new Vector2(-6f, 0f), eye + new Vector2(8f, -2f), 2.4f, Mat.Glow(new Color(0.07f, 0.05f, 0.1f)));
            else
            {
                pt.Ellipse(eye, 7.5f, 5.2f, 12f, glow);
                pt.Circle(eye + new Vector2(2f, 1f), 2.2f, Mat.Glow(Color.Lerp(s.Glow, Color.white, 0.8f)));
            }
            if (s.Mouth || p.MouthOpen)
            {
                Vector2 m = head + new Vector2(R * 0.55f, -R * 0.45f);
                if (p.MouthOpen) pt.Ellipse(m, 10f, 9f, 0f, Mat.Glow(new Color(0.55f, 0.1f, 0.12f)));
                else pt.Capsule(m + new Vector2(-8f, 0f), m + new Vector2(10f, -1f), 2f, Mat.Glow(new Color(0.07f, 0.05f, 0.1f)));
            }
        }

        static void Weapon(Painter pt, HumanoidSpec s, Pose p, Vector2 hand, WeaponPalette pal)
        {
            if (s.Weapon == "none") return;
            WeaponArt.Draw(pt, s.Weapon, hand, Dir(p.WeaponAngle), s.WeaponLen, pal, p.Draw);
        }
    }
}
