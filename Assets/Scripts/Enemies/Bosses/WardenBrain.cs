using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Enemies
{
    /// <summary>
    /// The Storm Warden: a bearded sky-king in white robes with a greatsword, fought over three health bars.
    /// Bar 1 is a grounded duellist: Overhead Slam, Wide Sweep, Leap Slam, a sword-swinging Charge, the Sky Thrust (jumps and plunges
    /// the blade into the floor), and chains of them with little rest. He blocks and dashes out of the way of blows when he is not
    /// committed to an attack. Bar 2 adds the Quake, a Sweep-Slam combo and the Dash Strike. Bar 3 gives him wings: he hovers and
    /// throws Feather Volleys, Spread Bursts and Storm Calls, and Lunges down at you. Every area is shown with a dotted warning first.
    /// </summary>
    public sealed class WardenBrain : BossBrain
    {
        static readonly Color Fire = new Color(1f, 0.55f, 0.2f);
        static readonly Color Storm = new Color(0.55f, 0.8f, 1f);

        const float HoverHeight = 4.2f;

        string _last = "";
        float _time, _dodgeReady, _blockReady;

        public override bool Flying => B != null && B.Phase >= 2;

        public override float Rest(int phase) => B.Rest(phase == 0 ? 0.85f : (phase == 1 ? 0.6f : 0.5f));

        public override Vector2 IdleVelocity(float dt)
        {
            if (!B.HasTarget) return Vector2.zero;
            _time += dt;
            B.FaceTarget();
            if (Flying)
            {
                Rect a = B.Arena;
                float x = Mathf.Clamp(B.TargetFeet.x + Mathf.Sin(_time * 0.9f) * 5.5f, a.xMin + 2f, a.xMax - 2f);
                Vector2 to = new Vector2(x, B.FloorY + HoverHeight + Mathf.Sin(_time * 1.7f) * 0.4f) - B.Position;
                return Vector2.ClampMagnitude(to * 2.6f, B.Spd(7.5f));
            }
            float dx = B.TargetFeet.x - B.Position.x;
            return Mathf.Abs(dx) > 2.4f ? new Vector2(Mathf.Sign(dx) * B.Spd(5.4f), 0f) : Vector2.zero;
        }

        // ------------------------------------------------------------------ choosing

        public override IEnumerator NextAttack(int phase) => Chain(phase);

        /// <summary>One attack, and often another right after it with no rest in between.</summary>
        IEnumerator Chain(int phase)
        {
            float chance = 0.3f + 0.17f * phase;
            int extras = 0;
            yield return Run(Pick(phase), phase);
            while (extras < 2 && !B.Dead && B.HasTarget && Random.value < chance)
            {
                extras++;
                B.FaceTarget();
                yield return Run(Pick(phase), phase);
            }
        }

        string Pick(int phase)
        {
            float d = Mathf.Abs(B.TargetFeet.x - B.Position.x);
            var o = new List<string>();
            if (phase >= 2)
            {
                o.Add("volley"); o.Add("spread"); o.Add("lunge"); o.Add("storm");
                if (d < 5f) o.Add("lunge");
            }
            else
            {
                if (d < 6.5f) { o.Add("slam"); o.Add("sweep"); }
                if (d > 3.5f) o.Add("leap");
                if (d > 4.5f) { o.Add("charge"); if (d > 8f) o.Add("charge"); }
                o.Add("skythrust");
                if (phase >= 1) { o.Add("quake"); o.Add("dash"); if (d < 10f) o.Add("dash"); o.Add("combo"); }
            }
            if (o.Count > 1) o.RemoveAll(x => x == _last);
            string pick = o[Random.Range(0, o.Count)];
            _last = pick;
            return pick;
        }

        IEnumerator Run(string pick, int phase)
        {
            switch (pick)
            {
                case "sweep": return Sweep(1f);
                case "leap": return LeapSlam(phase);
                case "quake": return Quake(phase);
                case "combo": return Combo();
                case "charge": return Charge(phase);
                case "skythrust": return SkyThrust(phase);
                case "dash": return DashStrike(phase);
                case "volley": return FeatherVolley(phase);
                case "spread": return SpreadBurst(phase);
                case "lunge": return AerialLunge(phase);
                case "storm": return StormCall(phase);
                default: return Slam(1f);
            }
        }

        public override IEnumerator PhaseShout(int phase)
        {
            B.SetVelocity(Vector2.zero);
            B.PlayOnce("attack", 0.6f);
            yield return B.WaitSeconds(0.35f);
            Vector2 at = B.Position;
            SpawnWaves(at + new Vector2(0f, 0.2f), 0.8f);
            B.StrikeCircle(at + Vector2.up * 0.4f, 2.8f, 0.8f, new Vector2(10f, 8f), phase >= 2 ? Storm : Fire);
            Impact(at, 2.4f);
            if (phase >= 2)
            {
                // wings open and he climbs into the air
                Vector2 from = B.Position;
                Vector2 to = new Vector2(Mathf.Clamp(B.TargetFeet.x, B.Arena.xMin + 3f, B.Arena.xMax - 3f), B.FloorY + HoverHeight);
                for (float t = 0f; t < 1.1f; t += Time.deltaTime)
                {
                    float s = t / 1.1f;
                    B.TeleportTo(Vector2.Lerp(from, to, s * s * (3f - 2f * s)));
                    yield return null;
                }
            }
            yield return B.WaitSeconds(0.6f);
        }

        // ------------------------------------------------------------------ reactions

        /// <summary>When he is not mid-attack he blocks some blows (heavily reduced) and slips others with a dash.</summary>
        public override HitResult? OnIncomingHit(ref DamageInfo info, bool busy)
        {
            if (busy || B.Reacting || B.Stunned) return null;
            if ((info.Tags & (DamageTags.Hazard | DamageTags.Dot | DamageTags.Unblockable)) != 0) return null;
            int phase = B.Phase;
            float roll = Random.value;
            float dodge = 0.3f + 0.08f * phase;
            float block = 0.3f;
            if (roll < dodge && Time.time >= _dodgeReady)
            {
                if (B.React(Dodge(info.SourcePosition, phase)))
                {
                    _dodgeReady = Time.time + 2.2f - 0.3f * phase;
                    return HitResult.Ignored;
                }
            }
            else if (roll < dodge + block && Time.time >= _blockReady)
            {
                if (B.React(Block()))
                {
                    _blockReady = Time.time + 0.5f;
                    info.Amount *= 0.12f;
                    info.Knockback = Vector2.zero;
                    info.Stagger = 0f;
                    return HitResult.Blocked;
                }
            }
            return null;
        }

        IEnumerator Block()
        {
            B.SetVelocity(Vector2.zero);
            B.FaceTarget();
            B.PlayOnce("block", 0.5f);
            Roughlike.Utils.Sfx.PlayAt(Roughlike.Utils.SfxId.Block, B.Position + Vector2.up * 1.5f, 0.8f);
            Roughlike.Core.EventBus<ExplosionEvent>.Raise(new ExplosionEvent { Cosmetic = true, Position = B.Position + new Vector2(B.Facing * 1.6f, 1.8f), Radius = 0.9f, Colour = Storm });
            yield return B.WaitSeconds(0.4f);
            // a clean block is answered with a quick sweep
            if (!Flying && B.HasTarget && Mathf.Abs(B.TargetFeet.x - B.Position.x) < 6f && Random.value < 0.4f)
                yield return Sweep(0.55f);
        }

        IEnumerator Dodge(Vector2 from, int phase)
        {
            Rect a = B.Arena;
            float awayDir = B.Position.x >= from.x ? 1f : -1f;
            // cornered: dash through to the other side instead
            if ((awayDir > 0f && B.Position.x > a.xMax - 4f) || (awayDir < 0f && B.Position.x < a.xMin + 4f)) awayDir = -awayDir;
            B.SetFacing(awayDir > 0f ? -1 : 1);
            B.PlayOnce("dash", 0.3f);
            Roughlike.Utils.Sfx.PlayAt(Roughlike.Utils.SfxId.Roll, B.Position + Vector2.up, 0.7f);
            for (float t = 0f; t < 0.26f; t += Time.deltaTime)
            {
                B.SetAlpha(0.45f);
                B.SetVelocity(new Vector2(awayDir * B.Spd(26f), 0f));
                yield return null;
            }
            B.SetAlpha(1f);
            B.SetVelocity(Vector2.zero);
            B.FaceTarget();
            // from the second bar on, a dodge is followed by a counter
            if (phase == 1 && B.HasTarget && Mathf.Abs(B.TargetFeet.x - B.Position.x) < 11f && Random.value < 0.5f)
                yield return DashStrike(phase);
            else yield return B.WaitSeconds(0.15f);
        }

        // ------------------------------------------------------------------ grounded attacks

        /// <summary>Raises the sword, brings it down: a box in front of him, then two shockwaves race along the floor.</summary>
        IEnumerator Slam(float speed)
        {
            B.SetVelocity(Vector2.zero);
            B.FaceTarget();
            float tel = B.Tel(0.9f) * speed;
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
            yield return B.WaitSeconds(0.25f);
            B.SetParryable(false);
            yield return B.WaitSeconds(0.45f * speed);
        }

        /// <summary>A very wide horizontal cut in front of him. Parry it and he is stunned.</summary>
        IEnumerator Sweep(float speed)
        {
            B.SetVelocity(Vector2.zero);
            B.FaceTarget();
            float tel = B.Tel(0.8f) * speed;
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
            yield return B.WaitSeconds(0.25f);
            B.SetParryable(false);
            yield return B.WaitSeconds(0.4f * speed);
        }

        /// <summary>Jumps to where you were standing and lands in a blast. A long, punishable recovery follows.</summary>
        IEnumerator LeapSlam(int phase)
        {
            B.SetVelocity(Vector2.zero);
            Rect a = B.Arena;
            float tel = B.Tel(0.9f);
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

            Vector2 from = B.Position;
            B.SetFacing(landing.x >= from.x ? 1 : -1);
            B.PlayOnce("attack", 0.7f);
            const float air = 0.6f;
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
            yield return B.WaitSeconds(0.9f);   // stuck in the floor for a moment: the moment to hit him
        }

        /// <summary>A line is drawn along the floor, then he runs down it with the sword swinging, hurting everything in his path.</summary>
        IEnumerator Charge(int phase)
        {
            B.SetVelocity(Vector2.zero);
            B.FaceTarget();
            Rect a = B.Arena;
            int dir = B.Facing;
            float tel = B.Tel(0.75f);
            float reach = Mathf.Clamp(Mathf.Abs(B.TargetFeet.x - B.Position.x) + 6f, 10f, 22f);
            Vector2 centre = B.Position + new Vector2(dir * reach * 0.5f, 1.3f);
            var box = B.ShowBox(centre, new Vector2(reach, 2.6f), tel, 0f, true);
            for (float t = 0f; t < tel; t += Time.deltaTime)
            {
                B.Scrub("telegraph", t / tel);
                yield return null;
            }
            box.Hide();

            float speed = B.Spd(15f + phase * 1.5f);
            float run = Mathf.Min(reach / speed, 1.5f), nextHit = 0f;
            Roughlike.Utils.Sfx.PlayAt(Roughlike.Utils.SfxId.BossCharge, B.Position, 0.8f);
            B.Loop("charge");
            for (float t = 0f; t < run; t += Time.deltaTime)
            {
                float x = B.Position.x;
                if ((dir > 0 && x > a.xMax - 2.5f) || (dir < 0 && x < a.xMin + 2.5f)) break;
                B.SetVelocity(new Vector2(dir * speed, 0f));
                if (t >= nextHit)
                {
                    nextHit = t + 0.22f;
                    B.StrikeBox(B.Position + new Vector2(dir * 1.7f, 1.6f), new Vector2(4.4f, 3.2f), 1.0f, new Vector2(12f, 6f));
                }
                yield return null;
            }
            B.SetVelocity(Vector2.zero);

            // the run ends in a finishing cut
            B.PlayOnce("attack", 0.4f);
            B.StrikeBox(B.Position + new Vector2(dir * 3.0f, 1.5f), new Vector2(7f, 3f), 1.3f, new Vector2(12f, 6f));
            yield return B.WaitSeconds(0.7f);
        }

        /// <summary>Jumps high, hangs for a moment while a ring marks where you stand, then plunges the sword into the floor.</summary>
        IEnumerator SkyThrust(int phase)
        {
            B.SetVelocity(Vector2.zero);
            B.FaceTarget();
            Rect a = B.Arena;
            float crouch = B.Tel(0.3f);
            for (float t = 0f; t < crouch; t += Time.deltaTime) { B.Scrub("sky", 0f); yield return null; }

            Vector2 from = B.Position;
            float apexX = Mathf.Clamp(B.TargetFeet.x, a.xMin + 2.5f, a.xMax - 2.5f);
            Vector2 apex = new Vector2(Mathf.Lerp(from.x, apexX, 0.7f), B.FloorY + 7.2f);
            const float rise = 0.5f;
            Roughlike.Utils.Sfx.PlayAt(Roughlike.Utils.SfxId.Jump, from, 0.8f);
            for (float t = 0f; t < rise; t += Time.deltaTime)
            {
                float s = t / rise;
                B.Scrub("sky", 0.4f);
                B.TeleportTo(Vector2.Lerp(from, apex, 1f - (1f - s) * (1f - s)));
                yield return null;
            }

            float tel = B.Tel(0.75f);
            float radius = 3.3f;
            Vector2 landing = new Vector2(Mathf.Clamp(B.TargetFeet.x, a.xMin + 2.5f, a.xMax - 2.5f), B.FloorY);
            var ring = B.ShowCircle(landing + Vector2.up * 0.1f, radius, tel, true);
            for (float t = 0f; t < tel; t += Time.deltaTime)
            {
                B.Scrub("sky", 0.75f);
                if (t / tel < 0.7f)
                {
                    landing = new Vector2(Mathf.Clamp(B.TargetFeet.x, a.xMin + 2.5f, a.xMax - 2.5f), B.FloorY);
                    ring.transform.position = landing + Vector2.up * 0.1f;
                    B.TeleportTo(new Vector2(Mathf.MoveTowards(B.Position.x, landing.x, Time.deltaTime * 9f), apex.y));
                }
                else B.TeleportTo(new Vector2(B.Position.x, apex.y));
                yield return null;
            }

            Vector2 top = B.Position;
            const float plunge = 0.17f;
            for (float t = 0f; t < plunge; t += Time.deltaTime)
            {
                float s = t / plunge;
                B.Scrub("sky", 1f);
                B.TeleportTo(Vector2.Lerp(top, landing, s * s));
                yield return null;
            }
            B.TeleportTo(landing);
            ring.Hide();
            B.StrikeCircle(landing + Vector2.up * 0.4f, radius, 1.6f, new Vector2(10f, 10f), Fire);
            SpawnWaves(landing + Vector2.up * 0.3f, 0.8f);
            Impact(landing, 2.6f);
            B.Expose(1.0f);
            yield return B.WaitSeconds(1.0f);   // blade stuck in the floor
        }

        /// <summary>Blasts erupt one after another in a line, marching toward and past you.</summary>
        IEnumerator Quake(int phase)
        {
            B.SetVelocity(Vector2.zero);
            B.FaceTarget();
            Rect a = B.Arena;
            int count = 6 + (phase >= 1 ? 2 : 0);
            float tel = B.Tel(0.75f);
            float gap = 3.1f;
            var points = new List<Vector2>();
            for (int i = 0; i < count; i++)
            {
                float x = B.Position.x + B.Facing * (2.6f + i * gap);
                if (x < a.xMin + 1.5f || x > a.xMax - 1.5f) break;
                points.Add(new Vector2(x, B.FloorY + 0.1f));
            }
            var marks = new List<TelegraphIndicator>();
            for (int i = 0; i < points.Count; i++) marks.Add(B.ShowCircle(points[i], 1.7f, tel + i * 0.32f, true));

            B.PlayOnce("telegraph", tel);
            float clock = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                float at = tel + i * 0.32f;
                while (clock < at) { clock += Time.deltaTime; B.Scrub("telegraph", Mathf.Clamp01(clock / tel)); yield return null; }
                marks[i].Hide();
                B.StrikeCircle(points[i] + Vector2.up * 0.5f, 1.8f, 1.2f, new Vector2(4f, 12f), Fire);
                if (i == 0) B.PlayOnce("attack", 0.5f);
            }
            yield return B.WaitSeconds(0.6f);
        }

        /// <summary>Sweep straight into a slam, with less warning than usual.</summary>
        IEnumerator Combo()
        {
            yield return Sweep(0.65f);
            yield return Slam(0.65f);
        }

        /// <summary>Marks a line to where you stand and blinks along it, cutting as he arrives. Often a second dash back follows.</summary>
        IEnumerator DashStrike(int phase)
        {
            int passes = phase >= 1 && Random.value < 0.55f ? 2 : 1;
            for (int i = 0; i < passes && B.HasTarget; i++)
            {
                B.SetVelocity(Vector2.zero);
                B.FaceTarget();
                Rect a = B.Arena;
                float tel = B.Tel(i == 0 ? 0.55f : 0.35f);
                float y = B.Position.y;
                float endX = Mathf.Clamp(B.TargetFeet.x + B.Facing * 2.4f, a.xMin + 2f, a.xMax - 2f);
                float startX = B.Position.x;
                Vector2 centre = new Vector2((startX + endX) * 0.5f, y + 1.3f);
                var box = B.ShowBox(centre, new Vector2(Mathf.Max(2f, Mathf.Abs(endX - startX)), 1.8f), tel, 0f, true);
                for (float t = 0f; t < tel; t += Time.deltaTime)
                {
                    B.Scrub("telegraph", t / tel * 0.9f);
                    yield return null;
                }
                box.Hide();

                B.PlayOnce("dash", 0.3f);
                Roughlike.Utils.Sfx.PlayAt(Roughlike.Utils.SfxId.Roll, B.Position, 0.9f);
                const float dash = 0.17f;
                for (float t = 0f; t < dash; t += Time.deltaTime)
                {
                    float s = t / dash;
                    B.TeleportTo(new Vector2(Mathf.Lerp(startX, endX, s), y));
                    yield return null;
                }
                B.TeleportTo(new Vector2(endX, y));
                B.PlayOnce("attack", 0.35f);
                B.StrikeBox(new Vector2((startX + endX) * 0.5f, y + 1.5f), new Vector2(Mathf.Abs(endX - startX) + 3f, 2.6f), 1.3f, new Vector2(11f, 6f));
                yield return B.WaitSeconds(0.3f);
            }
            yield return B.WaitSeconds(0.35f);
        }

        // ------------------------------------------------------------------ winged attacks

        /// <summary>A fan of feathers thrown at you, three times, each fan aimed afresh. The aim line shows the middle shot.</summary>
        IEnumerator FeatherVolley(int phase)
        {
            B.SetVelocity(Vector2.zero);
            for (int burst = 0; burst < 3 && B.HasTarget; burst++)
            {
                B.FaceTarget();
                float tel = B.Tel(burst == 0 ? 0.7f : 0.4f);
                Vector2 from = B.Position + new Vector2(B.Facing * 1.2f, 2.3f);
                Vector2 aim = B.TargetCentre - from;
                float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                var line = B.ShowBox(from + aim.normalized * 5f, new Vector2(10f, 0.5f), tel, angle, true);
                for (float t = 0f; t < tel; t += Time.deltaTime)
                {
                    B.Scrub("telegraph", t / tel);
                    yield return null;
                }
                line.Hide();
                B.PlayOnce("attack", 0.35f);
                Roughlike.Utils.Sfx.PlayAt(Roughlike.Utils.SfxId.EnemyShoot, from, 0.8f);
                for (int i = -2; i <= 2; i++)
                {
                    float a = (angle + i * 11f) * Mathf.Deg2Rad;
                    B.Shoot(from, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * B.Spd(11.5f), 0.75f, Storm, 0.28f, 4f);
                }
                yield return B.WaitSeconds(0.1f);
            }
            yield return B.WaitSeconds(0.4f);
        }

        /// <summary>Rings of bolts spreading out from him, the second ring offset to fill the first one's gaps.</summary>
        IEnumerator SpreadBurst(int phase)
        {
            B.SetVelocity(Vector2.zero);
            float tel = B.Tel(0.85f);
            var ring = B.ShowCircle(B.Position + Vector2.up * 1.5f, 3.2f, tel, true);
            for (float t = 0f; t < tel; t += Time.deltaTime)
            {
                B.Scrub("telegraph", t / tel);
                yield return null;
            }
            ring.Hide();
            Roughlike.Utils.Sfx.PlayAt(Roughlike.Utils.SfxId.EnemyShoot, B.Position, 0.9f);
            const int count = 14;
            for (int r = 0; r < 3; r++)
            {
                B.PlayOnce("attack", 0.3f);
                Vector2 from = B.Position + Vector2.up * 1.8f;
                float offset = r * (180f / count) + Random.Range(0f, 4f);
                for (int i = 0; i < count; i++)
                {
                    float a = (offset + i * (360f / count)) * Mathf.Deg2Rad;
                    B.Shoot(from, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * B.Spd(7.2f + r * 0.8f), 0.7f, Storm, 0.28f, 4.5f);
                }
                yield return B.WaitSeconds(0.5f);
            }
            yield return B.WaitSeconds(0.3f);
        }

        /// <summary>A diagonal line across the arena, then he dives down it into you, landing on the floor where he can be hit.</summary>
        IEnumerator AerialLunge(int phase)
        {
            B.SetVelocity(Vector2.zero);
            Rect a = B.Arena;
            float tel = B.Tel(0.85f);
            Vector2 start = B.Position;
            Vector2 end = LungeEnd(start, a);
            var line = B.ShowBox((start + end) * 0.5f + Vector2.up * 0.8f, new Vector2((end - start).magnitude, 1.9f), tel, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg, true);
            for (float t = 0f; t < tel; t += Time.deltaTime)
            {
                B.Scrub("telegraph", t / tel);
                if (t / tel < 0.6f)
                {
                    end = LungeEnd(start, a);
                    line.transform.position = (Vector3)((start + end) * 0.5f + Vector2.up * 0.8f);
                    line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg);
                }
                yield return null;
            }
            line.Hide();

            B.SetFacing(end.x >= start.x ? 1 : -1);
            B.PlayOnce("dash", 0.4f);
            Roughlike.Utils.Sfx.PlayAt(Roughlike.Utils.SfxId.BossCharge, start, 0.9f);
            float dist = (end - start).magnitude;
            float dur = Mathf.Max(0.2f, dist / B.Spd(30f)), next = 0f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                Vector2 p = Vector2.Lerp(start, end, t / dur);
                B.TeleportTo(p);
                if (t >= next)
                {
                    next = t + 0.08f;
                    B.StrikeBox(p + Vector2.up * 1.4f, new Vector2(3.8f, 3.4f), 1.3f, new Vector2(11f, 7f));
                }
                yield return null;
            }
            B.TeleportTo(end);
            B.StrikeCircle(end + Vector2.up * 0.4f, 2.8f, 1.3f, new Vector2(9f, 9f), Storm);
            Impact(end, 2.2f);
            B.Expose(1.4f);
            B.PlayOnce("hurt", 0.3f);
            yield return B.WaitSeconds(1.2f);   // down on the floor: the moment to hit him
        }

        Vector2 LungeEnd(Vector2 from, Rect a)
        {
            float x = Mathf.Clamp(B.TargetFeet.x + Mathf.Sign(B.TargetFeet.x - from.x) * 2f, a.xMin + 2f, a.xMax - 2f);
            return new Vector2(x, B.FloorY + 0.2f);
        }

        /// <summary>Lightning marked on the floor around you, wave after wave.</summary>
        IEnumerator StormCall(int phase)
        {
            B.SetVelocity(Vector2.zero);
            Rect a = B.Arena;
            B.PlayOnce("telegraph", 0.6f);
            for (int wave = 0; wave < 3 && B.HasTarget; wave++)
            {
                float tel = B.Tel(wave == 0 ? 0.95f : 0.7f);
                float px = B.TargetFeet.x;
                var xs = new List<float> { px, px - 4.6f, px + 4.6f };
                if (wave == 2) { xs.Add(px - 9f); xs.Add(px + 9f); }
                var marks = new List<TelegraphIndicator>();
                var pts = new List<Vector2>();
                foreach (float x in xs)
                {
                    if (x < a.xMin + 1.2f || x > a.xMax - 1.2f) continue;
                    var p = new Vector2(x, B.FloorY + 0.1f);
                    pts.Add(p);
                    marks.Add(B.ShowCircle(p, 1.6f, tel, true));
                }
                for (float t = 0f; t < tel; t += Time.deltaTime) yield return null;
                for (int i = 0; i < pts.Count; i++)
                {
                    marks[i].Hide();
                    B.StrikeCircle(pts[i] + Vector2.up * 0.5f, 1.7f, 1.2f, new Vector2(3f, 12f), Storm);
                    B.StrikeCircle(pts[i] + Vector2.up * 2.8f, 1.5f, 1.0f, new Vector2(3f, 8f), Storm);
                }
                if (pts.Count > 0) Roughlike.Combat.ScreenShake.Shake(0.7f, Vector2.up);
                yield return B.WaitSeconds(0.2f);
            }
            yield return B.WaitSeconds(0.5f);
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
