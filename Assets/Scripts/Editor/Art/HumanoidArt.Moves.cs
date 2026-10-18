using UnityEngine;

namespace Roughlike.Editor.Art
{
    /// <summary>
    /// The player's combat and special poses. Every attack is built the way animators build one: a slow anticipation that coils
    /// the body, a very fast strike that eases in, a follow-through that eases out, and a recovery back to the stance. The hips
    /// drop, the front foot steps and the cloak lags, so the body moves with the arm instead of the arm swinging on a statue.
    /// Poses are in the same frame-local pixels as the rest of <see cref="HumanoidArt"/>; the weapon angle is degrees from forward
    /// (+x), 90 straight up.
    /// </summary>
    public static partial class HumanoidArt
    {
        struct Move
        {
            public float Start, Rest, Wind, Hit, End;         // weapon angle at the first frame, at the last (the ready stance), end of anticipation, at impact, after follow-through
            public float LeanStart, LeanWind, LeanHit, LeanEnd;
            public float ReachStart;
            public float Crouch, Lunge, Step;                 // hip drop at the coil, hip push at impact, front foot step at impact
            public float WindEnd, HitEnd, FollowEnd;          // phase boundaries within the clip
            public float Reach0, Reach1, Reach2;              // how far the hand is from the shoulder (a thrust reaches far)
            public bool Thrust, TwoHands;
        }

        static float EaseOut(float u) { u = 1f - Mathf.Clamp01(u); return 1f - u * u * u; }
        static float EaseIn(float u) { u = Mathf.Clamp01(u); return u * u * (0.6f + 0.4f * u); }

        /// <summary>Piecewise eased value through the four phases of a move.</summary>
        static float Phased(Move m, float t, float start, float wind, float hit, float end, float rest)
        {
            if (t < m.WindEnd) return Mathf.Lerp(start, wind, EaseOut(t / Mathf.Max(0.001f, m.WindEnd)));
            if (t < m.HitEnd) return Mathf.Lerp(wind, hit, EaseIn((t - m.WindEnd) / Mathf.Max(0.001f, m.HitEnd - m.WindEnd)));
            if (t < m.FollowEnd) return Mathf.Lerp(hit, end, EaseOut((t - m.HitEnd) / Mathf.Max(0.001f, m.FollowEnd - m.HitEnd)));
            return Mathf.Lerp(end, rest, Smooth((t - m.FollowEnd) / Mathf.Max(0.001f, 1f - m.FollowEnd)));
        }

        /// <summary>1 while coiled (anticipation, held through the strike), easing out in the recovery.</summary>
        static float Coil(Move m, float t)
        {
            if (t < m.WindEnd) return EaseOut(t / Mathf.Max(0.001f, m.WindEnd));
            if (t < m.HitEnd) return 1f - EaseIn((t - m.WindEnd) / Mathf.Max(0.001f, m.HitEnd - m.WindEnd)) * 0.55f;
            if (t < m.FollowEnd) return 0.45f * (1f - (t - m.HitEnd) / Mathf.Max(0.001f, m.FollowEnd - m.HitEnd));
            return 0f;
        }

        /// <summary>0 before the strike, 1 at impact and through the follow-through, back to 0 in the recovery.</summary>
        static float Impact(Move m, float t)
        {
            if (t < m.WindEnd) return 0f;
            if (t < m.HitEnd) return EaseIn((t - m.WindEnd) / Mathf.Max(0.001f, m.HitEnd - m.WindEnd));
            if (t < m.FollowEnd) return 1f;
            return 1f - Smooth((t - m.FollowEnd) / Mathf.Max(0.001f, 1f - m.FollowEnd));
        }

        static Move Slash(float rest, float wind, float hit, float end) => new Move
        {
            Start = rest, Rest = rest, Wind = wind, Hit = hit, End = end, LeanStart = 2f, ReachStart = 62f,
            LeanWind = -10f, LeanHit = 14f, LeanEnd = 9f, Crouch = 4f, Lunge = 10f, Step = 30f,
            WindEnd = 0.34f, HitEnd = 0.5f, FollowEnd = 0.76f, Reach0 = 62f, Reach1 = 62f, Reach2 = 62f
        };

        /// <summary>Makes a move begin where the previous one ended, so a combo flows: the arm does not return to the guard between hits.</summary>
        static Move After(Move m, Move previous)
        {
            m.Start = previous.End;
            m.LeanStart = previous.LeanEnd;
            m.ReachStart = previous.Reach2;
            return m;
        }

        static bool TryMove(string clip, out Move m, out Move off, out bool dual)
        {
            dual = false;
            off = default;
            switch (clip)
            {
                // ---- one-handed blades
                case "slash1": m = Slash(55f, 135f, -35f, -60f); return true;
                case "slash2":
                    m = Slash(55f, -60f, 95f, 130f);
                    m.LeanWind = 8f; m.LeanHit = -8f; m.LeanEnd = -4f; m.Crouch = 9f; m.Lunge = 8f; m.Step = 24f;
                    m = After(m, Slash(55f, 135f, -35f, -60f));
                    return true;
                case "overhead":
                    m = Slash(55f, 175f, -70f, -85f);
                    m.LeanWind = -20f; m.LeanHit = 22f; m.LeanEnd = 14f; m.Crouch = 10f; m.Lunge = 16f; m.Step = 38f;
                    m.WindEnd = 0.42f; m.HitEnd = 0.56f; m.FollowEnd = 0.8f;
                    m.Start = 130f; m.LeanStart = -4f;
                    return true;
                case "jab":
                    m = Slash(30f, 24f, 6f, 12f);
                    m.Thrust = true; m.Reach0 = 38f; m.Reach1 = 98f; m.Reach2 = 84f;
                    m.LeanWind = -4f; m.LeanHit = 12f; m.LeanEnd = 6f; m.Crouch = 6f; m.Lunge = 12f;
                    m.WindEnd = 0.3f; m.HitEnd = 0.46f; m.FollowEnd = 0.7f;
                    return true;
                case "jab2":
                    m = Slash(30f, 10f, -8f, 0f);
                    m.Thrust = true; m.Reach0 = 40f; m.Reach1 = 98f; m.Reach2 = 80f;
                    m.LeanWind = 2f; m.LeanHit = 10f; m.LeanEnd = 6f; m.Crouch = 13f; m.Lunge = 12f;
                    m.WindEnd = 0.3f; m.HitEnd = 0.46f; m.FollowEnd = 0.7f;
                    m.Start = 6f; m.LeanStart = 12f; m.ReachStart = 84f;
                    return true;
                case "flurry":
                    m = Slash(55f, 120f, -20f, -35f);
                    m.Lunge = 16f; m.Step = 36f; m.WindEnd = 0.26f; m.HitEnd = 0.4f; m.FollowEnd = 0.7f;
                    m.Start = 0f; m.LeanStart = 6f; m.ReachStart = 80f;
                    return true;

                // ---- a sword in each hand
                case "dual_a":
                    dual = true;
                    m = Slash(55f, 130f, -30f, -50f);
                    off = Slash(18f, 10f, 30f, 22f);
                    off.WindEnd = m.WindEnd; off.HitEnd = m.HitEnd; off.FollowEnd = m.FollowEnd;
                    return true;
                case "dual_b":
                    dual = true;
                    m = Slash(55f, 60f, 72f, 60f);
                    off = Slash(18f, 140f, -30f, -50f);
                    off.LeanWind = m.LeanWind = 8f; off.LeanHit = m.LeanHit = 12f; off.LeanEnd = m.LeanEnd = 8f;
                    m.Start = -50f; m.LeanStart = -10f + 14f; off.Start = 22f;
                    return true;
                case "dual_cross":
                    dual = true;
                    m = Slash(55f, 140f, -40f, -55f);
                    off = Slash(18f, -55f, 75f, 110f);
                    m.Crouch = 8f; m.Lunge = 14f;
                    m.Start = 60f; off.Start = -50f; m.LeanStart = 8f;
                    return true;
                case "whirl":
                    dual = true;
                    m = Slash(55f, 95f, -250f, -270f);              // a full turn
                    off = Slash(18f, 275f, -70f, -90f);
                    m.LeanWind = off.LeanWind = -6f; m.LeanHit = off.LeanHit = 6f; m.LeanEnd = off.LeanEnd = 4f;
                    m.Crouch = 14f; m.Lunge = 6f; m.Step = 20f;
                    m.WindEnd = 0.24f; m.HitEnd = 0.62f; m.FollowEnd = 0.8f;
                    off.WindEnd = m.WindEnd; off.HitEnd = m.HitEnd; off.FollowEnd = m.FollowEnd;
                    m.Start = -55f; off.Start = 110f; m.LeanStart = 8f;
                    return true;

                // ---- two-handed
                case "smash":
                    m = Slash(55f, 170f, -75f, -80f);
                    m.TwoHands = true; m.Reach0 = 66f; m.Reach1 = 66f; m.Reach2 = 66f;
                    m.LeanWind = -22f; m.LeanHit = 26f; m.LeanEnd = 16f; m.Crouch = 14f; m.Lunge = 14f; m.Step = 34f;
                    m.WindEnd = 0.46f; m.HitEnd = 0.58f; m.FollowEnd = 0.82f;
                    return true;
                case "slam":
                    m = Slash(55f, 185f, -85f, -95f);
                    m.TwoHands = true; m.Reach0 = 68f; m.Reach1 = 68f; m.Reach2 = 68f;
                    m.LeanWind = -28f; m.LeanHit = 32f; m.LeanEnd = 20f; m.Crouch = 18f; m.Lunge = 20f; m.Step = 40f;
                    m.WindEnd = 0.5f; m.HitEnd = 0.62f; m.FollowEnd = 0.84f;
                    m.Start = -80f; m.LeanStart = 16f;
                    return true;
                case "uppercut":
                    // a rising blow that lifts the weapon back up: the third hit of the hammer
                    m = Slash(55f, -75f, 125f, 155f);
                    m.TwoHands = true; m.Reach0 = 66f; m.Reach1 = 70f; m.Reach2 = 66f;
                    m.LeanWind = 22f; m.LeanHit = -14f; m.LeanEnd = -8f; m.Crouch = 18f; m.Lunge = 10f; m.Step = 30f;
                    m.WindEnd = 0.44f; m.HitEnd = 0.58f; m.FollowEnd = 0.82f;
                    m.Start = -95f; m.LeanStart = 20f;
                    return true;
                case "great_sweep":
                    m = Slash(55f, 170f, -5f, -40f);
                    m.TwoHands = true; m.Reach0 = 70f; m.Reach1 = 70f; m.Reach2 = 70f;
                    m.LeanWind = -16f; m.LeanHit = 24f; m.LeanEnd = 12f; m.Crouch = 8f; m.Lunge = 16f; m.Step = 34f;
                    m.WindEnd = 0.4f; m.HitEnd = 0.54f; m.FollowEnd = 0.8f;
                    return true;
                case "great_overhead":
                    m = Slash(55f, 178f, -72f, -88f);
                    m.TwoHands = true; m.Reach0 = 68f; m.Reach1 = 68f; m.Reach2 = 68f;
                    m.LeanWind = -24f; m.LeanHit = 28f; m.LeanEnd = 16f; m.Crouch = 14f; m.Lunge = 18f; m.Step = 38f;
                    m.WindEnd = 0.48f; m.HitEnd = 0.6f; m.FollowEnd = 0.84f;
                    m.Start = -40f; m.LeanStart = 12f;
                    return true;
                case "great_slash3":
                    // a backhand sweep that finishes the greatsword combo
                    m = Slash(55f, 195f, -15f, 30f);
                    m.TwoHands = true; m.Reach0 = 70f; m.Reach1 = 72f; m.Reach2 = 70f;
                    m.LeanWind = -22f; m.LeanHit = 20f; m.LeanEnd = 10f; m.Crouch = 10f; m.Lunge = 18f; m.Step = 36f;
                    m.WindEnd = 0.44f; m.HitEnd = 0.58f; m.FollowEnd = 0.82f;
                    m.Start = -88f; m.LeanStart = 16f;
                    return true;
                case "poke":
                    m = Slash(25f, 18f, 4f, 10f);
                    m.Thrust = true; m.TwoHands = true; m.Reach0 = 34f; m.Reach1 = 98f; m.Reach2 = 88f;
                    m.LeanWind = -6f; m.LeanHit = 14f; m.LeanEnd = 8f; m.Crouch = 8f; m.Lunge = 14f;
                    m.WindEnd = 0.32f; m.HitEnd = 0.48f; m.FollowEnd = 0.72f;
                    return true;
                case "poke_b":
                    // a second, lower thrust that picks up from the first
                    m = Slash(25f, 8f, -6f, 4f);
                    m.Thrust = true; m.TwoHands = true; m.Reach0 = 34f; m.Reach1 = 98f; m.Reach2 = 88f;
                    m.LeanWind = -2f; m.LeanHit = 16f; m.LeanEnd = 8f; m.Crouch = 13f; m.Lunge = 14f;
                    m.WindEnd = 0.3f; m.HitEnd = 0.46f; m.FollowEnd = 0.7f;
                    m.Start = 10f; m.LeanStart = 8f; m.ReachStart = 88f;
                    return true;
                case "sweep":
                    m = Slash(25f, 140f, 10f, -25f);
                    m.TwoHands = true; m.Reach0 = 70f; m.Reach1 = 72f; m.Reach2 = 70f;
                    m.LeanWind = -12f; m.LeanHit = 18f; m.LeanEnd = 10f; m.Crouch = 8f; m.Lunge = 12f; m.Step = 30f;
                    m.WindEnd = 0.36f; m.HitEnd = 0.52f; m.FollowEnd = 0.78f;
                    m.Start = 4f; m.LeanStart = 8f; m.ReachStart = 88f;
                    return true;
            }
            m = default;
            return false;
        }

        /// <summary>Poses for the moves above plus the crawl and the shield poses. Returns false for clips handled elsewhere.</summary>
        static bool TryMovePose(HumanoidSpec s, string clip, float t, out Pose pose)
        {
            pose = default;
            if (clip == "crawl") { pose = Crawl(s, t); return true; }
            if (clip == "block_tower") { pose = Guard(s, t, false); return true; }
            if (clip == "block_buckler") { pose = Guard(s, t, true); return true; }
            if (clip == "parry") { pose = ParryPose(s, t); return true; }
            // the bow's three shots: the standard draw, a quick snap shot and a long full draw
            if (clip == "shoot_b") { pose = Shoot(s, t, 0f, 0.16f, 0.28f, 4f, 0f); return true; }
            if (clip == "shoot_c") { pose = Shoot(s, t, 0f, 0.26f, 0.5f, 10f, 10f); return true; }
            if (!TryMove(clip, out Move m, out Move off, out bool dual)) return false;
            pose = MovePose(s, m, off, dual, t, clip == "whirl");
            return true;
        }

        static Pose MovePose(HumanoidSpec s, Move m, Move off, bool dual, float t, bool spin)
        {
            Pose p = Stand(s);
            float hy = p.Hip.y;
            float torso = TorsoLen(s);
            float coil = Coil(m, t), hit = Impact(m, t);

            float angle = Phased(m, t, m.Start, m.Wind, m.Hit, m.End, m.Rest);
            float lean = Phased(m, t, m.LeanStart, m.LeanWind, m.LeanHit, m.LeanEnd, 2f);
            float reach = Phased(m, t, m.ReachStart, m.Reach0, m.Reach1, m.Reach2, m.Reach0);
            if (m.Thrust) reach = Phased(m, t, m.ReachStart, m.Reach0 * 0.8f, m.Reach1, m.Reach2, m.Reach0);

            p.Lean = lean;
            p.WeaponAngle = angle;
            p.Hip = new Vector2(m.Lunge * hit - 5f * coil, hy - m.Crouch * coil);
            p.FootF = new Vector2(Mathf.Lerp(16f, 16f + m.Step, hit) - 4f * coil, Ankle);
            p.FootB = new Vector2(-14f - 8f * coil - 6f * hit, Ankle);
            p.Sway = Mathf.Lerp(-3f, 14f, coil) - hit * 30f;
            p.HeadOff = new Vector2(3f * hit - 2f * coil, 0f);

            // the shoulder as the drawing code will place it, so the hand stays attached to the arm
            float lr = p.Lean * Mathf.Deg2Rad;
            Vector2 up = new Vector2(Mathf.Sin(lr), Mathf.Cos(lr)), fwd = new Vector2(up.y, -up.x);
            Vector2 chest = p.Hip + up * torso;
            Vector2 shoulder = chest - up * 12f + fwd * 6f;

            float reachAngle = m.Thrust ? angle : angle * 0.55f + 10f;
            p.HandF = shoulder + Dir(reachAngle) * reach;

            if (m.TwoHands) p.HandB = p.HandF - Dir(angle) * 20f;                                      // the back hand holds the grip
            else p.HandB = p.Hip + new Vector2(-16f - 12f * hit + 6f * coil, torso * 0.52f + 6f * coil);   // balances like a fencer's free arm

            if (dual)
            {
                // the off hand swings its own sword, lagging a little behind
                float tb = Mathf.Clamp01(t - 0.04f);
                float angleB = Phased(off, tb, off.Start, off.Wind, off.Hit, off.End, off.Rest);
                p.WeaponAngleB = angleB;
                p.HasAngleB = true;
                Vector2 shoulderB = chest - up * 12f - fwd * 5f;
                p.HandB = shoulderB + Dir(angleB * 0.55f + 10f) * 60f;
            }
            return p;
        }

        /// <summary>Crawling under a low ceiling: low, forward-leaning, small alternating steps and swaying arms.</summary>
        static Pose Crawl(HumanoidSpec s, float t)
        {
            Pose p = Stand(s);
            float phase = t;
            float ph = phase * Mathf.PI * 2f;
            float hipY = Ankle + LegLen(s) * 0.56f;
            p.Hip = new Vector2(8f, hipY + Mathf.Abs(Mathf.Sin(ph)) * 3f);
            p.Lean = 44f;
            p.HeadOff = new Vector2(12f, -6f);
            p.FootF = FootCycle(phase, 15f, 7f) + new Vector2(10f, 0f);
            p.FootB = FootCycle(phase + 0.5f, 15f, 7f) + new Vector2(-8f, 0f);
            float lr = p.Lean * Mathf.Deg2Rad;
            Vector2 up = new Vector2(Mathf.Sin(lr), Mathf.Cos(lr)), fwd = new Vector2(up.y, -up.x);
            Vector2 shoulder = p.Hip + up * TorsoLen(s) - up * 12f + fwd * 6f;
            p.HandF = shoulder + new Vector2(30f + Mathf.Sin(ph) * 14f, -48f + Mathf.Max(0f, Mathf.Cos(ph)) * 10f);
            p.HandB = shoulder + new Vector2(18f - Mathf.Sin(ph) * 14f, -52f + Mathf.Max(0f, -Mathf.Cos(ph)) * 10f);
            p.WeaponAngle = 215f;      // the blade trails behind and down so it does not scrape the ceiling
            p.Sway = 8f + Mathf.Sin(ph) * 5f;
            p.Lift = 0f;
            return p;
        }

        /// <summary>Raising a shield: planted feet, hips low, the shield coming up from the hip to the front.</summary>
        static Pose Guard(HumanoidSpec s, float t, bool buckler)
        {
            Pose p = Stand(s);
            float hy = p.Hip.y;
            float u = EaseOut(Mathf.Clamp01(t / 0.7f));
            float settle = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.0f;
            p.ShieldUp = true;
            p.HasShieldPos = true;
            p.FootF = new Vector2(Mathf.Lerp(16f, buckler ? 22f : 28f, u), Ankle);
            p.FootB = new Vector2(Mathf.Lerp(-14f, buckler ? -18f : -24f, u), Ankle);
            p.WeaponAngle = Mathf.Lerp(55f, buckler ? 70f : -15f, u);
            p.Sway = -6f * u;
            if (buckler)
            {
                // an arm-length punch of the small shield
                p.Lean = Mathf.Lerp(2f, 6f, u);
                p.Hip = new Vector2(2f * u, hy - 3f * u);
                p.HandB = new Vector2(Mathf.Lerp(-18f, 64f, u), Mathf.Lerp(hy - 30f, hy + 46f, u));
                p.ShieldPos = p.HandB + new Vector2(14f, 2f);
            }
            else
            {
                p.Lean = Mathf.Lerp(2f, 10f, u);
                p.Hip = new Vector2(3f * u, hy - 7f * u);
                p.HandB = new Vector2(Mathf.Lerp(-18f, 44f, u), Mathf.Lerp(hy - 30f, hy + 28f, u));
                p.ShieldPos = new Vector2(Mathf.Lerp(24f, 62f, u), Mathf.Lerp(hy - 4f, hy + 20f, u));
            }
            p.HandF = new Vector2(Mathf.Lerp(24f, 14f, u), Mathf.Lerp(hy - 26f, hy + 4f, u));   // the weapon hand tucks in behind the shield
            p.Sway += settle;
            return p;
        }

        /// <summary>A parry: the shield snaps forward and rebounds.</summary>
        static Pose ParryPose(HumanoidSpec s, float t)
        {
            Pose p = Guard(s, 1f, false);
            float punch = t < 0.4f ? EaseOut(t / 0.4f) : Mathf.Lerp(1f, 0.55f, Smooth((t - 0.4f) / 0.6f));
            p.Lean = Mathf.Lerp(8f, 16f, punch);
            p.Hip.x += 6f * punch;
            p.FootF.x += 8f * punch;
            p.HandB += new Vector2(30f * punch, 4f * punch);
            p.ShieldPos = new Vector2(62f + 36f * punch, p.ShieldPos.y + 8f * punch);
            p.HandF = new Vector2(14f, p.Hip.y + 4f);
            return p;
        }
    }
}
