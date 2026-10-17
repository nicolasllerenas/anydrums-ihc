using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnyDrums.EditorTools
{
    /// <summary>
    /// Revisa las escenas del estudio: cuenta prefabs y materiales faltantes y guarda una captura
    /// desde la posición del jugador en Logs/<escena>.png.
    /// </summary>
    public static class StudioScenesCheck
    {
        static readonly string[] Scenes = { "drumsAcustic", "drumsElectric" };

        [MenuItem("AnyDrums/Revisar escenas del estudio", priority = 20)]
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

                Capture($"Logs/{sceneName}.png");
            }
        }

        static void Capture(string path)
        {
            var rig = Object.FindFirstObjectByType<OVRCameraRig>();
            Vector3 position = rig != null ? rig.transform.position : Vector3.zero;
            Quaternion rotation = rig != null ? Quaternion.Euler(15f, rig.transform.eulerAngles.y, 0f) : Quaternion.Euler(15f, 0f, 0f);

            var go = new GameObject("PreviewCamera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(position + Vector3.up * 1.6f, rotation);
                cam.fieldOfView = 75f;
                cam.nearClipPlane = 0.05f;
                var rt = new RenderTexture(1600, 1000, 24) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(image);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
