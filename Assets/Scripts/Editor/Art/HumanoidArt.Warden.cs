using UnityEngine;

namespace Roughlike.Editor.Art
{
    /// <summary>
    /// The Storm Warden's extras: a flowing white beard, feathered wings (flapping with the pose), and his move poses
    /// (guard, dodge dash, sword-swinging charge and the three stages of the sky thrust).
    /// </summary>
    public static partial class HumanoidArt
    {
        static bool TryBossPose(HumanoidSpec s, string clip, float t, out Pose pose)
        {
            pose = default;
            switch (clip)
            {
                case "block": pose = WardenBlock(s, t); return true;
                case "dash": pose = WardenDash(s, t); return true;
                case "charge": pose = WardenCharge(s, t); return true;
                case "sky": pose = WardenSky(s, t); return true;
            }
            return false;
        }

        /// <summary>The greatsword held upright in front of him, feet planted wide.</summary>
        static Pose WardenBlock(HumanoidSpec s, float t)
        {
            Pose p = Stand(s);
            float hy = p.Hip.y, torso = TorsoLen(s);
            float k = Smooth(Mathf.Clamp01(t * 3f));
            p.Hip.y = hy - 8f * k;
            p.Lean = Mathf.Lerp(2f, 7f, k);
            p.HandF = Vector2.Lerp(p.HandF, new Vector2(48f, hy + torso * 0.7f), k);
            p.HandB = Vector2.Lerp(p.HandB, new Vector2(40f, hy + torso * 0.45f), k);
            p.WeaponAngle = Mathf.Lerp(55f, 84f, k);
            p.FootF = new Vector2(Mathf.Lerp(16f, 30f, k), Ankle);
            p.FootB = new Vector2(Mathf.Lerp(-14f, -26f, k), Ankle);
            p.Sway = -6f;
            return p;
        }

        /// <summary>A low, stretched lunge with the blade trailing behind.</summary>
        static Pose WardenDash(HumanoidSpec s, float t)
        {
            Pose p = Stand(s);
            float hy = p.Hip.y, torso = TorsoLen(s);
            p.Hip = new Vector2(8f, hy - 26f);
            p.Lean = 26f;
            p.FootF = new Vector2(40f, Ankle);
            p.FootB = new Vector2(-34f, Ankle + 6f);
            p.HandF = new Vector2(-6f, hy + torso * 0.35f);
            p.HandB = new Vector2(30f, hy + torso * 0.4f);
            p.WeaponAngle = 190f;   // pointing back along the dash
            p.Sway = -34f;
            return p;
        }

        /// <summary>Running flat out with the sword swung round and round.</summary>
        static Pose WardenCharge(HumanoidSpec s, float t)
        {
            Pose p = Stand(s);
            float hy = p.Hip.y, torso = TorsoLen(s);
            float phase = 2f * Mathf.PI * t;
            p.FootF = FootCycle(t, 40f, 24f);
            p.FootB = FootCycle(t + 0.5f, 40f, 24f);
            p.Hip.y = hy - 12f + Mathf.Abs(Mathf.Cos(phase)) * 7f;
            p.Lean = 17f;
            float wa = Mathf.Lerp(30f, -50f, 0.5f + 0.5f * Mathf.Sin(phase - 1.2f));
            p.WeaponAngle = wa;
            Vector2 shoulder = new Vector2(8f, p.Hip.y + torso * 0.92f);
            p.HandF = shoulder + Dir(wa * 0.6f + 10f) * (s.Height * 0.2f);
            p.HandB = new Vector2(14f, p.Hip.y + torso * 0.55f);
            p.Sway = -24f;
            p.HeadOff.y = Mathf.Sin(phase * 2f) * 1.5f;
            return p;
        }

        /// <summary>t 0 = crouched for the jump, 0.5 = rising with the sword over his head, 1 = plunging blade-down.</summary>
        static Pose WardenSky(HumanoidSpec s, float t)
        {
            Pose p = Stand(s);
            float hy = p.Hip.y, torso = TorsoLen(s);
            if (t < 0.25f)
            {
                p.Hip.y = hy - 26f;
                p.Lean = 12f;
                p.FootF = new Vector2(30f, Ankle); p.FootB = new Vector2(-24f, Ankle);
                p.HandF = new Vector2(14f, hy + torso * 0.5f); p.HandB = new Vector2(0f, hy + torso * 0.3f);
                p.WeaponAngle = 55f; p.Sway = 8f;
            }
            else if (t < 0.75f)
            {
                p.Hip.y = hy + 18f;
                p.Lean = -4f;
                p.FootF = new Vector2(20f, Ankle + 46f); p.FootB = new Vector2(-12f, Ankle + 30f);
                p.HandF = new Vector2(30f, hy + torso * 0.8f); p.HandB = new Vector2(20f, hy + torso * 0.55f);
                p.WeaponAngle = 38f; p.Sway = 10f; p.Lift = 20f;
            }
            else
            {
                p.Hip.y = hy + 10f;
                p.Lean = 6f;
                p.FootF = new Vector2(10f, Ankle + 24f); p.FootB = new Vector2(-6f, Ankle + 30f);
                p.HandF = new Vector2(34f, hy + torso * 0.85f); p.HandB = new Vector2(28f, hy + torso * 0.6f);
                p.WeaponAngle = -80f; p.Sway = 18f; p.Lift = 26f;
            }
            return p;
        }

        // ------------------------------------------------------------------ beard and wings

        static void HairBack(Painter pt, HumanoidSpec s, Vector2 head)
        {
            float R = s.HeadR;
            Mat white = Mat.Of(new Color(0.97f, 0.97f, 1f), s.Rim);
            pt.Ellipse(head + new Vector2(-R * 0.45f, -R * 0.25f), R * 0.75f, R * 1.3f, 8f, white);
            pt.Ellipse(head + new Vector2(-R * 0.1f, R * 0.5f), R * 1.0f, R * 0.55f, 10f, white);   // fringe over the crown
        }

        static void Beard(Painter pt, HumanoidSpec s, Vector2 head)
        {
            float R = s.HeadR;
            Mat white = Mat.Of(new Color(0.97f, 0.97f, 1f), s.Rim);
            Mat shade = Mat.Of(new Color(0.8f, 0.84f, 0.94f), s.Rim * 0.7f);
            // the beard flowing down from the chin (the hair behind the head is HairBack)
            pt.Poly(new[]
            {
                head + new Vector2(R * 0.05f, -R * 0.1f),
                head + new Vector2(R * 0.95f, -R * 0.2f),
                head + new Vector2(R * 0.85f, -R * 1.05f),
                head + new Vector2(R * 0.55f, -R * 1.95f),
                head + new Vector2(R * 0.25f, -R * 1.45f),
                head + new Vector2(-R * 0.05f, -R * 1.9f),
                head + new Vector2(-R * 0.35f, -R * 0.9f)
            }, white);
            pt.Capsule(head + new Vector2(R * 0.4f, -R * 0.38f), head + new Vector2(R * 1.0f, -R * 0.3f), 4.2f, white);   // moustache
            // gold circlet
            pt.Capsule(head + new Vector2(-R * 0.8f, R * 0.52f), head + new Vector2(R * 0.8f, R * 0.5f), 3.4f, Mat.Of(s.Trim, Color.white));
        }

        /// <summary>Two feathered wings rising from the shoulder blades. flap +1 is wings up, -1 is wings swept down.</summary>
        static void Wings(Painter pt, HumanoidSpec s, Pose p, Vector2 chest, Vector2 up, Vector2 fwd)
        {
            Mat front = Mat.Of(new Color(0.97f, 0.97f, 1f), s.Rim);
            Mat frontDark = Mat.Of(new Color(0.84f, 0.88f, 0.98f), s.Rim * 0.8f);
            Mat back = Mat.Of(new Color(0.72f, 0.78f, 0.92f), s.Rim * 0.6f);
            Vector2 root = chest - fwd * 14f - up * 8f;
            float len = s.Height * 0.4f;
            // far wing first (smaller, darker), then the near one
            for (int wing = 0; wing < 2; wing++)
            {
                bool far = wing == 0;
                Vector2 b = root + (far ? new Vector2(-10f, 6f) : Vector2.zero);
                float scale = far ? 0.84f : 1f;
                int feathers = 8;
                for (int i = 0; i < feathers; i++)
                {
                    float u = i / (float)(feathers - 1);
                    float ang = 176f - u * 46f - p.Flap * 18f + (far ? 8f : 0f);
                    float l = len * scale * (1f - 0.34f * Mathf.Abs(u - 0.35f));
                    Vector2 tip = b + Dir(ang) * l;
                    pt.Capsule(b, tip, 14f * scale, 8f * scale, far ? back : (i % 2 == 0 ? front : frontDark));
                }
                // the shorter covert row hides where the feathers meet the shoulder
                for (int i = 0; i < 5; i++)
                {
                    float u = i / 4f;
                    float ang = 170f - u * 42f - p.Flap * 16f + (far ? 8f : 0f);
                    pt.Capsule(b, b + Dir(ang) * len * 0.5f * scale, 15f * scale, 9f * scale, far ? back : front);
                }
                pt.Circle(b, 13f * scale, far ? back : front);
            }
        }
    }
}
