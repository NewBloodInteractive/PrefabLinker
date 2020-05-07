using UnityEditor;
using UnityEngine;

namespace NewBlood
{
    public class PrefabLinkerWindow : EditorWindow
    {
        [MenuItem("Tools/Prefab Linker")]
        public static void ShowWindow()
        {
            GetWindow<PrefabLinkerWindow>("Prefab Linker", true);
        }

        GameObject instance;

        GameObject prefab;

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Prefab Instance");
                instance = (GameObject)EditorGUILayout.ObjectField(instance, typeof(GameObject), true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Prefab to Link");
                prefab = (GameObject)EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
            }

            if (GUILayout.Button("Link and Replace"))
            {
                var variant = PrefabLinker.CreatePrefabVariant(instance, prefab);
                PrefabUtility.SaveAsPrefabAsset(variant, AssetDatabase.GetAssetPath(instance));
                DestroyImmediate(variant);
            }

            if (GUILayout.Button("Link As New Scene Object"))
                PrefabLinker.CreatePrefabVariant(instance, prefab);
        }
    }
}
