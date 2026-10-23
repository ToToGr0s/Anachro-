using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Anachronism.Editor
{
    /// <summary>
    /// Après chaque build non Development réussi (Windows ou Linux), zippe le build et le publie en pré-release
    /// GitHub ; le workflow build-discord.yml l'annonce ensuite dans #releases.
    /// Les builds Development (DevBuild) sont ignorés : ils servent aux tests multi en local et partiraient à chaque essai.
    /// L'envoi tourne dans une fenêtre PowerShell à part pour ne pas bloquer l'éditeur pendant l'upload.
    /// </summary>
    public sealed class BuildPublisher : IPostprocessBuildWithReport
    {
        private const string MenuPath = "Anachro/Build/Publier les builds sur #releases";
        private const string EnabledPrefKey = "Anachro.BuildPublisher.Enabled";
        private const string ScriptRelativePath = "../Tools/publish-build.ps1";

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

        public void OnPostprocessBuild(BuildReport report)
        {
            BuildSummary summary = report.summary;
            if (!IsEnabled || (summary.options & BuildOptions.Development) != 0)
                return;

            string platform = summary.platform switch
            {
                BuildTarget.StandaloneWindows64 => "Windows",
                BuildTarget.StandaloneLinux64 => "Linux",
                _ => null
            };
            if (platform == null)
                return;

            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string scriptPath = Path.GetFullPath(Path.Combine(projectPath, ScriptRelativePath));
            if (!File.Exists(scriptPath))
            {
                Debug.LogError($"[BuildPublisher] {scriptPath} introuvable : build non publié.");
                return;
            }

            string executablePath = Path.GetFullPath(summary.outputPath);
            string buildFolder = Path.GetDirectoryName(executablePath);
            string entriesFile = Path.Combine(Path.GetTempPath(), $"anachro-build-{platform}.txt");
            File.WriteAllLines(entriesFile, GetPlayerEntries(buildFolder, Path.GetFileNameWithoutExtension(executablePath)));

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Platform {platform} " +
                            $"-BuildFolder \"{buildFolder}\" -EntriesFile \"{entriesFile}\" -ProjectFolder \"{projectPath}\"",
                WorkingDirectory = projectPath,
                UseShellExecute = true
            });
            Debug.Log($"[BuildPublisher] Publication du build {platform} lancée (fenêtre PowerShell).");
        }

        // Seulement les fichiers du player : le dossier de sortie peut contenir d'autres builds (ex : Builds/Dev),
        // des zips ou le dossier BackUpThisFolder_ButDontShipItWithYourGame. La liste des fichiers du BuildReport
        // n'est pas fiable ici, le build n'étant pas encore terminé au post-process.
        private static IEnumerable<string> GetPlayerEntries(string buildFolder, string productName)
        {
            string[] playerFolders = { productName + "_Data", "MonoBleedingEdge", "D3D12" };
            string[] playerExtensions = { ".exe", ".dll", ".so", ".x86_64" };

            IEnumerable<string> folders = playerFolders.Where(folder => Directory.Exists(Path.Combine(buildFolder, folder)));
            IEnumerable<string> files = Directory.GetFiles(buildFolder)
                .Select(Path.GetFileName)
                .Where(file => playerExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));
            return folders.Concat(files);
        }
    }
}
