using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Anachronism.Editor
{
    /// <summary>
    /// Build Windows Development pour tester le multi hors éditeur (HUD de dev inclus, logs détaillés).
    /// Utilisable depuis le menu ou en ligne de commande : -executeMethod Anachronism.Editor.DevBuild.BuildWindows
    /// </summary>
    public static class DevBuild
    {
        // Dossier ignoré par Git (/Builds/ dans .gitignore).
        private const string OutputPath = "Builds/Dev/Anachro.exe";
        private const string SteamAppIdFile = "steam_appid.txt";

        [MenuItem("Anachro/Build/Windows (Development)")]
        public static void BuildWindows()
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            // Hors lancement par Steam, Steamworks lit l'AppID (480 en dev) dans ce fichier à côté de l'exe.
            // Seulement en build de dev : un build release est lancé par Steam avec le vrai AppID (T13).
            if (summary.result == BuildResult.Succeeded)
                File.Copy(SteamAppIdFile, Path.Combine(Path.GetDirectoryName(OutputPath), SteamAppIdFile), true);
            Debug.Log($"[DevBuild] {summary.result} : {summary.totalErrors} erreur(s), {summary.totalWarnings} avertissement(s), {summary.totalSize / (1024 * 1024)} Mo → {OutputPath}");

            // En batchmode, un code de sortie non nul signale l'échec au script appelant.
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
