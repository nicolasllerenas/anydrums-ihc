using System.IO;
using System.Linq;
using UnityEditor;

namespace AnyDrums.EditorTools
{
    /// <summary>Deja en Build Settings el menú primero y luego el estudio (las dos escenas del informe).</summary>
    public static class BuildSettingsUtil
    {
        // Escenas anteriores del estudio, reemplazadas por Studio (se conservan en el proyecto).
        static readonly string[] Superseded = { "drumsAcustic", "drumsElectric" };

        public static void Apply()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.path != MainMenuSceneBuilder.ScenePath && s.path != StudioSceneBuilder.ScenePath)
                .Where(s => !Superseded.Contains(Path.GetFileNameWithoutExtension(s.path)))
                .ToList();
            if (File.Exists(StudioSceneBuilder.ScenePath)) scenes.Insert(0, new EditorBuildSettingsScene(StudioSceneBuilder.ScenePath, true));
            if (File.Exists(MainMenuSceneBuilder.ScenePath)) scenes.Insert(0, new EditorBuildSettingsScene(MainMenuSceneBuilder.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
