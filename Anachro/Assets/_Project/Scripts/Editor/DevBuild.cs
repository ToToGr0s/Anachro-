using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Anachronism.Editor
{
    /// <summary>
    /// Build Windows Development pour tester le multi hors éditeur (HUD de dev inclus, logs détaillés).
    /// Fenêtré et redimensionnable pour lancer plusieurs instances côte à côte ; le réglage plein écran du
    /// projet (release) est rétabli après le build. Taille au lancement : -screen-width 960 -screen-height 540.
    /// Utilisable depuis le menu ou en ligne de commande : -executeMethod Anachronism.Editor.DevBuild.BuildWindows
    /// </summary>
    public static class DevBuild
    {
        // Relatif au dossier du projet Unity, ignoré par Git (/Builds/ dans .gitignore).
        private const string OutputPath = "Builds/Dev/Anachro.exe";
        private const string SteamAppIdFile = "steam_appid.txt";
        private const int WindowWidth = 1280;
        private const int WindowHeight = 720;

        [MenuItem("Anachro/Build/Windows (Development)")]
        public static void BuildWindows()
        {
            // Absolu depuis le dossier du projet : en ligne de commande, le dossier courant peut être ailleurs,
            // et le build comme la copie de steam_appid.txt doivent viser le même endroit.
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string outputPath = Path.Combine(projectPath, OutputPath);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildSummary summary = BuildWindowed(options);

            // Hors lancement par Steam, Steamworks lit l'AppID (480 en dev) dans ce fichier à côté de l'exe.
            // Seulement en build de dev : un build release est lancé par Steam avec le vrai AppID (T13).
            if (summary.result == BuildResult.Succeeded)
                CopySteamAppId(projectPath, Path.GetDirectoryName(outputPath));

            Debug.Log($"[DevBuild] {summary.result} : {summary.totalErrors} erreur(s), {summary.totalWarnings} avertissement(s), {summary.totalSize / (1024 * 1024)} Mo → {outputPath}");

            // En batchmode, un code de sortie non nul signale l'échec au script appelant.
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        private static void CopySteamAppId(string projectPath, string outputFolder)
        {
            string source = Path.Combine(projectPath, SteamAppIdFile);
            if (!File.Exists(source))
            {
                Debug.LogError($"[DevBuild] {source} introuvable : Steam ne s'initialisera pas dans ce build.");
                return;
            }

            File.Copy(source, Path.Combine(outputFolder, SteamAppIdFile), true);
        }

        private static BuildSummary BuildWindowed(BuildPlayerOptions options)
        {
            FullScreenMode fullScreenMode = PlayerSettings.fullScreenMode;
            bool isNativeResolution = PlayerSettings.defaultIsNativeResolution;
            int width = PlayerSettings.defaultScreenWidth;
            int height = PlayerSettings.defaultScreenHeight;
            bool isResizable = PlayerSettings.resizableWindow;

            try
            {
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultIsNativeResolution = false;
                PlayerSettings.defaultScreenWidth = WindowWidth;
                PlayerSettings.defaultScreenHeight = WindowHeight;
                PlayerSettings.resizableWindow = true;
                return BuildPipeline.BuildPlayer(options).summary;
            }
            finally
            {
                PlayerSettings.fullScreenMode = fullScreenMode;
                PlayerSettings.defaultIsNativeResolution = isNativeResolution;
                PlayerSettings.defaultScreenWidth = width;
                PlayerSettings.defaultScreenHeight = height;
                PlayerSettings.resizableWindow = isResizable;
            }
        }
    }
}
