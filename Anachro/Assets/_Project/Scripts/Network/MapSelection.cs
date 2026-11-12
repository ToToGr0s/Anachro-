using System;
using System.Collections.Generic;
using FishNet.Component.Scenes;
using FishNet.Managing;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Carte de la partie (T09-3), choisie par l'hôte avant de créer la partie : c'est la scène que FishNet charge à la
    /// connexion (DefaultScene). Une fois la partie créée, la carte est verrouillée pour tout le monde.
    /// Sur le NetworkManager de Boot, à côté de DefaultScene.
    /// </summary>
    public sealed class MapSelection : MonoBehaviour
    {
        [Serializable]
        private struct MapEntry
        {
            public string DisplayName;
            [Tooltip("Chemin de la scène, comme dans les Build Settings (Assets/_Project/Scenes/Maps/….unity).")]
            public string ScenePath;
        }

        [SerializeField] private MapEntry[] _maps = new MapEntry[0];

        private NetworkManager _networkManager;
        private DefaultScene _defaultScene;
        private readonly List<string> _names = new List<string>();

        public IReadOnlyList<string> Names => _names;
        public int SelectedIndex { get; private set; } = -1;
        /// <summary>Faux dès que ce poste héberge ou a rejoint une partie : la carte vient alors de l'hôte.</summary>
        public bool CanSelect => _networkManager != null && _networkManager.IsOffline;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            _defaultScene = GetComponent<DefaultScene>();
            foreach (MapEntry map in _maps)
                _names.Add(map.DisplayName);

            // Carte déjà réglée dans DefaultScene : c'est la sélection de départ.
            string current = _defaultScene != null ? _defaultScene.GetOnlineScene() : string.Empty;
            SelectedIndex = Array.FindIndex(_maps, map => map.ScenePath == current);
        }

        /// <summary>Faux si l'index est hors liste ou si une partie est déjà créée ou rejointe.</summary>
        public bool Select(int index)
        {
            if (!CanSelect || _defaultScene == null || index < 0 || index >= _maps.Length)
                return false;

            _defaultScene.SetOnlineScene(_maps[index].ScenePath);
            SelectedIndex = index;
            return true;
        }
    }
}
