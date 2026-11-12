using System;
using Anachronism.Core;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Slider = UnityEngine.UIElements.Slider;
using Toggle = UnityEngine.UIElements.Toggle;

namespace Anachronism
{
    public class InGameUI : Menus_base_functions
    {
        private string deregulationStaticText = "Dérèglement : ";
        private string bigDregulationStaticText = "grand";
        private string mediumDregulationStaticText = "moyen";
        private string smallDregulationStaticText = "petit";
        private string fullStaticText = "prêt";
        
        public bool isFugitive;
        public string mainMenuScene;

        private bool isPopUpOpen;

        // Menu pause, pop-up ou options ouverts (Échap, géré par InGameUIBridge).
        public bool IsPaused => pauseGroup.style.display == DisplayStyle.Flex
                                || popUpGroup.style.display == DisplayStyle.Flex
                                || Settings.style.display == DisplayStyle.Flex;
        
        //in game
        private VisualElement inGameUIGroup;
        private ProgressBar deregulationProgressBar;
        private Label timer;
        
        //deregulation tools
        private VisualElement deregulationToolsGroup;
        private Button bigTool;
        private Button mediumTool;
        private Button smallTool;
        private ProgressBar bigToolProgressBar;
        private ProgressBar mediumToolProgressBar;
        private ProgressBar smallToolProgressBar;
        
        //pause menu
        private VisualElement pauseGroup;
        private Button resumeButton;
        private Button settingsButton;
        private Button quitButton;
        
        //pop-up
        private VisualElement popUpGroup;
        private Button mainMenuButton;
        private Button quitQuitButton;
        
        //settings
        private VisualElement Settings;
        private Slider masterVolumeSlider;
        private Toggle fullScreenToggle;
        private Button closeButton;
        
        private void Awake()
        {
            initialize();
            
            //in game
            inGameUIGroup = root.Q<VisualElement>("inGameGroup");
            deregulationProgressBar = root.Q<ProgressBar>("deregulationBar");
            timer = root.Q<Label>("timer");

            //deregulation tools
            // Toujours lus, même pour le chasseur : sinon deregulationToolsGroup reste null et CloseMenu plante plus bas.
            deregulationToolsGroup = root.Q<VisualElement>("anachronismToolsGroup");
            bigTool = root.Q<Button>("bigDisruptionButton");
            mediumTool = root.Q<Button>("mediumDisruptionButton");
            smallTool = root.Q<Button>("smallDisruptionButton");
            bigToolProgressBar = root.Q<ProgressBar>("bigDisruptionBar");
            mediumToolProgressBar = root.Q<ProgressBar>("mediumDisruptionBar");
            smallToolProgressBar = root.Q<ProgressBar>("smallDisruptionBar");

            //pause
            pauseGroup = root.Q<VisualElement>("pauseMenuGroup");
            resumeButton = root.Q<Button>("resumeButton");
            settingsButton = root.Q<Button>("settingsButton");
            quitButton = root.Q<Button>("quitButton");

            //pop up
            popUpGroup = root.Q<VisualElement>("popUpGroup");
            mainMenuButton = root.Q<Button>("mainMenuButton");
            quitQuitButton = root.Q<Button>("quitQuitButton");

            //settings
            Settings = root.Q<VisualElement>("settingsGroup");
            masterVolumeSlider = root.Q<Slider>("masterVolumeSlider");
            fullScreenToggle = root.Q<Toggle>("fullScreenToggle");
            closeButton = root.Q<Button>("closeButton");
            
            OpenMenu(inGameUIGroup);
            SetFugitive(isFugitive);
            CloseMenu(pauseGroup);
            CloseMenu(popUpGroup);
            CloseMenu(Settings);
        }

        private void OnEnable()
        {
            //deregulation tools
            // Abonnés dans tous les cas : en ligne, le rôle arrive après OnEnable (SetFugitive).
            bigTool.RegisterCallback<ClickEvent>(OnBigToolClicked);
            mediumTool.RegisterCallback<ClickEvent>(OnMediumToolClicked);
            smallTool.RegisterCallback<ClickEvent>(OnSmallToolClicked);
            
            //pause
            resumeButton.RegisterCallback<ClickEvent>(OnResume);
            settingsButton.RegisterCallback<ClickEvent>(OnSettings);
            quitButton.RegisterCallback<ClickEvent>(OnQuit);
            
            //pop up
            mainMenuButton.RegisterCallback<ClickEvent>(OnMainMenu);
            quitQuitButton.RegisterCallback<ClickEvent>(OnQuitQuit);
            
            //settings
            masterVolumeSlider.RegisterCallback<ChangeEvent<float>>(OnMasterVolumeChanged);
            fullScreenToggle.RegisterCallback<ChangeEvent<bool>>(OnFullScreenToggleChanged);
            closeButton.RegisterCallback<ClickEvent>(OnClose);
        }

        private void OnDisable()
        {
            //deregulation tools
            bigTool.UnregisterCallback<ClickEvent>(OnBigToolClicked);
            mediumTool.UnregisterCallback<ClickEvent>(OnMediumToolClicked);
            smallTool.UnregisterCallback<ClickEvent>(OnSmallToolClicked);
            
            //pause
            resumeButton.UnregisterCallback<ClickEvent>(OnResume);
            settingsButton.UnregisterCallback<ClickEvent>(OnSettings);
            quitButton.UnregisterCallback<ClickEvent>(OnQuit);
            
            //pop up
            mainMenuButton.UnregisterCallback<ClickEvent>(OnMainMenu);
            quitQuitButton.UnregisterCallback<ClickEvent>(OnQuitQuit);
            
            //settings
            masterVolumeSlider.UnregisterCallback<ChangeEvent<float>>(OnMasterVolumeChanged);
            fullScreenToggle.UnregisterCallback<ChangeEvent<bool>>(OnFullScreenToggleChanged);
            closeButton.UnregisterCallback<ClickEvent>(OnClose);

        }
        
        //events--------------------------------------------------------------------------------------------------------
        //deregulation tools
        private void OnBigToolClicked(ClickEvent clicked)
        {
            //throw event
        }
        private void OnMediumToolClicked(ClickEvent clicked)
        {
            //throw event
        }
        private void OnSmallToolClicked(ClickEvent clicked)
        {
            //throw event
        }
        
        //pause
        private void OnResume(ClickEvent clicked)
        {
            if (isPopUpOpen == false)
            {
                ClosePause();
            }
        }
        private void OnSettings(ClickEvent clicked)
        {
            if (isPopUpOpen == false)
            {
                OpenMenu(Settings);
                CloseMenu(pauseGroup);
            }
        }
        private void OnQuit(ClickEvent clicked)
        {
            if (isPopUpOpen == false)
            {
                OpenMenu(popUpGroup);
                isPopUpOpen = true;
            }
        }
            
        //pop up
        private void OnMainMenu(ClickEvent clicked)
        {
            ClosePause();
            LoadScene();
        }
        private void OnQuitQuit(ClickEvent clicked)
        {
            ExitGame();
        }
            
        //settings
        private void OnMasterVolumeChanged(ChangeEvent<float> changed)
        {
            ChangeVolume(masterVolumeSlider);
        }

        private void OnFullScreenToggleChanged(ChangeEvent<bool> changed)
        {
            FullScreen(fullScreenToggle.value);
        }

        private void OnClose(ClickEvent clicked)
        {
            OpenMenu(pauseGroup);
            CloseMenu(Settings);
        }
        
        //functions-----------------------------------------------------------------------------------------------------
        //some functions here are to be used from other programs
        //in game
        public void UpdateDeregulationBar(float factor)
        {
            // La barre va de 0 à 100 par défaut : factor (0 à 1) seul la laissait presque vide.
            deregulationProgressBar.value = factor * deregulationProgressBar.highValue;
            deregulationProgressBar.title = deregulationStaticText + (int)(factor*100) + "%";
        }

        public void UpdateTimer(int minutes, int seconds)
        {
            timer.text = minutes.ToString("00") + ":" + seconds.ToString("00");
        }
            
        //deregulation tools
        public void UpdateDeregulationBar(ProgressBar bar, float factor)
        {
            bar.value = factor * bar.highValue;
            if (factor < 1)
            {
                bar.title = (int)(factor * 100) + "%";
            }
            else
            {
                bar.title = fullStaticText;
            }
        }
        
        //online : rôle et menu pause, appelés par InGameUIBridge
        public void SetFugitive(bool fugitive)
        {
            isFugitive = fugitive;
            if (isFugitive)
            {
                OpenMenu(deregulationToolsGroup);
            }
            else
            {
                CloseMenu(deregulationToolsGroup);
            }
        }

        public void OpenPause()
        {
            OpenMenu(pauseGroup);
            CloseMenu(inGameUIGroup);
            // La souris sert au menu ; le joueur local coupe ses entrées en écoutant GameMenuState.
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            GameMenuState.SetOpen(true);
        }

        // Échap ferme aussi la pop-up et les options : la pop-up n'avait pas de bouton annuler.
        public void ClosePause()
        {
            CloseMenu(pauseGroup);
            CloseMenu(popUpGroup);
            CloseMenu(Settings);
            isPopUpOpen = false;
            OpenMenu(inGameUIGroup);
            GameMenuState.SetOpen(false);
        }

        //pop up
        protected void LoadScene()
        {
            LeaveOnlineGame(mainMenuScene);
        }

        protected void ExitGame()
        {
            Application.Quit();
        }
            
        //settings
        private void FullScreen(bool fullscreen)
        {
            Screen.fullScreen = fullscreen;
        }

        private void ChangeVolume(Slider volumeSlider) //might add a parameter for the associated volume producer
        {
            float volume = volumeSlider.value;
            //complete function below
            // TODO (Hanaé) : brancher le volume général (le curseur ne fait encore rien).
            
        }
    }
}
