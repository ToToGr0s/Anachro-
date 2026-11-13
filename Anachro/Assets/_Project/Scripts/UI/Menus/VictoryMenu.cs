using UnityEngine;
using UnityEngine.UIElements;

namespace Anachronism
{
    public class VictoryMenu : Menus_base_functions
    {
        public string mainMenuScene;
        public bool fugitiveWon;
        public bool playerWon;

        private string winText = "Victoire !";
        private string loseText = "Défaite...";
        private string fugitiveWonText = "Fugitifs";
        private string detectiveWonText = "Détectives";
        private string timelinePreservedText = "La temporalité est sauvée";
        private string timelineDestroyedText = "La temporalité est détruite";
        
        private Label winLabel;
        private Label teamLabel;
        private Label timelineLabel;
        private Button mainMenuButton;

        private void OnEnable()
        {
            // Relu à chaque activation : UIDocument reconstruit son arbre, les éléments d'avant seraient morts.
            initialize();
            winLabel = root.Q<Label>("winLossText");
            teamLabel = root.Q<Label>("winningTeamText");
            timelineLabel = root.Q<Label>("timelineText");
            mainMenuButton = root.Q<Button>("mainMenuButton");

            Refresh();
            mainMenuButton.RegisterCallback<ClickEvent>(OnMainMenu);
        }

        // En ligne, le vainqueur n'est connu qu'à la fin de la partie : VictoryMenuBridge remplit
        // fugitiveWon / playerWon puis rappelle Refresh (avant, les textes n'étaient posés qu'une fois dans Awake).
        public void Refresh()
        {
            if (fugitiveWon)
            {
                teamLabel.text = fugitiveWonText;
                timelineLabel.text = timelineDestroyedText;
            }
            else
            {
                teamLabel.text = detectiveWonText;
                timelineLabel.text = timelinePreservedText;
            }

            if (playerWon)
            {
                winLabel.text = winText;
            }
            else
            {
                winLabel.text = loseText;
            }
        }

        private void OnDisable()
        {
            mainMenuButton.UnregisterCallback<ClickEvent>(OnMainMenu);
        }

        //events--------------------------------------------------------------------------------------------------------
        private void OnMainMenu(ClickEvent clicked)
        {
            LoadScene();
        }

        //functions-----------------------------------------------------------------------------------------------------
        private void LoadScene()
        {
            LeaveOnlineGame(mainMenuScene);
        }
    }
}
