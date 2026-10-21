using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism
{
    public class VictoryMenu : Menus_base_functions
    {
        public string mainMenuScene;
        public bool fugitiveWon;
        public bool playerWon;

        private string winText = "Vicoire!";
        private string loseText = "Défaite...";
        private string fugitiveWonText = "Fugitifs";
        private string detectiveWonText = "Détectives";
        private string timelinePreservedText = "La temporalité est sauvée";
        private string timelineDestroyedText = "La temporalité est détruite";
        
        private Label winLabel;
        private Label teamLabel;
        private Label timelineLabel;
        private Button mainMenuButton;

        private void Awake()
        {
            initialize();
            winLabel = root.Q<Label>("winLossText");
            teamLabel = root.Q<Label>("winningTeamText");
            timelineLabel = root.Q<Label>("timelineText");
            mainMenuButton = root.Q<Button>("mainMenuButton");

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

        private void OnEnable()
        {
            mainMenuButton.RegisterCallback<ClickEvent>(OnMainMenu);
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
            SceneManager.LoadScene(mainMenuScene);
        }
    }
}
