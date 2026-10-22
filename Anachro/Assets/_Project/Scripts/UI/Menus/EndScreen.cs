using Anachronism.Network;
using FishNet;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    /// <summary>
    /// Écran de fin de partie : équipe gagnante, et victoire ou défaite du joueur local.
    /// Écoute MatchManager sans jamais le modifier ; « Menu principal » quitte la partie comme le menu pause.
    /// </summary>
    // TODO (Olivier) : à poser dans Boot avec GameEndMenu.uxml, puis récap objets trouvés / tirs ratés (O11-2).
    [RequireComponent(typeof(UIDocument))]
    public sealed class EndScreen : MonoBehaviour
    {
        private const string WinText = "Victoire !";
        private const string LoseText = "Défaite...";
        private const string VillainsTeamText = "Fugitifs";
        private const string GuardiansTeamText = "Détectives";
        private const string TimelineDestroyedText = "La temporalité est détruite";
        private const string TimelinePreservedText = "La temporalité est sauvée";

        private UIDocument _document;
        private MatchManager _match;
        private bool _isBound;
        private bool _isShown;
        private Winner _shownWinner;

        private VisualElement _root;
        private Label _resultLabel;
        private Label _teamLabel;
        private Label _timelineLabel;
        private Button _mainMenuButton;

        private void Awake()
        {
            // Vit dans Boot comme le HUD et le menu pause : doit survivre au chargement de la map.
            DontDestroyOnLoad(gameObject);
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // UIDocument reconstruit son arbre à chaque activation : on rebranche tout ici.
            _root = _document.rootVisualElement;
            _resultLabel = _root.Q<Label>("winLossText");
            _teamLabel = _root.Q<Label>("winningTeamText");
            _timelineLabel = _root.Q<Label>("timelineText");
            _mainMenuButton = _root.Q<Button>("mainMenuButton");

            if (_resultLabel == null || _teamLabel == null || _timelineLabel == null || _mainMenuButton == null)
            {
                Debug.LogError("[EndScreen] Éléments manquants dans l'UXML (GameEndMenu.uxml attendu).", this);
                enabled = false;
                return;
            }

            _mainMenuButton.clicked += OnMainMenuClicked;
            SetShown(false);
        }

        private void OnDisable()
        {
            if (_mainMenuButton != null)
                _mainMenuButton.clicked -= OnMainMenuClicked;
            Unbind();
        }

        private void Update()
        {
            // MatchManager vit dans la map : il apparaît et disparaît avec elle.
            if (_isBound && _match == null)
                Unbind();
            if (!_isBound && MatchManager.Instance != null)
                Bind(MatchManager.Instance);

            // Le vainqueur et la phase sont deux SyncVar : le vainqueur peut arriver une frame après la phase End.
            if (_isShown && _match != null && _match.Winner != _shownWinner)
                Refresh();
        }

        private void Bind(MatchManager match)
        {
            _match = match;
            _match.PhaseChanged += OnPhaseChanged;
            _isBound = true;
            OnPhaseChanged(_match.Phase);
        }

        private void Unbind()
        {
            if (!_isBound)
                return;

            if (_match != null)
                _match.PhaseChanged -= OnPhaseChanged;
            _match = null;
            _isBound = false;
            SetShown(false);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            SetShown(phase == GamePhase.End && _match.LocalRole != PlayerRole.None);
            if (_isShown)
                Refresh();
        }

        private void Refresh()
        {
            _shownWinner = _match.Winner;
            bool haveVillainsWon = _shownWinner == Winner.Villains;
            bool isLocalVillain = _match.LocalRole == PlayerRole.Villain;

            _teamLabel.text = haveVillainsWon ? VillainsTeamText : GuardiansTeamText;
            _timelineLabel.text = haveVillainsWon ? TimelineDestroyedText : TimelinePreservedText;
            _resultLabel.text = _shownWinner == Winner.None ? string.Empty
                : haveVillainsWon == isLocalVillain ? WinText : LoseText;
        }

        private void SetShown(bool isShown)
        {
            _isShown = isShown;
            if (_root != null)
                _root.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Même sortie que le menu pause : SteamLobby ferme proprement la connexion (et la session si c'est l'hôte).
        private void OnMainMenuClicked()
        {
            SteamLobby steamLobby = InstanceFinder.NetworkManager != null
                ? InstanceFinder.NetworkManager.GetComponent<SteamLobby>()
                : null;
            if (steamLobby != null)
                steamLobby.LeaveGame();
        }
    }
}
