using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Enemies
{
    /// <summary>
    /// The Hag of Bells: a floating witch who scatters magic around the room. Her moves:
    /// Scatter Volley (a ring of slow bolts flying every which way), Hex Rain (marked circles all over the floor that erupt),
    /// Soul Orbs (slow homing orbs you can parry back), Blink Slash (teleports beside you for a melee sweep) and, in the last
    /// phase, summoning drowned wretches. Everything is telegraphed with dotted warnings.
    /// </summary>
    public sealed class WitchBrain : BossBrain
    {
        static readonly Color Green = new Color(0.62f, 1f, 0.42f);
        static readonly Color Violet = new Color(0.85f, 0.5f, 1f);

        const float HoverHeight = 4.3f;

        readonly List<EnemyController> _minions = new List<EnemyController>();
        float _time;
        string _last = "";

        public override bool Flying => true;
        public int LiveMinions { get { _minions.RemoveAll(m => m == null || m.Health.IsDead); return _minions.Count; } }

        public override float Rest(int phase) => B.Rest(phase == 0 ? 1.7f : (phase == 1 ? 1.3f : 1.0f));

        Vector2 HoverPoint(float phaseTime)
        {
            Rect a = B.Arena;
            float x = a.center.x + Mathf.Sin(phaseTime * 0.55f) * a.width * 0.3f;
            float y = B.FloorY + HoverHeight + Mathf.Sin(phaseTime * 1.3f) * 0.45f;
            return new Vector2(x, y);
        }

        public override Vector2 IdleVelocity(float dt)
        {
            _time += dt;
            Vector2 to = HoverPoint(_time) - B.Position;
            B.FaceTarget();
            return Vector2.ClampMagnitude(to * 2.2f, B.Spd(5f));
        }

        Vector2 Staff => B.Position + new Vector2(B.Facing * 1.3f, 2.4f);

        public override IEnumerator NextAttack(int phase)
        {
            var options = new List<string> { "scatter", "rain", "orbs" };
            if (phase >= 1) options.Add("blink");
            if (phase >= 2) { options.Add("scatter"); if (D.minion != null && LiveMinions < 3) options.Add("summon"); }
            options.Remove(_last);
            string pick = options[Random.Range(0, options.Count)];
            _last = pick;
            switch (pick)
            {
                case "rain": return HexRain(phase);
                case "orbs": return SoulOrbs(phase);
                case "blink": return BlinkSlash(phase);
                case "summon": return Summon(phase);
                default: return Scatter(phase);
            }
        }

        public override IEnumerator PhaseShout(int phase)
        {
            B.SetVelocity(Vector2.zero);
            yield return Scatter(phase);
            yield return B.WaitSeconds(0.6f);
        }

        // ------------------------------------------------------------------ attacks

        /// <summary>A ring of bolts thrown out at random speeds and slightly random angles, so the gaps are never the same twice.</summary>
        IEnumerator Scatter(int phase)
        {
            B.SetVelocity(Vector2.zero);
            float tel = B.Tel(1.0f);
            var ring = B.ShowCircle(Staff, 2.6f, tel, true);
            for (float t = 0f; t < tel; t += Time.deltaTime)
            {
                B.Scrub("telegraph", t / tel);
                ring.transform.position = Staff;
                yield return null;
            }
            ring.Hide();
            B.PlayOnce("attack", 0.5f);

            int bursts = phase >= 2 ? 3 : (phase >= 1 ? 2 : 1);
            for (int b = 0; b < bursts; b++)
            {
                int n = 12 + 4 * phase;
                float offset = Random.Range(0f, 360f);
                for (int i = 0; i < n; i++)
                {
                    float angle = offset + i * (360f / n) + Random.Range(-9f, 9f);
                    float speed = B.Spd(Random.Range(5.2f, 8.6f));
                    Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                    B.Shoot(Staff, dir * speed, 0.7f, Green, 0.22f, 4.8f);
                }
                yield return B.WaitSeconds(0.55f);
            }
            yield return B.WaitSeconds(0.4f);
        }

        /// <summary>Circles appear across the floor, two of them right under you, and erupt one after another.</summary>
        IEnumerator HexRain(int phase)
        {
            B.SetVelocity(Vector2.zero);
            B.PlayOnce("telegraph", 0.9f);
            Rect a = B.Arena;
            int count = 5 + phase * 2;
            float tel = B.Tel(1.15f);
            var points = new List<Vector2>();
            Vector2 player = B.TargetFeet;
            for (int i = 0; i < count; i++)
            {
                float x = i < 2 ? player.x + (i == 0 ? -0.5f : 0.9f) : Random.Range(a.xMin + 2f, a.xMax - 2f);
                points.Add(new Vector2(Mathf.Clamp(x, a.xMin + 1.8f, a.xMax - 1.8f), B.FloorY + 0.1f));
            }
            var marks = new List<TelegraphIndicator>();
            for (int i = 0; i < count; i++) marks.Add(B.ShowCircle(points[i], 1.65f, tel + i * 0.13f, true));

            float clock = 0f;
            for (int i = 0; i < count; i++)
            {
                float at = tel + i * 0.13f;
                while (clock < at) { clock += Time.deltaTime; B.Scrub("telegraph", Mathf.Clamp01(clock / tel)); yield return null; }
                marks[i].Hide();
                B.StrikeCircle(points[i], 1.75f, 1.0f, new Vector2(5f, 9f), Violet);
            }
            B.PlayOnce("attack", 0.4f);
            yield return B.WaitSeconds(0.7f);
        }

        /// <summary>Slow orbs that curve toward you for a few seconds. They can be parried back at her.</summary>
        IEnumerator SoulOrbs(int phase)
        {
            B.SetVelocity(Vector2.zero);
            float tel = B.Tel(0.9f);
            Vector2 aim = (B.TargetCentre - Staff);
            var line = B.ShowBox(Staff + aim.normalized * 3f, new Vector2(6f, 0.1f), tel, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, false);
            for (float t = 0f; t < tel; t += Time.deltaTime) { B.Scrub("telegraph", t / tel); yield return null; }
            line.Hide();
            B.PlayOnce("attack", 0.5f);

            int n = phase >= 2 ? 5 : 3;
            Vector2 baseDir = (B.TargetCentre - Staff).normalized;
            for (int i = 0; i < n; i++)
            {
                float spread = n == 1 ? 0f : Mathf.Lerp(-38f, 38f, i / (float)(n - 1));
                Vector2 dir = Quaternion.Euler(0f, 0f, spread) * baseDir;
                var orb = B.Shoot(Staff, dir * B.Spd(4.6f), 1.0f, Violet, 0.36f, 5f);
                orb.SetHoming(105f, 3.3f);
                yield return B.WaitSeconds(0.18f);
            }
            yield return B.WaitSeconds(0.9f);
        }

        /// <summary>Vanishes, reappears beside you and sweeps. Parry the sweep to stun her.</summary>
        IEnumerator BlinkSlash(int phase)
        {
            B.SetVelocity(Vector2.zero);
            yield return Fade(1f, 0f, 0.25f);

            Rect a = B.Arena;
            Vector2 feet = B.TargetFeet;
            float side = feet.x - a.xMin > a.xMax - feet.x ? -1f : 1f;       // appear on the roomier side
            Vector2 dest = new Vector2(Mathf.Clamp(feet.x + side * 2.6f, a.xMin + 2f, a.xMax - 2f), B.FloorY + 1.5f);
            B.TeleportTo(dest);
            B.SetFacing(-(int)side);
            yield return Fade(0f, 1f, 0.15f);

            float tel = B.Tel(0.85f);
            Vector2 centre = dest + new Vector2(B.Facing * 2.0f, 0.6f);
            var box = B.ShowBox(centre, new Vector2(4.0f, 2.8f), tel, 0f, false);
            for (float t = 0f; t < tel; t += Time.deltaTime) { B.Scrub("telegraph", t / tel); yield return null; }
            box.Hide();

            B.PlayOnce("attack", 0.45f);
            B.SetParryable(true);
            B.StrikeBox(centre, new Vector2(4.0f, 2.8f), 1.3f, new Vector2(10f, 6f));
            yield return B.WaitSeconds(0.25f);
            B.SetParryable(false);
            yield return B.WaitSeconds(0.45f);

            yield return Fade(1f, 0f, 0.2f);
            B.TeleportTo(HoverPoint(_time));
            yield return Fade(0f, 1f, 0.2f);
        }

        IEnumerator Summon(int phase)
        {
            B.SetVelocity(Vector2.zero);
            Rect a = B.Arena;
            float tel = B.Tel(1.1f);
            var spots = new[] { Mathf.Clamp(B.Position.x - 5.5f, a.xMin + 2f, a.xMax - 2f), Mathf.Clamp(B.Position.x + 5.5f, a.xMin + 2f, a.xMax - 2f) };
            var marks = new List<TelegraphIndicator>();
            foreach (float x in spots) marks.Add(B.ShowCircle(new Vector2(x, B.FloorY + 0.1f), 1.2f, tel, true));
            for (float t = 0f; t < tel; t += Time.deltaTime) { B.Scrub("telegraph", t / tel); yield return null; }
            foreach (var m in marks) m.Hide();
            B.PlayOnce("attack", 0.5f);
            foreach (float x in spots)
            {
                var minion = EnemyFactory.Build(D.minion, new Vector2(x, B.FloorY + 0.05f), true, B.Scaling);
                minion.Alert();
                _minions.Add(minion);
            }
            yield return B.WaitSeconds(1.0f);
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                B.SetAlpha(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            B.SetAlpha(to);
        }

        public override void OnDied()
        {
            foreach (var m in _minions)
                if (m != null && !m.Health.IsDead)
                    m.Health.TakeHit(new DamageInfo { Amount = 1_000_000f, SourceTeam = Team.Neutral });
            _minions.Clear();
        }
    }
}
