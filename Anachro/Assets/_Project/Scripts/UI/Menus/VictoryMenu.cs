using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    public sealed class VictoryMenu : MenuBase
    {
        private const string WinText = "Victoire !";
        private const string LoseText = "Défaite...";
        private const string FugitiveWonText = "Fugitifs";
        private const string DetectiveWonText = "Détectives";
        private const string TimelinePreservedText = "La temporalité est sauvée";
        private const string TimelineDestroyedText = "La temporalité est détruite";

        [SerializeField] private string _mainMenuScene;

        private Label _winLabel;
        private Label _teamLabel;
        private Label _timelineLabel;
        private Button _mainMenuButton;

        private void OnEnable()
        {
            _winLabel = Root.Q<Label>("winLossText");
            _teamLabel = Root.Q<Label>("winningTeamText");
            _timelineLabel = Root.Q<Label>("timelineText");
            _mainMenuButton = Root.Q<Button>("mainMenuButton");

            _mainMenuButton.clicked += OnMainMenu;
        }

        private void OnDisable()
        {
            _mainMenuButton.clicked -= OnMainMenu;
        }

        /// <summary>
        /// Affiche le résultat. Le résultat n'est connu qu'en fin de partie : à appeler par le gameplay à ce moment-là.
        /// </summary>
        public void Show(bool hasFugitiveWon, bool hasPlayerWon)
        {
            _teamLabel.text = hasFugitiveWon ? FugitiveWonText : DetectiveWonText;
            _timelineLabel.text = hasFugitiveWon ? TimelineDestroyedText : TimelinePreservedText;
            _winLabel.text = hasPlayerWon ? WinText : LoseText;
        }

        //events--------------------------------------------------------------------------------------------------------
        private void OnMainMenu()
        {
            // TODO (Hana) : passer par SteamLobby pour quitter la partie (arrêt de FishNet) au lieu de charger la scène.
            SceneManager.LoadScene(_mainMenuScene);
        }
    }
}
