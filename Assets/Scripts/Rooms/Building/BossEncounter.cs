using UnityEngine;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Utils;

namespace Roughlike.Rooms
{
    /// <summary>
    /// The boss room at the end of a level. The boss waits in the arena; when the player walks in, the door behind them seals and
    /// the fight starts. Beating the boss opens the door again and reveals the exit portal.
    /// </summary>
    public sealed class BossEncounter : MonoBehaviour
    {
        RoomChunk _chunk;
        BossController _boss;
        ExitDoor _exit;
        GameObject _gate;
        bool _started;

        public BossController Boss => _boss;
        public bool Started => _started;
        public bool Defeated { get; private set; }
        public Rect Arena { get; private set; }

        public static BossEncounter Create(RoomChunk chunk, BiomeData biome, ExitDoor exit, EnemyScaling scaling)
        {
            var go = new GameObject("Boss Encounter");
            go.transform.SetParent(chunk.transform, false);
            var e = go.AddComponent<BossEncounter>();
            e._chunk = chunk;
            e._exit = exit;

            Rect b = chunk.WorldBounds;
            e.Arena = new Rect(b.x + 1f, b.y + ChunkMetrics.FloorTop, b.width - 2f, ChunkMetrics.Height - 1f - ChunkMetrics.FloorTop);
            float spawnX = b.x + (biome.boss.kind == BossKind.Witch ? 21f : 22f);
            e._boss = BossFactory.Build(biome.boss, new Vector2(spawnX, e.Arena.yMin + (biome.boss.kind == BossKind.Witch ? 4.3f : 0.05f)), scaling, e.Arena);
            e._boss.transform.SetParent(chunk.transform, true);
            e._boss.SetFacing(-1);
            return e;
        }

        void OnEnable() => EventBus<BossDefeatedEvent>.Subscribe(OnDefeated);
        void OnDisable() => EventBus<BossDefeatedEvent>.Unsubscribe(OnDefeated);

        void Update()
        {
            if (_framing && _camTarget != null) UpdateFraming();
            if (_started || _boss == null) return;
            var player = Roughlike.Combat.Health.Player;
            if (player == null || player.IsDead) return;
            if (player.transform.position.x > Arena.xMin + 5f && Arena.Contains(player.Center)) Begin();
        }

        public void Begin()
        {
            if (_started) return;
            _started = true;
            SealDoor();
            ZoomCamera(BossZoom);
            _boss.Engage();
            EventBus<ToastEvent>.Raise(new ToastEvent { Text = _boss.Definition.displayName, Colour = new Color(1f, 0.55f, 0.4f) });
        }

        void OnDefeated(BossDefeatedEvent e)
        {
            if (_boss == null || e.Definition != _boss.Definition) return;
            Defeated = true;
            OpenDoor();
            ZoomCamera(_normalZoom);
            if (_exit != null) _exit.gameObject.SetActive(true);
            EventBus<ToastEvent>.Raise(new ToastEvent { Text = _boss.Definition.displayName + " defeated", Colour = new Color(0.55f, 1f, 0.7f) });
        }

        const float BossZoom = 7.6f;

        float _normalZoom = -1f;
        Unity.Cinemachine.CinemachineCamera _vcam;
        Coroutine _zoom;
        Transform _camTarget, _originalFollow;
        bool _framing;

        /// <summary>Pulls the camera back so the whole arena is visible during the fight, then returns it afterwards.</summary>
        void ZoomCamera(float size)
        {
            if (_vcam == null) _vcam = FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (_vcam == null || size < 0f) return;
            if (_normalZoom < 0f) _normalZoom = _vcam.Lens.OrthographicSize;
            FrameArena(size > _normalZoom);
            if (_zoom != null) StopCoroutine(_zoom);
            _zoom = StartCoroutine(ZoomTo(size));
        }

        /// <summary>While fighting, the camera looks at the middle of the arena (leaning toward the player) so the boss stays in view.</summary>
        void FrameArena(bool on)
        {
            if (_vcam == null) return;
            if (on && !_framing)
            {
                _framing = true;
                _originalFollow = _vcam.Follow;
                if (_camTarget == null) _camTarget = new GameObject("Boss Camera Target").transform;
                _camTarget.SetParent(transform, false);
                _vcam.Follow = _camTarget;
                UpdateFraming();
            }
            else if (!on && _framing)
            {
                _framing = false;
                _vcam.Follow = _originalFollow;
            }
        }

        void UpdateFraming()
        {
            var player = Roughlike.Combat.Health.Player;
            float px = player != null ? player.transform.position.x : Arena.center.x;
            _camTarget.position = new Vector3(Arena.center.x + (px - Arena.center.x) * 0.3f, Arena.yMin + 4.2f, 0f);
        }

        System.Collections.IEnumerator ZoomTo(float size)
        {
            while (_vcam != null && Mathf.Abs(_vcam.Lens.OrthographicSize - size) > 0.02f)
            {
                var lens = _vcam.Lens;
                lens.OrthographicSize = Mathf.MoveTowards(lens.OrthographicSize, size, Time.unscaledDeltaTime * 3.5f);
                _vcam.Lens = lens;
                yield return null;
            }
        }

        void OnDestroy()
        {
            // leaving the level mid-fight (dying, a new run) puts the camera back
            if (_vcam != null && _framing) _vcam.Follow = _originalFollow;
            if (_vcam != null && _normalZoom > 0f)
            {
                var lens = _vcam.Lens;
                lens.OrthographicSize = _normalZoom;
                _vcam.Lens = lens;
            }
        }

        void SealDoor()
        {
            if (_gate != null) return;
            Rect b = _chunk.WorldBounds;
            float h = ChunkMetrics.DoorTop - ChunkMetrics.FloorTop;
            _gate = new GameObject("Boss Gate");
            _gate.transform.SetParent(transform, false);
            _gate.layer = LayerMask.NameToLayer(GameLayers.GroundName);
            _gate.transform.position = new Vector3(b.x + 0.5f, b.y + ChunkMetrics.FloorTop + h * 0.5f, 0f);
            _gate.AddComponent<BoxCollider2D>().size = new Vector2(1f, h);
            var vis = new GameObject("Glow");
            vis.transform.SetParent(_gate.transform, false);
            vis.transform.localScale = new Vector3(0.7f, h, 1f);
            var sr = vis.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Square;
            sr.color = new Color(1f, 0.35f, 0.3f, 0.5f);
            sr.sortingOrder = 6;
            Physics2D.SyncTransforms();
        }

        void OpenDoor()
        {
            if (_gate == null) return;
            _gate.SetActive(false);
            Destroy(_gate);
            _gate = null;
        }
    }
}
