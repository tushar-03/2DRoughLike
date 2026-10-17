using UnityEngine;

namespace Roughlike.Utils
{
    /// <summary>
    /// Plays <see cref="CharacterArt"/> clips on a SpriteRenderer. Clips either loop on their own clock, play once,
    /// or are scrubbed by an outside value (an enemy's telegraph progress), so the pose always matches the gameplay timing.
    /// Missing clips are ignored: the sprite just keeps its last frame.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] CharacterArt art;

        SpriteRenderer _sr;
        string _clip;
        Sprite[] _frames = System.Array.Empty<Sprite>();
        CharacterArt.Clip _def;
        float _time, _duration;
        bool _once, _scrub;
        float _scrubValue;

        public CharacterArt Art => art;
        public string Clip => _clip;
        /// <summary>True while a one-shot clip is still running; looping clips and scrubbed clips never report finished.</summary>
        public bool IsPlayingOnce => _once && _time < _duration;

        public void Bind(CharacterArt newArt)
        {
            art = newArt;
            _clip = null;
            _frames = System.Array.Empty<Sprite>();
            Renderer.sprite = null;
        }

        SpriteRenderer Renderer => _sr != null ? _sr : (_sr = GetComponent<SpriteRenderer>());

        /// <summary>Loops a clip. Does nothing when it is already the current clip, so it is safe to call every frame.</summary>
        public void Loop(string clip)
        {
            if (_clip == clip && !_once && !_scrub) return;
            Begin(clip, false, false, 0f);
        }

        /// <summary>Plays a clip once over <paramref name="duration"/> seconds (0 = its natural length) and holds the last frame.</summary>
        public void PlayOnce(string clip, float duration = 0f) => Begin(clip, true, false, duration);

        /// <summary>Shows the frame matching <paramref name="progress01"/> of the clip. Call every frame while it should show.</summary>
        public void Scrub(string clip, float progress01)
        {
            if (_clip != clip || !_scrub) Begin(clip, false, true, 0f);
            _scrubValue = Mathf.Clamp01(progress01);
            Show(_scrubValue);
        }

        void Begin(string clip, bool once, bool scrub, float duration)
        {
            if (art == null) return;
            _frames = art.Frames(clip);
            _def = art.Find(clip);
            _clip = clip;
            _once = once;
            _scrub = scrub;
            _time = 0f;
            _duration = duration > 0f ? duration : (_def != null ? _def.frames / Mathf.Max(1f, _def.fps) : 0.5f);
            if (_frames.Length > 0) Renderer.sprite = _frames[0];
        }

        void Update()
        {
            if (_frames.Length == 0 || _scrub) return;
            _time += Time.deltaTime;

            if (_once)
            {
                Show(Mathf.Clamp01(_time / Mathf.Max(0.001f, _duration)));
                return;
            }
            float fps = _def != null ? _def.fps : 10f;
            int i = Mathf.FloorToInt(_time * fps);
            bool loop = _def == null || _def.loop;
            i = loop ? i % _frames.Length : Mathf.Min(i, _frames.Length - 1);
            Renderer.sprite = _frames[i];
        }

        /// <summary>Test seam: advances the clock without Update.</summary>
        public void Advance(float dt)
        {
            if (_frames.Length == 0 || _scrub) return;
            _time += dt;
            if (_once) { Show(Mathf.Clamp01(_time / Mathf.Max(0.001f, _duration))); return; }
            float fps = _def != null ? _def.fps : 10f;
            int i = Mathf.FloorToInt(_time * fps);
            i = (_def == null || _def.loop) ? i % _frames.Length : Mathf.Min(i, _frames.Length - 1);
            Renderer.sprite = _frames[i];
        }

        void Show(float progress01)
        {
            if (_frames.Length == 0) return;
            int i = Mathf.Min(_frames.Length - 1, Mathf.FloorToInt(progress01 * _frames.Length));
            Renderer.sprite = _frames[i];
        }
    }
}
