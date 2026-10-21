using System;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
            if (isFugitive)
            {
                deregulationToolsGroup = root.Q<VisualElement>("anachronismToolsGroup");
                bigTool = root.Q<Button>("bigDisruptionButton");
                mediumTool = root.Q<Button>("mediumDisruptionButton");
                smallTool = root.Q<Button>("smallDisruptionButton");
                bigToolProgressBar = root.Q<ProgressBar>("bigDisruptionBar");
                mediumToolProgressBar = root.Q<ProgressBar>("mediumDisruptionBar");
                smallToolProgressBar = root.Q<ProgressBar>("smallDisruptionBar");
            }

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
            if(isFugitive)
            {
                OpenMenu(deregulationToolsGroup);
            }
            else
            {
                CloseMenu(deregulationToolsGroup);
            }
            CloseMenu(pauseGroup);
            CloseMenu(popUpGroup);
            CloseMenu(Settings);
        }

        private void OnEnable()
        {
            //deregulation tools
            if (isFugitive)
            {
                bigTool.RegisterCallback<ClickEvent>(OnBigToolClicked);
                mediumTool.RegisterCallback<ClickEvent>(OnMediumToolClicked);
                smallTool.RegisterCallback<ClickEvent>(OnSmallToolClicked);
            }
            
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
            if (isFugitive)
            {
                bigTool.UnregisterCallback<ClickEvent>(OnBigToolClicked);
                mediumTool.UnregisterCallback<ClickEvent>(OnMediumToolClicked);
                smallTool.UnregisterCallback<ClickEvent>(OnSmallToolClicked);
            }
            
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
                OpenMenu(inGameUIGroup);
                CloseMenu(pauseGroup);
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
            deregulationProgressBar.value = factor;
            deregulationProgressBar.title = deregulationStaticText + (int)(factor*100) + "%";
        }

        public void UpdateTimer(int minutes, int seconds)
        {
            timer.text = minutes.ToString("00") + ":" + seconds.ToString("00");
        }
            
        //deregulation tools
        public void UpdateDeregulationBar(ProgressBar bar, float factor)
        {
            bar.value = factor;
            if (factor < 1)
            {
                bar.title = (int)(factor * 100) + "%";
            }
            else
            {
                bar.title = fullStaticText;
            }
        }
        
        //pop up
        protected void LoadScene()
        {
            SceneManager.LoadScene(mainMenuScene);
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
            
        }
    }
}
