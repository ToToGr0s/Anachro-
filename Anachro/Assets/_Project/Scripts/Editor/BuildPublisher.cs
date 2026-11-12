using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Anachronism.Editor
{
    /// <summary>
    /// Publie les builds en pré-release GitHub (zip par plateforme) ; le workflow build-discord.yml l'annonce dans #releases.
    /// - « Anachro > Build > Windows + Linux + macOS → Discord » : build des trois plateformes, une seule release, un seul message ;
    /// - tout autre build non Development (bouton Build d'un Build Profile) : publié seul.
    /// Les builds Development (DevBuild) sont ignorés : ils servent aux tests multi en local et partiraient à chaque essai.
    /// L'envoi tourne dans une fenêtre PowerShell à part pour ne pas bloquer l'éditeur pendant l'upload.
    /// </summary>
    public sealed class BuildPublisher : IPostprocessBuildWithReport
    {
        // Pas de « # » dans les noms de menu : Unity le lit comme un raccourci (Shift).
        private const string MenuPath = "Anachro/Build/Publier les builds sur Discord";
        private const string EnabledPrefKey = "Anachro.BuildPublisher.Enabled";
        private const string ScriptRelativePath = "../Tools/publish-build.ps1";

        // Dossiers séparés : les builds écrivent chacun un dossier Anachro_Data qui s'écraserait.
        // macOS sans Build Profile (aucun dans le projet) : réglages du projet, architecture par défaut (Apple Silicon).
        private static readonly (string Platform, string ProfilePath, BuildTarget Target, string OutputPath)[] AllPlatforms =
        {
            ("Windows", "Assets/Settings/Build Profiles/Windows.asset", BuildTarget.StandaloneWindows64, "Builds/Windows/Anachro.exe"),
            ("Linux", "Assets/Settings/Build Profiles/Linux.asset", BuildTarget.StandaloneLinux64, "Builds/Linux/Anachro.x86_64"),
            ("macOS", null, BuildTarget.StandaloneOSX, "Builds/macOS/Anachro.app")
        };

        // Pendant le build des deux plateformes, le post-process ne publie pas : la release commune part à la fin.
        private static bool _isBuildingAll;

        public int callbackOrder => 1000;

        private static bool IsEnabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, true);
            set => EditorPrefs.SetBool(EnabledPrefKey, value);
        }

        [MenuItem(MenuPath)]
        private static void TogglePublishing()
        {
            IsEnabled = !IsEnabled;
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateTogglePublishing()
        {
            Menu.SetChecked(MenuPath, IsEnabled);
            return true;
        }

        [MenuItem("Anachro/Build/Windows + Linux + macOS → Discord")]
        public static void BuildAllAndPublish()
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            var manifest = new List<string>();

            _isBuildingAll = true;
            try
            {
                foreach ((string platform, string profilePath, BuildTarget target, string outputPath) in AllPlatforms)
                {
                    string executablePath = Path.Combine(projectPath, outputPath);
                    BuildReport report = Build(platform, profilePath, target, executablePath);
                    if (report == null)
                        return;

                    if (report.summary.result != BuildResult.Succeeded)
                    {
                        Debug.LogError($"[BuildPublisher] Build {platform} : {report.summary.result}, rien n'est publié.");
                        return;
                    }

                    if ((report.summary.options & BuildOptions.Development) != 0)
                    {
                        Debug.LogError($"[BuildPublisher] Le Build Profile {platform} est en Development : décocher « Development Build », rien n'est publié.");
                        return;
                    }

                    manifest.Add(GetManifestLine(platform, executablePath));
                }
            }
            finally
            {
                _isBuildingAll = false;
            }

            Publish(projectPath, manifest, "all");
        }

        // Null si le Build Profile attendu manque (erreur déjà affichée).
        private static BuildReport Build(string platform, string profilePath, BuildTarget target, string executablePath)
        {
            if (profilePath == null)
            {
                return BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                    locationPathName = executablePath,
                    target = target,
                    options = BuildOptions.None
                });
            }

            BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            if (profile == null)
            {
                Debug.LogError($"[BuildPublisher] Build Profile {platform} ({profilePath}) introuvable : rien n'est publié.");
                return null;
            }

            return BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = executablePath,
                options = BuildOptions.None
            });
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            BuildSummary summary = report.summary;
            if (_isBuildingAll || !IsEnabled || (summary.options & BuildOptions.Development) != 0)
                return;

            string platform = summary.platform switch
            {
                BuildTarget.StandaloneWindows64 => "Windows",
                BuildTarget.StandaloneLinux64 => "Linux",
                BuildTarget.StandaloneOSX => "macOS",
                _ => null
            };
            if (platform == null)
                return;

            string projectPath = Path.GetDirectoryName(Application.dataPath);
            Publish(projectPath, new List<string> { GetManifestLine(platform, Path.GetFullPath(summary.outputPath)) }, platform);
        }

        // Une ligne par plateforme : « Plateforme|dossier du build|entrée|entrée… », lue par publish-build.ps1.
        private static string GetManifestLine(string platform, string executablePath)
        {
            string buildFolder = Path.GetDirectoryName(Path.GetFullPath(executablePath));
            IEnumerable<string> entries = GetPlayerEntries(buildFolder, Path.GetFileNameWithoutExtension(executablePath));
            return string.Join("|", new[] { platform, buildFolder }.Concat(entries));
        }

        private static void Publish(string projectPath, List<string> manifest, string label)
        {
            string scriptPath = Path.GetFullPath(Path.Combine(projectPath, ScriptRelativePath));
            if (!File.Exists(scriptPath))
            {
                Debug.LogError($"[BuildPublisher] {scriptPath} introuvable : build non publié.");
                return;
            }

            string manifestPath = Path.Combine(Path.GetTempPath(), $"anachro-build-{label.ToLowerInvariant()}.txt");
            File.WriteAllLines(manifestPath, manifest);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Manifest \"{manifestPath}\" -ProjectFolder \"{projectPath}\"",
                    WorkingDirectory = projectPath,
                    UseShellExecute = true
                });
                Debug.Log($"[BuildPublisher] Publication lancée ({label}, fenêtre PowerShell).");
            }
            catch (Win32Exception exception)
            {
                Debug.LogError($"[BuildPublisher] PowerShell introuvable (éditeur Windows seulement) : build non publié. {exception.Message}");
            }
        }

        // Seulement les fichiers du player : le dossier de sortie peut contenir d'autres builds (ex : Builds/Dev),
        // des zips ou le dossier BackUpThisFolder_ButDontShipItWithYourGame. La liste des fichiers du BuildReport
        // n'est pas fiable ici, le build n'étant pas encore terminé au post-process.
        private static IEnumerable<string> GetPlayerEntries(string buildFolder, string productName)
        {
            // macOS : tout le jeu est dans le bundle Anachro.app.
            string[] playerFolders = { productName + "_Data", productName + ".app", "MonoBleedingEdge", "D3D12" };
            string[] playerExtensions = { ".exe", ".dll", ".so", ".x86_64" };

            IEnumerable<string> folders = playerFolders.Where(folder => Directory.Exists(Path.Combine(buildFolder, folder)));
            IEnumerable<string> files = Directory.GetFiles(buildFolder)
                .Select(Path.GetFileName)
                .Where(file => playerExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));
            return folders.Concat(files);
        }
    }
}
