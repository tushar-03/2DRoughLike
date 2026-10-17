using UnityEngine;

namespace Roughlike.Player
{
    /// <summary>
    /// Platform that travels between local-space waypoints. It runs before characters
    /// (execution order -100) and publishes <see cref="FrameDelta"/>, which riders apply to themselves.
    /// Put the collider on the Ground or OneWay layer.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MovingPlatform : MonoBehaviour
    {
        [SerializeField] Vector2[] localWaypoints = { Vector2.zero, new Vector2(6f, 0f) };
        [SerializeField] float speed = 2.5f;
        [SerializeField] float waitTime = 0.6f;
        [Tooltip("Run from Update. Tests turn this off and call Tick themselves.")]
        public bool autoTick = true;

        Vector2[] _world;
        int _target = 1;
        int _dir = 1;
        float _wait;

        public Vector2 FrameDelta { get; private set; }

        void Awake()
        {
            _world = new Vector2[localWaypoints.Length];
            for (int i = 0; i < _world.Length; i++) _world[i] = (Vector2)transform.position + localWaypoints[i];
            if (_world.Length > 0) transform.position = _world[0];
        }

        public void SetWaypoints(Vector2[] local, float newSpeed)
        {
            localWaypoints = local;
            speed = newSpeed;
        }

        void Update() { if (autoTick) Tick(Time.deltaTime); }

        public void Tick(float dt)
        {
            FrameDelta = Vector2.zero;
            if (_world == null || _world.Length < 2) return;

            if (_wait > 0f) { _wait -= dt; return; }

            Vector2 pos = transform.position;
            Vector2 next = Vector2.MoveTowards(pos, _world[_target], speed * dt);
            FrameDelta = next - pos;
            transform.position = next;
            Physics2D.SyncTransforms();

            if ((next - _world[_target]).sqrMagnitude < 1e-6f)
            {
                _wait = waitTime;
                if (_target + _dir >= _world.Length || _target + _dir < 0) _dir = -_dir;
                _target += _dir;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector2 origin = Application.isPlaying && _world != null ? Vector2.zero : (Vector2)transform.position;
            for (int i = 0; i < localWaypoints.Length; i++)
            {
                Vector2 p = Application.isPlaying && _world != null ? _world[i] : origin + localWaypoints[i];
                Gizmos.DrawWireSphere(p, 0.2f);
                if (i > 0)
                {
                    Vector2 q = Application.isPlaying && _world != null ? _world[i - 1] : origin + localWaypoints[i - 1];
                    Gizmos.DrawLine(q, p);
                }
            }
        }
    }
}
