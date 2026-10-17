using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Roughlike.Core;
using Roughlike.Player;

namespace Roughlike.Editor
{
    /// <summary>
    /// Builds the M1 movement test scene, the movement config asset and the player prefab.
    /// Re-runnable: rebuilds the scene from scratch, keeps the config asset if it exists.
    /// </summary>
    public static class GrayboxBuilder
    {
        const string ConfigPath = "Assets/Data/Player/PlayerMovement.asset";
        const string PrefabPath = "Assets/Prefabs/Player/Player.prefab";
        const string ScenePath = "Assets/Scenes/M1_Graybox.unity";

        static Sprite _square;
        static Transform _root;

        static readonly Color GroundCol = new Color(0.20f, 0.23f, 0.30f);
        static readonly Color SlopeCol = new Color(0.26f, 0.30f, 0.40f);
        static readonly Color OneWayCol = new Color(0.95f, 0.70f, 0.25f);
        static readonly Color LadderCol = new Color(0.95f, 0.85f, 0.35f, 0.55f);
        static readonly Color MoverCol = new Color(0.25f, 0.75f, 0.75f);
        static readonly Color LedgeCol = new Color(0.45f, 0.35f, 0.55f);

        [MenuItem("Roughlike/Build/M1 Graybox Scene")]
        public static void Build()
        {
            ProjectSetup.CreateLayers();
            PlaceholderArt.Generate();
            _square = PlaceholderArt.LoadSquare();

            MovementConfig config = LoadOrCreateConfig();
            GameObject prefab = BuildPlayerPrefab(config);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _root = new GameObject("Level").transform;

            BuildLevel();

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.position = new Vector3(0f, 0.05f, 0f);
            var controller = player.GetComponent<PlayerController>();

            BuildCamera(controller);
            BuildDebug(controller);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Graybox] M1 scene built: " + ScenePath);
        }

        // ------------------------------------------------------------------ assets

        static MovementConfig LoadOrCreateConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MovementConfig>(ConfigPath);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            var cfg = ScriptableObject.CreateInstance<MovementConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
            return cfg;
        }

        static GameObject BuildPlayerPrefab(MovementConfig config)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var controller = PlayerFactory.Build(config, PlayerFactory.InputKind.Device, PlaceholderArt.LoadPlayer());
            GameObject go = controller.gameObject;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ------------------------------------------------------------------ level

        static GameObject Block(string name, Vector2 centre, Vector2 size, string layer, Color col, float angle = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = centre;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.layer = LayerMask.NameToLayer(layer);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = col;
            go.AddComponent<BoxCollider2D>();
            return go;
        }

        /// <summary>Solid box described by its top edge (x0..x1, top y, bottom y).</summary>
        static GameObject Solid(string name, float x0, float x1, float topY, float bottomY, Color col)
            => Block(name, new Vector2((x0 + x1) * 0.5f, (topY + bottomY) * 0.5f), new Vector2(x1 - x0, topY - bottomY), GameLayers.GroundName, col);

        static GameObject OneWay(string name, float x0, float x1, float topY, float thickness = 0.3f)
            => Block(name, new Vector2((x0 + x1) * 0.5f, topY - thickness * 0.5f), new Vector2(x1 - x0, thickness), GameLayers.OneWayName, OneWayCol);

        /// <summary>Slope whose top surface runs from (x, y) over a horizontal run at the given angle (negative angle descends).</summary>
        static void Ramp(string name, float x, float y, float run, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float length = Mathf.Abs(run / Mathf.Cos(rad));
            Vector2 start = new Vector2(x, y);
            Vector2 end = start + new Vector2(run, run * Mathf.Tan(rad));
            Vector2 mid = (start + end) * 0.5f;
            Vector2 normal = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
            const float thickness = 2f;
            Block(name, mid - normal * (thickness * 0.5f), new Vector2(length, thickness), GameLayers.GroundName, SlopeCol, angleDeg);
        }

        static void Sign(string text, Vector2 pos)
        {
            var go = new GameObject("Sign: " + text);
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.text = text;
            tm.characterSize = 0.18f;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 1f, 1f, 0.85f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 5;
        }

        static void BuildLevel()
        {
            // Floor A and B with a pit between.
            Solid("Floor A", -8f, 108f, 0f, -3f, GroundCol);
            Solid("Floor B", 136f, 175f, 0f, -3f, GroundCol);
            Solid("Left wall", -9f, -8f, 30f, -3f, GroundCol);

            Sign("RUN  JUMP  ROLL (also mid-air)", new Vector2(3f, 4f));

            // Slopes: 30 degree up, plateau, 30 degree down.
            Ramp("Ramp up", 8f, 0f, 8f, 30f);
            float plateau = 8f * Mathf.Tan(30f * Mathf.Deg2Rad);
            Solid("Plateau", 16f, 24f, plateau, -3f, SlopeCol);
            Ramp("Ramp down", 24f, plateau, 8f, -30f);
            Sign("SLOPES 30 deg", new Vector2(16f, plateau + 3f));

            // One-way platforms.
            OneWay("OneWay low", 36f, 41f, 3f);
            OneWay("OneWay high", 43f, 48f, 6f);
            Sign("ONE-WAY: jump up through\nDown + Jump to drop", new Vector2(42f, 8.5f));

            // Ladder through a floor hole, capped by a one-way platform.
            Solid("Tower floor L", 52f, 55.6f, 8f, 7f, GroundCol);
            Solid("Tower floor R", 57f, 66f, 8f, 7f, GroundCol);
            OneWay("Ladder cap", 55.6f, 57f, 8f, 0.15f);
            var ladder = new GameObject("Ladder");
            ladder.transform.SetParent(_root, false);
            ladder.transform.position = new Vector3(56.3f, 4f, 0f);
            ladder.layer = LayerMask.NameToLayer(GameLayers.LadderName);
            var lbox = ladder.AddComponent<BoxCollider2D>();
            lbox.size = new Vector2(1f, 8f);
            lbox.isTrigger = true;
            ladder.AddComponent<Roughlike.Player.Ladder>();
            var lvis = new GameObject("Visual");
            lvis.transform.SetParent(ladder.transform, false);
            lvis.transform.localScale = new Vector3(1f, 8f, 1f);
            var lsr = lvis.AddComponent<SpriteRenderer>();
            lsr.sprite = _square; lsr.color = LadderCol; lsr.sortingOrder = -1;
            Sign("LADDER: Up / Down\n(Jump to leap off)", new Vector2(56.3f, 10.5f));

            // Ledges: a 4 high block, then a taller one beside it.
            Block("Ledge block 1", new Vector2(73f, 1f), new Vector2(6f, 6f), GameLayers.GroundName, LedgeCol);
            Solid("Ledge block 2", 76f, 82f, 7.6f, -3f, LedgeCol);
            Sign("LEDGES: jump toward the wall\nUp / Jump to climb, Down to drop", new Vector2(69f, 6.5f));

            // Roll-under tunnel.
            Solid("Low ceiling", 88f, 96f, 5.2f, 1.2f, GroundCol);
            Sign("ROLL UNDER (gap 1.2, you are 1.6)", new Vector2(92f, 6.8f));

            // Wall-jump shaft (needs the Wall Jump rune: press F3).
            Solid("Shaft wall L", 100f, 101f, 14f, -3f, GroundCol);
            Solid("Shaft wall R", 105f, 106f, 14f, -3f, GroundCol);
            Sign("WALL JUMP shaft\n(rune: press F3)", new Vector2(103f, 16.5f));

            // Moving platforms over the pit.
            var h = Block("Mover horizontal", new Vector2(111f, -0.25f), new Vector2(4f, 0.5f), GameLayers.GroundName, MoverCol);
            AddMover(h, new[] { Vector2.zero, new Vector2(20f, 0f) }, 3f);
            Sign("MOVING PLATFORMS", new Vector2(122f, 5f));

            // Elevator beside a high ledge.
            var e = Block("Mover elevator", new Vector2(146f, -0.25f), new Vector2(4f, 0.5f), GameLayers.GroundName, MoverCol);
            AddMover(e, new[] { Vector2.zero, new Vector2(0f, 7f) }, 2.5f);
            Solid("High block", 151f, 166f, 7f, -3f, LedgeCol);
            Sign("END of the M1 graybox", new Vector2(158f, 10f));
        }

        static void AddMover(GameObject go, Vector2[] waypoints, float speed)
        {
            var mp = go.AddComponent<MovingPlatform>();
            mp.SetWaypoints(waypoints, speed);
            EditorUtility.SetDirty(mp);
        }

        // ------------------------------------------------------------------ camera / debug

        static void BuildCamera(PlayerController player)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.13f);
            camGo.transform.position = new Vector3(0f, 3f, -10f);
            camGo.AddComponent<CinemachineBrain>();

            var targetGo = new GameObject("Camera Target");
            var target = targetGo.AddComponent<PlayerCameraTarget>();
            target.SetPlayer(player);
            targetGo.transform.position = player.transform.position;

            var vcamGo = new GameObject("CM Player Camera");
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Follow = targetGo.transform;
            vcam.Lens.OrthographicSize = 6.5f;
            vcam.Lens.NearClipPlane = -50f;
            vcam.Lens.FarClipPlane = 50f;

            var composer = vcamGo.AddComponent<CinemachinePositionComposer>();
            composer.Damping = new Vector3(0.25f, 0.35f, 0f);
            composer.Composition.DeadZone.Enabled = true;
            composer.Composition.DeadZone.Size = new Vector2(0.06f, 0.1f);
            composer.Composition.ScreenPosition = new Vector2(0f, -0.05f);
        }

        static void BuildDebug(PlayerController player)
        {
            var go = new GameObject("Debug");
            var overlay = go.AddComponent<MovementDebugOverlay>();
            overlay.SetPlayer(player);
            var respawn = go.AddComponent<FallRespawn>();
            respawn.SetPlayer(player);
        }
    }
}


