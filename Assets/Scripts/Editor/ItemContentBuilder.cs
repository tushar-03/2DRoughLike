using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Items;

namespace Roughlike.Editor
{
    /// <summary>
    /// Saves the default weapons, skills, gear, affixes and mutations as ScriptableObject assets and wires up the
    /// loot database. Existing assets are kept (so hand-tuning survives); the database is refreshed so new content shows up.
    /// </summary>
    public static class ItemContentBuilder
    {
        public const string DatabasePath = "Assets/Data/LootDatabase.asset";

        [MenuItem("Roughlike/Build/Item Content Assets")]
        public static LootDatabase Build()
        {
            LootDatabase source = DefaultContent.CreateDatabase();
            // remember these now: SaveAll destroys in-memory duplicates of assets that already exist
            string start1 = source.startingWeapon1.name, start2 = source.startingWeapon2.name, startOutfit = source.startingOutfit.name;

            var weapons = SaveAll("Assets/Data/Weapons", source.weapons);
            var skills = SaveAll("Assets/Data/Skills", source.skills);
            var amulets = SaveAll("Assets/Data/Gear", source.amulets);
            var outfits = SaveAll("Assets/Data/Gear", source.outfits);
            var affixes = SaveAll("Assets/Data/Affixes", source.affixes);
            var mutations = SaveAll("Assets/Data/Mutations", source.mutations);

            var db = AssetDatabase.LoadAssetAtPath<LootDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<LootDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            db.weapons = weapons;
            db.skills = skills;
            db.amulets = amulets;
            db.outfits = outfits;
            db.affixes = affixes;
            db.mutations = mutations;
            db.startingWeapon1 = Find(weapons, start1);
            db.startingWeapon2 = Find(weapons, start2);
            db.startingOutfit = Find(outfits, startOutfit);
            EditorUtility.SetDirty(db);

            Roughlike.Editor.Art.WeaponArtBuilder.Assign();
            AssetDatabase.SaveAssets();
            Debug.Log("[Items] Item content assets built.");
            return db;
        }

        static T Find<T>(T[] list, string name) where T : Object
        {
            foreach (var o in list) if (o != null && o.name == name) return o;
            return null;
        }

        /// <summary>Saves each in-memory ScriptableObject as an asset, or returns the existing asset of that name.</summary>
        static T[] SaveAll<T>(string folder, T[] items) where T : ScriptableObject
        {
            Directory.CreateDirectory(folder);
            var result = new List<T>();
            foreach (var item in items)
            {
                string path = $"{folder}/{item.name}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<T>(path);
                if (existing != null)
                {
                    PatchNewFields(existing, item);
                    Object.DestroyImmediate(item);
                    result.Add(existing);
                }
                else
                {
                    AssetDatabase.CreateAsset(item, path);
                    result.Add(item);
                }
            }
            return result.ToArray();
        }

        /// <summary>Older assets (from M2) lack fields added since; fill only those so other tuning is untouched.</summary>
        static void PatchNewFields<T>(T existing, T fresh) where T : ScriptableObject
        {
            if (existing is WeaponData w && fresh is WeaponData f)
            {
                if (w.affinity == Roughlike.Core.StatColor.None) w.affinity = f.affinity;
                if (string.IsNullOrEmpty(w.description)) w.description = f.description;
                EditorUtility.SetDirty(w);
            }
        }
    }
}
