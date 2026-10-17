using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Enemies
{
    /// <summary>
    /// The Rust Warden: a towering knight with a greatsword whose blows leave danger areas on the floor. His moves:
    /// Overhead Slam (a crushing blow plus shockwaves running along the floor), Wide Sweep (a huge arc you can parry),
    /// Leap Slam (jumps onto your position and lands in a blast), Quake (the sword plunged, blasts erupting in a line toward you)
    /// and, in the last phase, a sweep-into-slam combo. Every area is shown with a dotted warning first.
    /// </summary>
    public sealed class WardenBrain : BossBrain
    {
        static readonly Color Fire = new Color(1f, 0.55f, 0.2f);

        string _last = "";

        public override float Rest(int phase) => B.Rest(phase == 0 ? 1.5f : (phase == 1 ? 1.1f : 0.8f));

        public override Vector2 IdleVelocity(float dt)
        {
            if (!B.HasTarget) return Vector2.zero;
            B.FaceTarget();
            float dx = B.TargetFeet.x - B.Position.x;
            return Mathf.Abs(dx) > 3.0f ? new Vector2(Mathf.Sign(dx) * B.Spd(3.4f), 0f) : Vector2.zero;
        }

        public override IEnumerator NextAttack(int phase)
        {
            float d = Mathf.Abs(B.TargetFeet.x - B.Position.x);
            var options = new List<string>();
            if (d < 6.5f) { options.Add("slam"); options.Add("sweep"); }
            if (d > 3.5f) options.Add("leap");
            if (phase >= 1) options.Add("quake");
            if (phase >= 2) options.Add("combo");
            if (options.Count == 0) options.Add("slam");
            if (options.Count > 1) options.Remove(_last);
            string pick = options[Random.Range(0, options.Count)];
            _last = pick;
            switch (pick)
            {
                case "sweep": return Sweep(1f);
                case "leap": return LeapSlam(phase);
                case "quake": return Quake(phase);
                case "combo": return Combo();
                default: return Slam(1f);
            }
        }

        public override IEnumerator PhaseShout(int phase)
        {
            B.SetVelocity(Vector2.zero);
            B.PlayOnce("attack", 0.6f);
            yield return B.WaitSeconds(0.35f);
            SpawnWaves(B.Position + new Vector2(0f, 0.2f), 0.8f);
            B.StrikeCircle(B.Position + Vector2.up * 0.4f, 2.6f, 0.8f, new Vector2(10f, 8f), Fire);
            yield return B.WaitSeconds(0.9f);
        }

        // ------------------------------------------------------------------ attacks

        /// <summary>Raises the sword, brings it down: a box in front of him, then two shockwaves race along the floor.</summary>
        IEnumerator Slam(float speed)
        {
            B.SetVelocity(Vector2.zero);
            B.FaceTarget();
            float tel = B.Tel(1.05f) * speed;
            Vector2 centre = B.Position + new Vector2(B.Facing * 2.7f, 1.8f);
            var size = new Vector2(4.6f, 3.8f);
            var box = B.ShowBox(centre, size, tel, 0f, false);
            for (float t = 0f; t < tel; t += Time.deltaTime)
            {
                B.Scrub("telegraph", t / tel);
                B.SetParryable(t / tel > 0.8f);
                yield return null;
            }
            box.Hide();

            B.PlayOnce("attack", 0.5f);
            B.StrikeBox(centre, size, 1.4f, new Vector2(10f, 7f));
            SpawnWaves(B.Position + new Vector2(B.Facing * 3.1f, 0.3f), 0.8f);
            Impact(B.Position + new Vector2(B.Facing * 3.1f, 0.3f), 1.8f);
            yield return B.WaitSeconds(0.3f);
            B.SetParryable(false);
            yield return B.WaitSeconds(0.7f * speed);
        }

        /// <summary>A very wide horizontal cut in front of him. Parry it and he is stunned.</summary>
        IEnumerator Sweep(float speed)
        {
            B.SetVelocity(Vector2.zero);
            B.FaceTarget();
            float tel = B.Tel(0.95f) * speed;
            Vector2 centre = B.Position + new Vector2(B.Facing * 3.3f, 1.5f);
            var size = new Vector2(8.6f, 2.8f);
            var box = B.ShowBox(centre, size, tel, 0f, false);
            for (float t = 0f; t < tel; t += Time.deltaTime)
            {
                B.Scrub("telegraph", t / tel);
                B.SetParryable(t / tel > 0.75f);
                yield return null;
            }
            box.Hide();

            B.PlayOnce("attack", 0.5f);
            B.StrikeBox(centre, size, 1.25f, new Vector2(13f, 6f));
            yield return B.WaitSeconds(0.3f);
            B.SetParryable(false);
            yield return B.WaitSeconds(0.65f * speed);
        }

        /// <summary>Jumps to where you were standing and lands in a blast. A long, punishable recovery follows.</summary>
        IEnumerator LeapSlam(int phase)
        {
            B.SetVelocity(Vector2.zero);
            Rect a = B.Arena;
            float tel = B.Tel(1.0f);
            float radius = 3.7f;
            Vector2 landing = new Vector2(B.TargetFeet.x, B.FloorY);
            var ring = B.ShowCircle(landing + Vector2.up * 0.1f, radius, tel, true);
            for (float t = 0f; t < tel; t += Time.deltaTime)
            {
                B.Scrub("telegraph", t / tel);
                if (t / tel < 0.65f)
                {
                    landing = new Vector2(Mathf.Clamp(B.TargetFeet.x, a.xMin + 2.5f, a.xMax - 2.5f), B.FloorY);
                    ring.transform.position = landing + Vector2.up * 0.1f;
                }
                yield return null;
            }

            // the jump itself: an arc over about 0.7 seconds
            Vector2 from = B.Position;
            B.SetFacing(landing.x >= from.x ? 1 : -1);
            B.PlayOnce("attack", 0.7f);
            const float air = 0.7f;
            for (float t = 0f; t < air; t += Time.deltaTime)
            {
                float s = t / air;
                Vector2 p = Vector2.Lerp(from, landing, s);
                p.y += Mathf.Sin(s * Mathf.PI) * 5.5f;
                B.TeleportTo(p);
                yield return null;
            }
            B.TeleportTo(landing);
            ring.Hide();
            B.StrikeCircle(landing + Vector2.up * 0.4f, radius, 1.5f, new Vector2(10f, 9f), Fire);
            SpawnWaves(landing + Vector2.up * 0.3f, 0.7f);
            Impact(landing, 2.4f);
            yield return B.WaitSeconds(1.2f);   // stuck in the floor for a moment: the moment to hit him
        }

        /// <summary>Blasts erupt one after another in a line, marching toward and past you.</summary>
        IEnumerator Quake(int phase)
        {
            B.SetVelocity(Vector2.zero);
            B.FaceTarget();
            Rect a = B.Arena;
            int count = 6 + (phase >= 2 ? 2 : 0);
            float tel = B.Tel(0.85f);
            float gap = 3.1f;
            var points = new List<Vector2>();
            for (int i = 0; i < count; i++)
            {
                float x = B.Position.x + B.Facing * (2.6f + i * gap);
                if (x < a.xMin + 1.5f || x > a.xMax - 1.5f) break;
                points.Add(new Vector2(x, B.FloorY + 0.1f));
            }
            var marks = new List<TelegraphIndicator>();
            for (int i = 0; i < points.Count; i++) marks.Add(B.ShowCircle(points[i], 1.7f, tel + i * 0.4f, true));

            B.PlayOnce("telegraph", tel);
            float clock = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                float at = tel + i * 0.4f;
                while (clock < at) { clock += Time.deltaTime; B.Scrub("telegraph", Mathf.Clamp01(clock / tel)); yield return null; }
                marks[i].Hide();
                B.StrikeCircle(points[i] + Vector2.up * 0.5f, 1.8f, 1.2f, new Vector2(4f, 12f), Fire);
                if (i == 0) B.PlayOnce("attack", 0.5f);
            }
            yield return B.WaitSeconds(0.9f);
        }

        /// <summary>Sweep straight into a slam, with less warning than usual.</summary>
        IEnumerator Combo()
        {
            yield return Sweep(0.7f);
            yield return Slam(0.7f);
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>Two low shockwaves racing left and right along the floor; jump them.</summary>
        void SpawnWaves(Vector2 origin, float mult)
        {
            for (int dir = -1; dir <= 1; dir += 2)
            {
                Vector2 pos = new Vector2(origin.x, B.FloorY + 0.72f);
                var wave = B.Shoot(pos, new Vector2(dir * B.Spd(8.6f), 0f), mult, Fire, 0.55f, 1.4f);
                wave.transform.localScale = new Vector3(1.2f, 0.7f, 1f);
            }
        }

        void Impact(Vector2 at, float radius)
        {
            Roughlike.Core.EventBus<Roughlike.Combat.ExplosionEvent>.Raise(new Roughlike.Combat.ExplosionEvent { Position = at + Vector2.up * 0.3f, Radius = radius, Colour = Fire });
            Roughlike.Combat.ScreenShake.Shake(0.9f, Vector2.up);
        }
    }
}
