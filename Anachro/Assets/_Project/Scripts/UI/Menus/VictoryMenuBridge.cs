using Anachronism.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    /// <summary>
    /// Branche l'écran de fin d'Hanaé (VictoryMenu + GameEndMenu.uxml) sur la partie en ligne : affiché à la fin de la
    /// partie, avec l'équipe gagnante et la victoire ou défaite du joueur local. « Rejouer » (hôte seul) relance une partie
    /// avec les mêmes joueurs, comme le bouton du lobby. Écoute MatchManager sans jamais modifier son état.
    /// </summary>
    // Reprend la logique de l'ancien EndScreen (UI provisoire retirée).
    [RequireComponent(typeof(UIDocument), typeof(VictoryMenu))]
    public sealed class VictoryMenuBridge : MonoBehaviour
    {
        private UIDocument _document;
        private VictoryMenu _menu;
        private MatchManager _match;
        private bool _isShown;
        private Winner _shownWinner;

        private VisualElement _root;
        private Button _replayButton;
        private Label _replayStatus;

        private void Awake()
        {
            // Vit dans Boot comme l'écran de jeu : doit survivre au chargement de la map.
            DontDestroyOnLoad(gameObject);
            _document = GetComponent<UIDocument>();
            _menu = GetComponent<VictoryMenu>();
        }

        private void OnEnable()
        {
            // UIDocument reconstruit son arbre à chaque activation : on relit les éléments ici.
            _root = _document.rootVisualElement;
            _replayButton = _root.Q<Button>("replayButton");
            _replayStatus = _root.Q<Label>("replayStatus");
            if (_replayButton == null || _replayStatus == null)
            {
                Debug.LogError("[VictoryMenuBridge] Éléments manquants dans l'UXML (GameEndMenu.uxml attendu).", this);
                enabled = false;
                return;
            }

            _replayButton.clicked += OnReplayClicked;
            _isShown = true;
            SetShown(false);
        }

        private void OnDisable()
        {
            if (_replayButton != null)
                _replayButton.clicked -= OnReplayClicked;
            SetShown(false);
        }

        private void Update()
        {
            // MatchManager vit dans la map : il apparaît et disparaît avec elle.
            _match = MatchManager.Instance;

            // Le vainqueur et la phase sont deux SyncVar : on attend le vainqueur pour ne pas afficher une fausse défaite.
            bool isShown = _match != null && _match.Phase == GamePhase.End && _match.Winner != Winner.None
                           && _match.LocalRole != PlayerRole.None;
            SetShown(isShown);
            if (!isShown || _match.Winner == _shownWinner)
                return;

            _shownWinner = _match.Winner;
            bool haveVillainsWon = _shownWinner == Winner.Villains;
            _menu.fugitiveWon = haveVillainsWon;
            _menu.playerWon = haveVillainsWon == (_match.LocalRole == PlayerRole.Villain);
            _menu.Refresh();
            _replayStatus.text = string.Empty;
            SetVisible(_replayButton, _match.IsServerInitialized);
        }

        private void SetShown(bool isShown)
        {
            if (isShown == _isShown || _root == null)
                return;

            _isShown = isShown;
            _root.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isShown)
                _shownWinner = Winner.None;
        }

        private void OnReplayClicked()
        {
            // StartMatch est réservé au serveur : le bouton n'est montré qu'à l'hôte, on revérifie quand même.
            if (_match == null || !_match.IsServerInitialized)
                return;

            // En test solo, l'hôte rejoue le rôle qu'il avait choisi au lobby (celui de la partie qui se termine).
            PlayerRole soloRole = _match.LocalRole == PlayerRole.Villain ? PlayerRole.Villain : PlayerRole.Guardian;
            _replayStatus.text = _match.StartMatch(soloRole) switch
            {
                MatchStartResult.NotEnoughPlayers => "Pas assez de joueurs pour relancer la partie.",
                MatchStartResult.PlayersLoading => "Un joueur charge encore la map, réessaie dans un instant.",
                MatchStartResult.InvalidSettings => "Réglages de partie invalides (voir la console).",
                MatchStartResult.WrongPhase => "Une partie est déjà en cours.",
                _ => string.Empty
            };
        }

        private static void SetVisible(VisualElement element, bool isVisible) =>
            element.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
