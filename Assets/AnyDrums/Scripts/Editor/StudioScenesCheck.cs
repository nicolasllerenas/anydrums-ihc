using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnyDrums.EditorTools
{
    /// <summary>Revisa la escena del estudio: cuenta prefabs y materiales faltantes.</summary>
    public static class StudioScenesCheck
    {
        static readonly string[] Scenes = { "Studio" };

        [MenuItem("AnyDrums/Revisar escena del estudio", priority = 20)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Check();
        }

        /// <summary>Igual que <see cref="Run"/> pero sin preguntar por cambios sin guardar (para la línea de comandos).</summary>
        public static void Check()
        {
            foreach (string sceneName in Scenes)
            {
                string path = AssetDatabase.FindAssets($"{sceneName} t:Scene")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == sceneName);
                if (path == null)
                {
                    Debug.LogWarning($"[AnyDrums] No se encontró la escena {sceneName}.");
                    continue;
                }
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var missingPrefabs = all.Where(PrefabUtility.IsPrefabAssetMissing).Select(g => g.name).ToArray();
                var missingMaterials = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(r => r.sharedMaterials.Any(m => m == null))
                    .Select(r => r.name).Distinct().ToArray();
                Debug.Log($"[AnyDrums] {sceneName}: {all.Length} objetos, prefabs faltantes: {missingPrefabs.Length} " +
                          $"[{string.Join(", ", missingPrefabs.Take(10))}], renderers sin material: {missingMaterials.Length} " +
                          $"[{string.Join(", ", missingMaterials.Take(10))}]");
            }
        }
    }
}
