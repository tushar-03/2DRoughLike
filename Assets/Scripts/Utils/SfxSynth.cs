using System;
using UnityEngine;

namespace Roughlike.Utils
{
    /// <summary>
    /// Builds every sound effect from scratch: oscillators, noise, envelopes and one-pole filters. Each recipe is a handful of
    /// lines, so a sound is easy to retune; swap any of them for a recorded clip later by returning that clip from
    /// <see cref="Make"/>. Deterministic (fixed noise seed per sound).
    /// </summary>
    public static class SfxSynth
    {
        const int Rate = 44100;
        static System.Random _rng;

        enum Wave { Sine, Triangle, Saw, Square }

        // ------------------------------------------------------------------ primitives

        static float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);
        static float[] Buf(float seconds) => new float[Mathf.Max(1, Mathf.RoundToInt(seconds * Rate))];

        static float Osc(Wave w, float phase)
        {
            float p = phase - Mathf.Floor(phase);
            switch (w)
            {
                case Wave.Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;
                case Wave.Saw: return 2f * p - 1f;
                case Wave.Square: return p < 0.5f ? 1f : -1f;
                default: return Mathf.Sin(p * Mathf.PI * 2f);
            }
        }

        /// <summary>Attack then exponential decay, 0..1.</summary>
        static float Env(float t, float attack, float decayTau)
        {
            float a = attack <= 0f ? 1f : Mathf.Clamp01(t / attack);
            return a * Mathf.Exp(-Mathf.Max(0f, t - attack) / Mathf.Max(0.001f, decayTau));
        }

        /// <summary>Adds an oscillator that glides from f0 to f1 (exponentially) over its length.</summary>
        static void Tone(float[] b, float start, float dur, float f0, float f1, Wave wave, float gain, float attack, float tau, float vibratoHz = 0f, float vibratoDepth = 0f)
        {
            int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt(dur * Rate);
            float phase = 0f;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = i / (float)Rate, u = n <= 1 ? 0f : i / (float)(n - 1);
                float f = f0 * Mathf.Pow(Mathf.Max(1f, f1) / Mathf.Max(1f, f0), u);
                if (vibratoHz > 0f) f *= 1f + vibratoDepth * Mathf.Sin(t * vibratoHz * Mathf.PI * 2f);
                phase += f / Rate;
                b[s0 + i] += Osc(wave, phase) * gain * Env(t, attack, tau);
            }
        }

        static void Tone(float[] b, float start, float dur, float f, Wave wave, float gain, float attack, float tau) => Tone(b, start, dur, f, f, wave, gain, attack, tau);

        /// <summary>White noise with an envelope, optionally with a gliding one-pole low-pass (cutoff0 to cutoff1 Hz).</summary>
        static void NoiseBurst(float[] b, float start, float dur, float gain, float attack, float tau, float cutoff0 = 0f, float cutoff1 = 0f)
        {
            int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt(dur * Rate);
            float lp = 0f;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = i / (float)Rate, u = n <= 1 ? 0f : i / (float)(n - 1);
                float x = Noise();
                if (cutoff0 > 0f)
                {
                    float fc = Mathf.Lerp(cutoff0, cutoff1 <= 0f ? cutoff0 : cutoff1, u);
                    float a = 1f - Mathf.Exp(-2f * Mathf.PI * fc / Rate);
                    lp += a * (x - lp);
                    x = lp * 2.2f;
                }
                b[s0 + i] += x * gain * Env(t, attack, tau);
            }
        }

        /// <summary>A bell or struck metal: several inharmonic partials with their own decays.</summary>
        static void Metal(float[] b, float start, float f, float gain, float tau, float[] ratios = null)
        {
            ratios = ratios ?? new[] { 1f, 2.76f, 5.4f, 8.93f };
            for (int i = 0; i < ratios.Length; i++)
                Tone(b, start, tau * 5f, f * ratios[i], Wave.Sine, gain / (1f + i * 0.9f), 0.002f, tau / (1f + i * 0.5f));
        }

        static void HighPass(float[] b, float cutoff)
        {
            float a = Mathf.Exp(-2f * Mathf.PI * cutoff / Rate), px = 0f, py = 0f;
            for (int i = 0; i < b.Length; i++) { float x = b[i]; py = a * (py + x - px); px = x; b[i] = py; }
        }

        static void LowPass(float[] b, float cutoff)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate), y = 0f;
            for (int i = 0; i < b.Length; i++) { y += a * (b[i] - y); b[i] = y; }
        }

        static void Drive(float[] b, float amount)
        {
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * amount);
        }

        static AudioClip Finish(string name, float[] b, float peak = 0.85f, bool loop = false)
        {
            float max = 0.0001f;
            for (int i = 0; i < b.Length; i++) max = Mathf.Max(max, Mathf.Abs(b[i]));
            float k = peak / max;
            int fade = loop ? 0 : Mathf.Min(b.Length / 8, Rate / 100 * 3);
            for (int i = 0; i < b.Length; i++)
            {
                b[i] *= k;
                if (fade > 0 && i > b.Length - fade) b[i] *= (b.Length - i) / (float)fade;
            }
            var clip = AudioClip.Create("sfx_" + name, b.Length, 1, Rate, false);
            clip.SetData(b, 0);
            return clip;
        }

        // ------------------------------------------------------------------ recipes

        public static AudioClip Make(SfxId id)
        {
            _rng = new System.Random((int)id * 7919 + 13);
            float[] b;
            bool loop = false;
            switch (id)
            {
                case SfxId.Jump:
                    b = Buf(0.18f);
                    Tone(b, 0f, 0.16f, 260f, 560f, Wave.Triangle, 0.7f, 0.004f, 0.07f);
                    NoiseBurst(b, 0f, 0.12f, 0.12f, 0.01f, 0.05f, 900f, 2500f);
                    break;
                case SfxId.DoubleJump:
                    b = Buf(0.26f);
                    Tone(b, 0f, 0.2f, 380f, 900f, Wave.Triangle, 0.6f, 0.003f, 0.08f);
                    Tone(b, 0.05f, 0.2f, 1500f, 2200f, Wave.Sine, 0.25f, 0.003f, 0.06f);
                    break;
                case SfxId.WallJump:
                    b = Buf(0.2f);
                    Tone(b, 0f, 0.16f, 320f, 640f, Wave.Square, 0.25f, 0.003f, 0.06f);
                    NoiseBurst(b, 0f, 0.1f, 0.35f, 0.002f, 0.04f, 1500f, 4000f);
                    break;
                case SfxId.Land:
                    b = Buf(0.2f);
                    NoiseBurst(b, 0f, 0.14f, 0.7f, 0.001f, 0.045f, 700f, 200f);
                    Tone(b, 0f, 0.18f, 110f, 60f, Wave.Sine, 0.8f, 0.001f, 0.07f);
                    break;
                case SfxId.Roll:
                    b = Buf(0.34f);
                    NoiseBurst(b, 0f, 0.32f, 0.8f, 0.09f, 0.12f, 500f, 2400f);
                    LowPass(b, 5000f);
                    break;
                case SfxId.Footstep:
                    b = Buf(0.09f);
                    NoiseBurst(b, 0f, 0.07f, 0.8f, 0.001f, 0.022f, 900f, 350f);
                    Tone(b, 0f, 0.06f, 130f, 80f, Wave.Sine, 0.4f, 0.001f, 0.02f);
                    break;
                case SfxId.LedgeGrab:
                    b = Buf(0.14f);
                    NoiseBurst(b, 0f, 0.1f, 0.5f, 0.002f, 0.03f, 1800f, 600f);
                    break;

                // ---- combat
                case SfxId.SwingLight:
                    b = Buf(0.22f);
                    NoiseBurst(b, 0f, 0.2f, 0.9f, 0.04f, 0.07f, 1200f, 5200f);
                    HighPass(b, 500f);
                    break;
                case SfxId.SwingHeavy:
                    b = Buf(0.42f);
                    NoiseBurst(b, 0f, 0.4f, 1f, 0.12f, 0.14f, 400f, 3200f);
                    Tone(b, 0f, 0.3f, 140f, 60f, Wave.Sine, 0.3f, 0.05f, 0.14f);
                    break;
                case SfxId.SwingBow:
                    b = Buf(0.2f);
                    NoiseBurst(b, 0f, 0.15f, 0.4f, 0.05f, 0.05f, 900f, 2400f);
                    Tone(b, 0f, 0.18f, 160f, 190f, Wave.Triangle, 0.15f, 0.08f, 0.1f);
                    break;
                case SfxId.BowRelease:
                    b = Buf(0.4f);
                    Tone(b, 0f, 0.35f, 196f, 188f, Wave.Triangle, 0.8f, 0.001f, 0.09f, 36f, 0.012f);
                    Tone(b, 0f, 0.3f, 392f, 380f, Wave.Sine, 0.35f, 0.001f, 0.06f);
                    NoiseBurst(b, 0.005f, 0.12f, 0.3f, 0.001f, 0.03f, 4000f, 1500f);
                    break;
                case SfxId.Charge:
                    b = Buf(1.3f);
                    Tone(b, 0f, 1.2f, 140f, 520f, Wave.Triangle, 0.5f, 0.2f, 0.9f, 7f, 0.03f);
                    NoiseBurst(b, 0f, 1.1f, 0.12f, 0.3f, 0.6f, 600f, 3500f);
                    LowPass(b, 3500f);
                    break;
                case SfxId.Hit:
                    b = Buf(0.2f);
                    NoiseBurst(b, 0f, 0.13f, 0.9f, 0.001f, 0.04f, 3200f, 500f);
                    Tone(b, 0f, 0.14f, 190f, 70f, Wave.Sine, 0.8f, 0.001f, 0.05f);
                    break;
                case SfxId.HitHeavy:
                    b = Buf(0.36f);
                    NoiseBurst(b, 0f, 0.25f, 1f, 0.001f, 0.08f, 2200f, 250f);
                    Tone(b, 0f, 0.3f, 130f, 42f, Wave.Sine, 1f, 0.001f, 0.12f);
                    Tone(b, 0f, 0.2f, 260f, 90f, Wave.Triangle, 0.4f, 0.001f, 0.06f);
                    break;
                case SfxId.Crit:
                    b = Buf(0.6f);
                    NoiseBurst(b, 0f, 0.1f, 0.8f, 0.001f, 0.03f, 4000f, 800f);
                    Tone(b, 0f, 0.14f, 190f, 80f, Wave.Sine, 0.6f, 0.001f, 0.05f);
                    Metal(b, 0f, 880f, 0.5f, 0.18f);
                    break;
                case SfxId.Parry:
                    b = Buf(0.8f);
                    NoiseBurst(b, 0f, 0.05f, 0.8f, 0.001f, 0.012f, 7000f, 3000f);
                    Metal(b, 0f, 1180f, 0.9f, 0.16f, new[] { 1f, 1.41f, 2.04f, 2.7f, 3.2f });
                    Tone(b, 0f, 0.1f, 2400f, 1200f, Wave.Sine, 0.3f, 0.001f, 0.03f);
                    break;
                case SfxId.Block:
                    b = Buf(0.4f);
                    NoiseBurst(b, 0f, 0.07f, 0.8f, 0.001f, 0.02f, 2500f, 600f);
                    Metal(b, 0f, 330f, 0.7f, 0.07f, new[] { 1f, 1.52f, 2.3f });
                    Tone(b, 0f, 0.2f, 120f, 70f, Wave.Sine, 0.7f, 0.001f, 0.07f);
                    break;
                case SfxId.ShieldRaise:
                    b = Buf(0.16f);
                    NoiseBurst(b, 0f, 0.12f, 0.6f, 0.03f, 0.05f, 1200f, 2600f);
                    Tone(b, 0.05f, 0.08f, 400f, 520f, Wave.Triangle, 0.2f, 0.002f, 0.04f);
                    break;
                case SfxId.PlayerHurt:
                    b = Buf(0.36f);
                    Tone(b, 0f, 0.3f, 420f, 170f, Wave.Saw, 0.45f, 0.004f, 0.11f, 24f, 0.04f);
                    NoiseBurst(b, 0f, 0.15f, 0.5f, 0.001f, 0.05f, 2800f, 600f);
                    LowPass(b, 3000f);
                    break;
                case SfxId.PlayerDeath:
                    b = Buf(1.3f);
                    Tone(b, 0f, 1.1f, 340f, 55f, Wave.Saw, 0.5f, 0.01f, 0.45f, 5f, 0.03f);
                    NoiseBurst(b, 0f, 0.5f, 0.35f, 0.01f, 0.2f, 1800f, 200f);
                    Metal(b, 0.1f, 196f, 0.4f, 0.5f);
                    LowPass(b, 2600f);
                    break;
                case SfxId.EnemyDeath:
                    b = Buf(0.5f);
                    NoiseBurst(b, 0f, 0.3f, 0.8f, 0.001f, 0.1f, 2400f, 300f);
                    Tone(b, 0f, 0.4f, 260f, 60f, Wave.Triangle, 0.6f, 0.002f, 0.14f);
                    break;
                case SfxId.EnemyTelegraph:
                    b = Buf(0.22f);
                    Tone(b, 0f, 0.1f, 520f, 640f, Wave.Sine, 0.4f, 0.004f, 0.05f);
                    Tone(b, 0.09f, 0.12f, 700f, 860f, Wave.Sine, 0.5f, 0.004f, 0.06f);
                    break;
                case SfxId.EnemyShoot:
                    b = Buf(0.28f);
                    Tone(b, 0f, 0.22f, 980f, 260f, Wave.Triangle, 0.6f, 0.002f, 0.08f);
                    NoiseBurst(b, 0f, 0.12f, 0.35f, 0.001f, 0.04f, 3500f, 900f);
                    break;
                case SfxId.Explosion:
                    b = Buf(1.0f);
                    NoiseBurst(b, 0f, 0.9f, 1f, 0.002f, 0.28f, 3000f, 140f);
                    Tone(b, 0f, 0.8f, 90f, 32f, Wave.Sine, 1f, 0.002f, 0.3f);
                    Drive(b, 1.6f);
                    break;
                case SfxId.Burn:
                    b = Buf(0.3f);
                    NoiseBurst(b, 0f, 0.28f, 0.5f, 0.05f, 0.12f, 1600f, 3000f);
                    break;
                case SfxId.Poison:
                    b = Buf(0.3f);
                    Tone(b, 0f, 0.26f, 300f, 160f, Wave.Sine, 0.5f, 0.02f, 0.1f, 14f, 0.12f);
                    NoiseBurst(b, 0f, 0.2f, 0.15f, 0.04f, 0.08f, 900f, 500f);
                    break;

                // ---- bosses
                case SfxId.BossRoar:
                    b = Buf(1.3f);
                    Tone(b, 0f, 1.2f, 90f, 62f, Wave.Saw, 0.8f, 0.08f, 0.6f, 11f, 0.05f);
                    Tone(b, 0f, 1.1f, 135f, 90f, Wave.Square, 0.25f, 0.1f, 0.5f);
                    NoiseBurst(b, 0f, 1.0f, 0.4f, 0.1f, 0.4f, 1200f, 300f);
                    Drive(b, 2.2f);
                    LowPass(b, 1800f);
                    break;
                case SfxId.BossSlam:
                    b = Buf(1.0f);
                    NoiseBurst(b, 0f, 0.8f, 1f, 0.001f, 0.2f, 1800f, 100f);
                    Tone(b, 0f, 0.9f, 70f, 28f, Wave.Sine, 1.2f, 0.001f, 0.32f);
                    Metal(b, 0f, 120f, 0.3f, 0.25f);
                    Drive(b, 1.8f);
                    break;
                case SfxId.BossPhase:
                    b = Buf(1.1f);
                    Tone(b, 0f, 1.0f, 110f, 330f, Wave.Saw, 0.55f, 0.3f, 0.5f);
                    Metal(b, 0.5f, 220f, 0.5f, 0.45f);
                    NoiseBurst(b, 0.3f, 0.6f, 0.3f, 0.2f, 0.25f, 1000f, 4000f);
                    LowPass(b, 3500f);
                    break;
                case SfxId.BossDeath:
                    b = Buf(2.4f);
                    Tone(b, 0f, 2.2f, 120f, 34f, Wave.Saw, 0.7f, 0.05f, 0.9f, 7f, 0.05f);
                    NoiseBurst(b, 0f, 1.6f, 0.7f, 0.02f, 0.7f, 2000f, 120f);
                    Metal(b, 0.2f, 98f, 0.6f, 0.9f);
                    Drive(b, 1.7f);
                    LowPass(b, 2400f);
                    break;
                case SfxId.BossCharge:
                    b = Buf(0.9f);
                    Tone(b, 0f, 0.85f, 90f, 520f, Wave.Saw, 0.45f, 0.3f, 0.7f);
                    NoiseBurst(b, 0f, 0.8f, 0.3f, 0.4f, 0.4f, 500f, 4000f);
                    LowPass(b, 3500f);
                    break;

                // ---- world and items
                case SfxId.GoldPickup:
                    b = Buf(0.3f);
                    Tone(b, 0f, 0.1f, 1318f, Wave.Triangle, 0.6f, 0.002f, 0.05f);
                    Tone(b, 0.07f, 0.22f, 1760f, Wave.Triangle, 0.6f, 0.002f, 0.1f);
                    break;
                case SfxId.CellPickup:
                    b = Buf(0.4f);
                    for (int i = 0; i < 3; i++) Tone(b, i * 0.06f, 0.2f, 1500f + i * 420f, Wave.Sine, 0.5f, 0.002f, 0.09f);
                    break;
                case SfxId.ScrollPickup:
                    b = Buf(0.5f);
                    NoiseBurst(b, 0f, 0.12f, 0.25f, 0.02f, 0.05f, 3000f, 6000f);
                    for (int i = 0; i < 3; i++) Tone(b, 0.05f + i * 0.08f, 0.3f, 660f * Mathf.Pow(1.26f, i), Wave.Triangle, 0.5f, 0.003f, 0.14f);
                    break;
                case SfxId.ItemPickup:
                    b = Buf(0.7f);
                    for (int i = 0; i < 4; i++) Tone(b, i * 0.07f, 0.45f, 523f * Mathf.Pow(1.25f, i), Wave.Triangle, 0.45f, 0.003f, 0.18f);
                    Metal(b, 0.2f, 1046f, 0.2f, 0.25f);
                    break;
                case SfxId.ItemDrop:
                    b = Buf(0.25f);
                    NoiseBurst(b, 0f, 0.08f, 0.5f, 0.001f, 0.025f, 1800f, 500f);
                    Tone(b, 0f, 0.15f, 280f, 160f, Wave.Triangle, 0.35f, 0.001f, 0.06f);
                    break;
                case SfxId.ChestOpen:
                    b = Buf(0.9f);
                    Tone(b, 0f, 0.35f, 90f, 140f, Wave.Saw, 0.35f, 0.02f, 0.2f, 30f, 0.2f);
                    NoiseBurst(b, 0f, 0.3f, 0.3f, 0.02f, 0.12f, 800f, 1800f);
                    for (int i = 0; i < 3; i++) Tone(b, 0.3f + i * 0.08f, 0.5f, 784f * Mathf.Pow(1.26f, i), Wave.Sine, 0.4f, 0.003f, 0.25f);
                    LowPass(b, 5000f);
                    break;
                case SfxId.DoorOpen:
                    b = Buf(0.8f);
                    NoiseBurst(b, 0f, 0.7f, 0.5f, 0.2f, 0.3f, 300f, 1600f);
                    Tone(b, 0f, 0.7f, 70f, 120f, Wave.Triangle, 0.4f, 0.1f, 0.3f);
                    break;
                case SfxId.Portal:
                    b = Buf(1.3f);
                    Tone(b, 0f, 1.2f, 180f, 720f, Wave.Triangle, 0.5f, 0.2f, 0.7f, 6f, 0.03f);
                    Tone(b, 0.1f, 1.1f, 270f, 1080f, Wave.Sine, 0.3f, 0.2f, 0.6f);
                    NoiseBurst(b, 0f, 1.0f, 0.2f, 0.3f, 0.4f, 800f, 5000f);
                    break;
                case SfxId.Flask:
                    b = Buf(0.55f);
                    for (int i = 0; i < 3; i++) Tone(b, i * 0.13f, 0.1f, 150f + i * 40f, 110f + i * 30f, Wave.Sine, 0.7f, 0.004f, 0.045f);
                    Tone(b, 0.4f, 0.14f, 700f, 1100f, Wave.Sine, 0.25f, 0.005f, 0.07f);
                    break;
                case SfxId.Skill:
                    b = Buf(0.45f);
                    NoiseBurst(b, 0f, 0.3f, 0.5f, 0.06f, 0.12f, 800f, 4500f);
                    Tone(b, 0f, 0.3f, 300f, 700f, Wave.Triangle, 0.4f, 0.01f, 0.14f);
                    Tone(b, 0.1f, 0.25f, 1600f, 2400f, Wave.Sine, 0.2f, 0.004f, 0.1f);
                    break;
                case SfxId.Mutation:
                    b = Buf(1.0f);
                    Tone(b, 0f, 0.9f, 140f, 560f, Wave.Triangle, 0.5f, 0.15f, 0.5f, 8f, 0.04f);
                    Metal(b, 0.35f, 392f, 0.5f, 0.4f);
                    NoiseBurst(b, 0.1f, 0.6f, 0.15f, 0.2f, 0.3f, 1500f, 6000f);
                    break;
                case SfxId.ShopBuy:
                    b = Buf(0.5f);
                    Tone(b, 0f, 0.08f, 1568f, Wave.Triangle, 0.5f, 0.002f, 0.04f);
                    Tone(b, 0.06f, 0.08f, 2093f, Wave.Triangle, 0.5f, 0.002f, 0.04f);
                    Tone(b, 0.12f, 0.3f, 2637f, Wave.Triangle, 0.5f, 0.002f, 0.14f);
                    break;
                case SfxId.ShopDeny:
                    b = Buf(0.25f);
                    Tone(b, 0f, 0.2f, 160f, 110f, Wave.Square, 0.35f, 0.003f, 0.09f);
                    LowPass(b, 1400f);
                    break;
                case SfxId.RoomClear:
                    b = Buf(0.9f);
                    float[] notes = { 392f, 494f, 587f, 784f };
                    for (int i = 0; i < notes.Length; i++) Tone(b, i * 0.1f, 0.5f, notes[i], Wave.Triangle, 0.45f, 0.004f, 0.2f);
                    break;
                case SfxId.LevelStart:
                    b = Buf(1.6f);
                    Metal(b, 0f, 130f, 0.8f, 0.7f);
                    Tone(b, 0.1f, 1.4f, 98f, 98f, Wave.Triangle, 0.3f, 0.3f, 0.7f);
                    break;
                case SfxId.Bell:
                    b = Buf(1.8f);
                    Metal(b, 0f, 330f, 0.9f, 0.6f);
                    break;

                // ---- interface
                case SfxId.UiMove:
                    b = Buf(0.05f);
                    Tone(b, 0f, 0.04f, 1200f, 1000f, Wave.Sine, 0.5f, 0.001f, 0.015f);
                    break;
                case SfxId.UiClick:
                    b = Buf(0.12f);
                    Tone(b, 0f, 0.05f, 620f, 900f, Wave.Triangle, 0.5f, 0.001f, 0.025f);
                    Tone(b, 0.04f, 0.08f, 1100f, 1250f, Wave.Triangle, 0.4f, 0.001f, 0.035f);
                    break;
                case SfxId.UiBack:
                    b = Buf(0.12f);
                    Tone(b, 0f, 0.05f, 900f, 650f, Wave.Triangle, 0.5f, 0.001f, 0.025f);
                    Tone(b, 0.04f, 0.08f, 600f, 420f, Wave.Triangle, 0.4f, 0.001f, 0.035f);
                    break;
                case SfxId.UiConfirm:
                    b = Buf(0.3f);
                    Tone(b, 0f, 0.1f, 784f, Wave.Triangle, 0.5f, 0.002f, 0.05f);
                    Tone(b, 0.07f, 0.22f, 1175f, Wave.Triangle, 0.5f, 0.002f, 0.1f);
                    break;
                case SfxId.UiOpen:
                    b = Buf(0.25f);
                    NoiseBurst(b, 0f, 0.2f, 0.4f, 0.08f, 0.07f, 800f, 3000f);
                    Tone(b, 0.04f, 0.15f, 500f, 740f, Wave.Sine, 0.25f, 0.01f, 0.07f);
                    break;
                case SfxId.UiDeny:
                    b = Buf(0.2f);
                    Tone(b, 0f, 0.16f, 130f, 100f, Wave.Square, 0.35f, 0.002f, 0.07f);
                    LowPass(b, 1200f);
                    break;
                case SfxId.Toast:
                    b = Buf(0.35f);
                    Tone(b, 0f, 0.3f, 988f, Wave.Sine, 0.45f, 0.003f, 0.12f);
                    Tone(b, 0f, 0.2f, 1976f, Wave.Sine, 0.15f, 0.003f, 0.06f);
                    break;

                // ---- loops
                case SfxId.AmbientCatacombs:
                    loop = true;
                    b = AmbientLoop(8f, 55f, 82.4f, 0.12f);
                    break;
                case SfxId.AmbientPromenade:
                    loop = true;
                    b = AmbientLoop(8f, 49f, 73.4f, 0.28f);
                    break;
                case SfxId.FireLoop:
                    loop = true;
                    b = Buf(4f);
                    NoiseBurst(b, 0f, 4f, 0.25f, 0f, 100f, 700f, 700f);
                    for (int i = 0; i < 26; i++)
                    {
                        float at = (float)_rng.NextDouble() * 3.8f;
                        NoiseBurst(b, at, 0.025f + (float)_rng.NextDouble() * 0.03f, 0.9f, 0.0005f, 0.01f, 3500f, 1500f);
                    }
                    break;
                default:
                    b = Buf(0.1f);
                    break;
            }
            return Finish(id.ToString(), b, loop ? 0.6f : 0.85f, loop);
        }

        /// <summary>A slow drone: two low sines drifting against each other under filtered wind, with a distant bell now and then.</summary>
        static float[] AmbientLoop(float seconds, float f1, float f2, float wind)
        {
            var b = Buf(seconds);
            // whole numbers of cycles keep the loop seamless
            f1 = Mathf.Round(f1 * seconds) / seconds;
            f2 = Mathf.Round(f2 * seconds) / seconds;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float swell = 0.65f + 0.35f * Mathf.Sin(t / seconds * Mathf.PI * 4f);
                b[i] = (Mathf.Sin(t * f1 * Mathf.PI * 2f) * 0.6f + Mathf.Sin(t * f2 * Mathf.PI * 2f) * 0.4f) * swell * 0.5f;
            }
            var w = Buf(seconds);
            NoiseBurst(w, 0f, seconds, wind, 0f, 100f, 420f, 420f);
            for (int i = 0; i < b.Length; i++) b[i] += w[i] * (0.6f + 0.4f * Mathf.Sin(i / (float)Rate / seconds * Mathf.PI * 2f));
            Metal(b, 1.5f, 220f, 0.07f, 0.9f);
            Metal(b, 5.2f, 165f, 0.06f, 1.1f);
            return b;
        }
    }
}
