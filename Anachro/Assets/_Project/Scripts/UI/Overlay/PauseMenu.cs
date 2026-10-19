using Anachronism.Core;
using Anachronism.Network;
using Anachronism.Npc;
using FishNet;
using FishNet.Managing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    /// <summary>
    /// Menu Échap en ligne : reprendre, quitter, et pour l'hôte les réglages des PNJ (recharger, re-tirer les skins).
    /// L'hôte est le serveur : ses boutons appellent directement le gameplay serveur, comme « Lancer la partie ».
    /// </summary>
    // TODO (Olivier) : version provisoire posée par Théo, à reprendre (UXML/USS).
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseMenu : MonoBehaviour
    {
        private UIDocument _document;
        private NetworkManager _networkManager;
        private SteamLobby _steamLobby;

        private VisualElement _pausePanel;
        private VisualElement _hostSection;
        private Button _resumeButton;
        private Button _reloadNpcsButton;
        private Button _rerollSkinsButton;
        private Button _leaveButton;
        private Label _statusLabel;

        private void Awake()
        {
            // Vit dans Boot, comme le menu du lobby, et doit survivre au chargement de la map.
            DontDestroyOnLoad(gameObject);
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // UIDocument reconstruit son arbre à chaque activation : on rebranche tout ici.
            VisualElement root = _document.rootVisualElement;
            _pausePanel = root.Q<VisualElement>("pause-panel");
            _hostSection = root.Q<VisualElement>("host-section");
            _resumeButton = root.Q<Button>("resume-button");
            _reloadNpcsButton = root.Q<Button>("reload-npcs-button");
            _rerollSkinsButton = root.Q<Button>("reroll-skins-button");
            _leaveButton = root.Q<Button>("pause-leave-button");
            _statusLabel = root.Q<Label>("pause-status");

            _resumeButton.clicked += Close;
            _reloadNpcsButton.clicked += OnReloadNpcsClicked;
            _rerollSkinsButton.clicked += OnRerollSkinsClicked;
            _leaveButton.clicked += OnLeaveClicked;

            SetVisible(_pausePanel, false);
        }

        private void OnDisable()
        {
            _resumeButton.clicked -= Close;
            _reloadNpcsButton.clicked -= OnReloadNpcsClicked;
            _rerollSkinsButton.clicked -= OnRerollSkinsClicked;
            _leaveButton.clicked -= OnLeaveClicked;
            GameMenuState.SetOpen(false);
        }

        private void Start()
        {
            _networkManager = InstanceFinder.NetworkManager;
            if (_networkManager == null)
            {
                Debug.LogError("[PauseMenu] Pas de NetworkManager : lancer le jeu depuis Boot.");
                enabled = false;
                return;
            }

            _steamLobby = _networkManager.GetComponent<SteamLobby>();
        }

        private void Update()
        {
            bool isOnline = _networkManager.IsClientStarted;

            // Déconnecté avec le menu ouvert : on le ferme, le menu du lobby reprend la main.
            if (GameMenuState.IsOpen && !isOnline)
            {
                Close();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (GameMenuState.IsOpen)
                Close();
            else if (isOnline)
                Open();
        }

        private void Open()
        {
            SetVisible(_pausePanel, true);
            SetVisible(_hostSection, _networkManager.IsServerStarted);
            _statusLabel.text = string.Empty;

            // Au lobby, aucun joueur local n'écoute le menu : on libère la souris ici aussi.
            // UnityEngine.Cursor : UIElements a aussi un type Cursor.
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            GameMenuState.SetOpen(true);
        }

        private void Close()
        {
            SetVisible(_pausePanel, false);
            GameMenuState.SetOpen(false);
        }

        private void OnReloadNpcsClicked()
        {
            NpcDestinationManager npcs = FindHostNpcs();
            if (npcs == null)
                return;

            npcs.RespawnAll();
            _statusLabel.text = "PNJ rechargés.";
        }

        private void OnRerollSkinsClicked()
        {
            NpcDestinationManager npcs = FindHostNpcs();
            if (npcs == null)
                return;

            npcs.RerollSkins();
            _statusLabel.text = "Nouveaux skins tirés.";
        }

        // Cherché au clic (jamais dans Update) : les PNJ vivent dans la map, le menu dans Boot.
        private NpcDestinationManager FindHostNpcs()
        {
            if (!_networkManager.IsServerStarted)
                return null;

            NpcDestinationManager npcs = FindAnyObjectByType<NpcDestinationManager>();
            if (npcs == null)
                _statusLabel.text = "Pas de PNJ dans cette map.";
            return npcs;
        }

        private void OnLeaveClicked()
        {
            Close();
            if (_steamLobby != null)
                _steamLobby.LeaveGame();
        }

        private static void SetVisible(VisualElement element, bool isVisible) =>
            element.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
