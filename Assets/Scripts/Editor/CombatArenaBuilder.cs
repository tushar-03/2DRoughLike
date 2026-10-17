using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;

namespace Roughlike.Editor
{
    /// <summary>
    /// Builds the M2 combat sandbox: weapon and enemy data assets, the combat-enabled player prefab and the arena scene.
    /// Existing assets are kept (so hand-tuning survives a rebuild); delete an asset to regenerate its defaults.
    /// </summary>
    public static class CombatArenaBuilder
    {
        const string WeaponFolder = "Assets/Data/Weapons";
        const string EnemyFolder = "Assets/Data/Enemies";
        const string PrefabPath = "Assets/Prefabs/Player/PlayerCombat.prefab";
        const string ScenePath = "Assets/Scenes/M2_CombatArena.unity";

        static Transform _root;
        static Sprite _square;

        [MenuItem("Roughlike/Build/M2 Combat Arena")]
        public static void Build()
        {
            ProjectSetup.CreateLayers();
            PlaceholderArt.Generate();
            _square = PlaceholderArt.LoadSquare();

            var config = AssetDatabase.LoadAssetAtPath<MovementConfig>("Assets/Data/Player/PlayerMovement.asset");
            if (config == null)
            {
                GrayboxBuilder.Build();   // creates the movement config
                config = AssetDatabase.LoadAssetAtPath<MovementConfig>("Assets/Data/Player/PlayerMovement.asset");
            }

            WeaponData dagger = LoadOrCreateWeapon("RustedDagger", MakeDagger);
            WeaponData sword = LoadOrCreateWeapon("BellringerSword", MakeSword);
            WeaponData shield = LoadOrCreateWeapon("TowerShield", MakeShield);
            EnemyDefinition grunt = LoadOrCreateEnemy("DrownedGrunt", MakeGrunt);
            EnemyDefinition shooter = LoadOrCreateEnemy("DrownedSpitter", MakeShooter);

            GameObject prefab = BuildPlayerPrefab(config, sword, shield);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _root = new GameObject("Level").transform;
            BuildLevel(grunt, shooter);

            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            playerGo.transform.position = new Vector3(0f, 0.05f, 0f);
            var controller = playerGo.GetComponent<PlayerController>();
            var combat = playerGo.GetComponent<PlayerCombat>();

            BuildCamera(controller);
            BuildSystems(controller, combat, new[] { sword, dagger, shield }, new[] { grunt, shooter });

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Arena] M2 scene built: " + ScenePath);
        }

        // ------------------------------------------------------------------ assets

        static WeaponData LoadOrCreateWeapon(string name, System.Func<WeaponData> make)
        {
            Directory.CreateDirectory(WeaponFolder);
            string path = $"{WeaponFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (existing != null) return existing;
            var w = make();
            AssetDatabase.CreateAsset(w, path);
            return w;
        }

        static EnemyDefinition LoadOrCreateEnemy(string name, System.Func<EnemyDefinition> make)
        {
            Directory.CreateDirectory(EnemyFolder);
            string path = $"{EnemyFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;
            var e = make();
            AssetDatabase.CreateAsset(e, path);
            return e;
        }

        static AttackStep Step(string name, float dmg, float windup, float active, float recovery, float chain,
            Vector2 offset, Vector2 size, Vector2 kb, float stagger, float hitStop, float shake, float lunge, float moveScale)
            => new AttackStep
            {
                name = name, damage = dmg, windup = windup, active = active, recovery = recovery, chainFraction = chain,
                hitOffset = offset, hitSize = size, knockback = kb, stagger = stagger, hitStop = hitStop, shake = shake,
                lunge = lunge, moveScale = moveScale
            };

        static WeaponData MakeDagger() => DefaultContent.Dagger();
        static WeaponData MakeSword() => DefaultContent.Sword();
        static WeaponData MakeShield() => DefaultContent.Shield();
        static EnemyDefinition MakeGrunt()
        {
            var e = ScriptableObject.CreateInstance<EnemyDefinition>();
            e.displayName = "Drowned Grunt";
            e.archetype = EnemyArchetype.MeleeGrunt;
            e.maxHealth = 45f;
            e.size = new Vector2(0.9f, 1.5f);
            e.sprite = PlaceholderArt.Load(PlaceholderArt.GruntPath);
            e.tint = new Color(0.85f, 0.4f, 0.35f);
            e.patrolSpeed = 2f; e.chaseSpeed = 4.2f; e.patrolRadius = 4f;
            e.attackRange = 1.7f; e.telegraphTime = 0.55f; e.activeTime = 0.12f; e.recoverTime = 0.65f; e.attackCooldown = 0.9f;
            e.damage = 14f; e.knockback = new Vector2(7f, 4f); e.stagger = 12f;
            e.hitOffset = new Vector2(1.0f, 0.75f); e.hitSize = new Vector2(1.7f, 1.3f);
            e.staggerThreshold = 18f; e.staggerTime = 0.45f;
            return e;
        }

        static EnemyDefinition MakeShooter()
        {
            var e = ScriptableObject.CreateInstance<EnemyDefinition>();
            e.displayName = "Drowned Spitter";
            e.archetype = EnemyArchetype.RangedShooter;
            e.maxHealth = 30f;
            e.size = new Vector2(0.7f, 1.5f);
            e.sprite = PlaceholderArt.Load(PlaceholderArt.ShooterPath);
            e.tint = new Color(0.3f, 0.75f, 0.8f);
            e.chaseSpeed = 3.5f; e.patrolSpeed = 1.5f; e.patrolRadius = 3f;
            e.sightRange = 12f; e.loseSightRange = 16f;
            e.telegraphTime = 0.7f; e.activeTime = 0.1f; e.recoverTime = 0.5f; e.attackCooldown = 1.6f;
            e.damage = 10f; e.knockback = new Vector2(5f, 3f); e.stagger = 8f;
            e.preferredMinDistance = 4.5f; e.preferredMaxDistance = 8.5f; e.projectileSpeed = 10f;
            e.staggerThreshold = 14f; e.staggerTime = 0.5f;
            return e;
        }

        static GameObject BuildPlayerPrefab(MovementConfig config, WeaponData slot1, WeaponData slot2)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var controller = PlayerFactory.Build(config, PlayerFactory.InputKind.Device, PlaceholderArt.LoadPlayer());
            PlayerCombatSetup.Attach(controller, slot1, slot2);
            GameObject go = controller.gameObject;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ------------------------------------------------------------------ level

        static GameObject Block(string name, Vector2 centre, Vector2 size, string layer, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = centre;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.layer = LayerMask.NameToLayer(layer);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = col;
            go.AddComponent<BoxCollider2D>();
            return go;
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
            tm.characterSize = 0.12f;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 1f, 1f, 0.8f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 5;
        }

        static void BuildLevel(EnemyDefinition grunt, EnemyDefinition shooter)
        {
            Color ground = new Color(0.20f, 0.23f, 0.30f);
            Block("Floor", new Vector2(24f, -1.5f), new Vector2(76f, 3f), GameLayers.GroundName, ground);
            Block("Left wall", new Vector2(-13f, 10f), new Vector2(1f, 26f), GameLayers.GroundName, ground);
            Block("Right wall", new Vector2(61f, 10f), new Vector2(1f, 26f), GameLayers.GroundName, ground);
            Block("OneWay A", new Vector2(21f, 3.35f), new Vector2(6f, 0.3f), GameLayers.OneWayName, new Color(0.95f, 0.7f, 0.25f));
            Block("OneWay B", new Vector2(31f, 3.35f), new Vector2(6f, 0.3f), GameLayers.OneWayName, new Color(0.95f, 0.7f, 0.25f));

            Sign("COMBAT SANDBOX\nJ = Sword (combo x3)   K = Shield (tap parry, hold block)", new Vector2(2f, 6f));

            EnemyFactory.BuildDummy(new Vector2(9f, 0f), PlaceholderArt.Load(PlaceholderArt.DummyPath));
            Sign("Parry the grunt's swing (tap K just before it lands)\nfor a stun and bonus damage", new Vector2(20f, 6.2f));
            EnemyFactory.Build(grunt, new Vector2(20f, 0.05f));
            Sign("Spitters shoot projectiles: parry to reflect, roll to dodge", new Vector2(44f, 6.2f));
            EnemyFactory.Build(shooter, new Vector2(46f, 0.05f));
            EnemyFactory.Build(grunt, new Vector2(54f, 0.05f));
        }

        // ------------------------------------------------------------------ camera / systems

        internal static CinemachineCamera BuildCamera(PlayerController player)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.6f;
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
            vcam.Lens.OrthographicSize = 5.6f;
            vcam.Lens.NearClipPlane = -50f;
            vcam.Lens.FarClipPlane = 50f;

            var composer = vcamGo.AddComponent<CinemachinePositionComposer>();
            composer.Damping = new Vector3(0.25f, 0.35f, 0f);
            composer.Composition.DeadZone.Enabled = true;
            composer.Composition.DeadZone.Size = new Vector2(0.06f, 0.1f);
            composer.Composition.ScreenPosition = new Vector2(0f, -0.05f);

            vcamGo.AddComponent<CinemachineImpulseListener>();   // receives ScreenShake impulses
            return vcam;
        }

        static void BuildSystems(PlayerController player, PlayerCombat combat, WeaponData[] weapons, EnemyDefinition[] enemies)
        {
            var feedback = new GameObject("Combat Feedback");
            feedback.AddComponent<CombatFeedbackHub>();

            var debug = new GameObject("Debug");
            var move = debug.AddComponent<MovementDebugOverlay>();
            move.SetPlayer(player);
            debug.AddComponent<FallRespawn>().SetPlayer(player);

            var overlay = debug.AddComponent<CombatSandboxOverlay>();
            overlay.Bind(combat, player, weapons, enemies);

            var respawn = debug.AddComponent<DeathRespawn>();
            respawn.Bind(player.GetComponent<Health>(), player);
        }
    }
}


