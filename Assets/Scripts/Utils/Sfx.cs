using System.Collections.Generic;
using UnityEngine;

namespace Roughlike.Utils
{
    /// <summary>Every sound the game makes. The clips are synthesised in code (see <see cref="SfxSynth"/>), so there are no audio files to ship.</summary>
    public enum SfxId
    {
        // player movement
        Jump, DoubleJump, WallJump, Land, Roll, Footstep, LedgeGrab,
        // combat
        SwingLight, SwingHeavy, SwingBow, BowRelease, Charge, Hit, HitHeavy, Crit, Parry, Block, ShieldRaise, PlayerHurt, PlayerDeath,
        EnemyDeath, EnemyTelegraph, EnemyShoot, Explosion, Burn, Poison,
        // bosses
        BossRoar, BossSlam, BossPhase, BossDeath, BossCharge,
        // world and items
        GoldPickup, CellPickup, ScrollPickup, ItemPickup, ItemDrop, ChestOpen, DoorOpen, Portal, Flask, Skill, Mutation, ShopBuy, ShopDeny, RoomClear, LevelStart, Bell,
        // interface
        UiMove, UiClick, UiBack, UiConfirm, UiOpen, UiDeny, Toast,
        // loops
        AmbientCatacombs, AmbientPromenade, FireLoop
    }

    public enum SfxBus { Sfx, Ui, Ambient }

    /// <summary>
    /// Plays <see cref="SfxId"/> sounds from a small pool of audio sources. Pitch varies a little on every play so repeated sounds do
    /// not feel mechanical, and the same sound cannot stack within a few milliseconds. Bus volumes come from the settings
    /// (<see cref="SetVolumes"/>); the master volume is the AudioListener's.
    /// </summary>
    public static class Sfx
    {
        sealed class Runner : MonoBehaviour { }

        const int PoolSize = 20;

        static GameObject _root;
        static AudioSource[] _pool;
        static int _next;
        static readonly Dictionary<SfxId, AudioClip> Clips = new Dictionary<SfxId, AudioClip>();
        static readonly Dictionary<SfxId, float> LastPlayed = new Dictionary<SfxId, float>();
        static readonly Dictionary<SfxId, AudioSource> Loops = new Dictionary<SfxId, AudioSource>();
        static float _sfx = 1f, _ui = 1f, _ambient = 0.8f;

        /// <summary>Counts every sound requested (tests read this; audio is silent in batch mode).</summary>
        public static int PlayCount { get; private set; }
        public static SfxId LastId { get; private set; }
        public static bool Muted { get; set; }

        public static void SetVolumes(float sfx, float ui, float ambient)
        {
            _sfx = Mathf.Clamp01(sfx);
            _ui = Mathf.Clamp01(ui);
            _ambient = Mathf.Clamp01(ambient);
            foreach (var kv in Loops) if (kv.Value != null) kv.Value.volume = LoopVolume(kv.Key);
        }

        static SfxBus BusOf(SfxId id)
        {
            switch (id)
            {
                case SfxId.UiMove: case SfxId.UiClick: case SfxId.UiBack: case SfxId.UiConfirm: case SfxId.UiOpen: case SfxId.UiDeny: case SfxId.Toast:
                    return SfxBus.Ui;
                case SfxId.AmbientCatacombs: case SfxId.AmbientPromenade: case SfxId.FireLoop:
                    return SfxBus.Ambient;
                default: return SfxBus.Sfx;
            }
        }

        static float BusVolume(SfxBus bus) => bus == SfxBus.Ui ? _ui : bus == SfxBus.Ambient ? _ambient : _sfx;

        static float LoopVolume(SfxId id) => BusVolume(BusOf(id)) * (id == SfxId.FireLoop ? 0.35f : 0.5f);

        static float _nextListenerCheck;

        /// <summary>
        /// Scenes built before sound existed have no AudioListener, and without one nothing is ever heard. If none is active
        /// (checked every couple of seconds, which is cheap), the sound system supplies its own.
        /// </summary>
        /// <summary>Test seam: makes the next sound re-check for a listener straight away.</summary>
        public static void RecheckListener() => _nextListenerCheck = 0f;

        /// <summary>True when the sound system has had to supply its own listener (the scene has none).</summary>
        public static bool UsingOwnListener => _root != null && _root.TryGetComponent<AudioListener>(out var l) && l.enabled;

        static void EnsureListener()
        {
            if (Time.unscaledTime < _nextListenerCheck) return;
            _nextListenerCheck = Time.unscaledTime + 2f;
            var own = _root.GetComponent<AudioListener>();
            foreach (var l in Object.FindObjectsByType<AudioListener>())
            {
                if (l == own || !l.enabled || !l.gameObject.activeInHierarchy) continue;
                if (own != null) own.enabled = false;      // a scene camera has one: two listeners would only produce warnings
                return;
            }
            if (own == null) own = _root.AddComponent<AudioListener>();
            own.enabled = true;
        }

        static void Ensure()
        {
            if (_root != null) { EnsureListener(); return; }
            _nextListenerCheck = 0f;
            _root = new GameObject("Sfx");

            if (Application.isPlaying) Object.DontDestroyOnLoad(_root);
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var s = _root.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _pool[i] = s;
            }
            EnsureListener();
        }

        public static AudioClip Clip(SfxId id)
        {
            if (Clips.TryGetValue(id, out var c) && c != null) return c;
            c = SfxSynth.Make(id);
            Clips[id] = c;
            return c;
        }

        /// <summary>Plays a sound once. <paramref name="pitchVariance"/> is the +/- random pitch spread.</summary>
        public static void Play(SfxId id, float volume = 1f, float pitch = 1f, float pitchVariance = 0.06f)
        {
            PlayCount++;
            LastId = id;
            if (Muted || !Application.isPlaying) return;

            float now = Time.unscaledTime;
            // a negative gap means the clock restarted (a new play session without a domain reload): that entry is stale
            if (LastPlayed.TryGetValue(id, out float last) && now >= last && now - last < MinGap(id)) return;
            LastPlayed[id] = now;

            float bus = BusVolume(BusOf(id));
            if (bus <= 0.001f) return;
            Ensure();
            var src = _pool[_next];
            _next = (_next + 1) % PoolSize;
            src.Stop();
            src.clip = Clip(id);
            src.volume = Mathf.Clamp01(volume) * bus;
            src.pitch = pitch * (1f + Random.Range(-pitchVariance, pitchVariance));
            src.ignoreListenerPause = BusOf(id) == SfxBus.Ui;
            src.Play();
        }

        /// <summary>Plays a sound that fades with distance from the camera, so far-away fights are quieter.</summary>
        public static void PlayAt(SfxId id, Vector2 position, float volume = 1f, float pitch = 1f)
        {
            float dist = 0f;
            var cam = Camera.main;
            if (cam != null) dist = Vector2.Distance(cam.transform.position, position);
            float falloff = Mathf.Clamp01(1.25f - dist / 22f);
            if (falloff <= 0.02f) { PlayCount++; LastId = id; return; }
            Play(id, volume * falloff, pitch);
        }

        static float MinGap(SfxId id)
        {
            switch (id)
            {
                case SfxId.Footstep: return 0.12f;
                case SfxId.UiMove: return 0.03f;
                case SfxId.Hit: case SfxId.HitHeavy: return 0.05f;
                default: return 0.03f;
            }
        }

        /// <summary>Starts a looping sound (ambience, fire). Safe to call again; it is a no-op while the loop is already running.</summary>
        public static void StartLoop(SfxId id)
        {
            if (!Application.isPlaying || Muted) return;
            Ensure();
            if (Loops.TryGetValue(id, out var existing) && existing != null) { if (!existing.isPlaying) existing.Play(); return; }
            var src = _root.AddComponent<AudioSource>();
            src.clip = Clip(id);
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = LoopVolume(id);
            src.Play();
            Loops[id] = src;
        }

        public static void StopLoop(SfxId id)
        {
            if (Loops.TryGetValue(id, out var src) && src != null) { src.Stop(); Object.Destroy(src); }
            Loops.Remove(id);
        }

        public static void StopAllLoops()
        {
            foreach (var kv in new List<KeyValuePair<SfxId, AudioSource>>(Loops)) StopLoop(kv.Key);
        }

        public static bool IsLooping(SfxId id) => Loops.TryGetValue(id, out var s) && s != null && s.isPlaying;
    }
}
