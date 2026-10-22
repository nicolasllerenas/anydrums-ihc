using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AnyDrums.EditorTools
{
    /// <summary>Compila el APK para Meta Quest con las escenas de Build Settings (Builds/AnyDrums.apk).</summary>
    public static class QuestBuild
    {
        public const string ApkPath = "Builds/AnyDrums.apk";

        [MenuItem("AnyDrums/Compilar APK para Quest", priority = 50)]
        static void BuildFromMenu()
        {
            if (Build()) EditorUtility.RevealInFinder(ApkPath);
        }

        /// <summary>Unity -projectPath . -executeMethod AnyDrums.EditorTools.QuestBuild.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine() => EditorApplication.Exit(Build() ? 0 : 1);

        static bool Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"[AnyDrums] APK {(ok ? "listo" : "falló")}: {ApkPath} ({report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errores). " +
                      $"Escenas: {string.Join(", ", scenes)}");
            return ok;
        }
    }
}
