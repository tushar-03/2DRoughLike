using UnityEditor;
using UnityEngine;
using Roughlike.Core;

namespace Roughlike.Editor
{
    /// <summary>One-time project configuration: physics layers and 2D physics defaults.</summary>
    public static class ProjectSetup
    {
        [MenuItem("Roughlike/Setup/Create Layers")]
        public static void CreateLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            bool changed = false;
            for (int i = 0; i < GameLayers.AllNames.Length; i++)
            {
                int index = GameLayers.FirstUserLayer + i;
                SerializedProperty slot = layers.GetArrayElementAtIndex(index);
                if (slot.stringValue != GameLayers.AllNames[i])
                {
                    slot.stringValue = GameLayers.AllNames[i];
                    changed = true;
                }
            }

            if (changed)
            {
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log("[Setup] Physics layers created.");
            }
        }
    }
}
