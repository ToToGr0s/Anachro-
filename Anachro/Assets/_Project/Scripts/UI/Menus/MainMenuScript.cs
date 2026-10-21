using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism
{
    public class MainMenuScript : Menus_base_functions
    {
        public string playScene;
        
        //main
        private VisualElement Main;
        private Button startButton;
        private Button settingsButton;
        private Button quitButton;
        
        //settings
        private VisualElement Settings;
        private Slider masterVolumeSlider;
        private Toggle fullScreenToggle;
        private Button closeButton;

        private void Awake()
        {
            initialize();

            //main
            Main = root.Q<VisualElement>("MainMenuGroup");
            startButton = root.Q<Button>("startButton");
            settingsButton = root.Q<Button>("settingsButton");
            quitButton = root.Q<Button>("quitButton");
            
            //settings
            Settings = root.Q<VisualElement>("SettingsMenuGroup");
            masterVolumeSlider = root.Q<Slider>("masterVolumeSlider");
            fullScreenToggle = root.Q<Toggle>("fullScreenToggle");
            closeButton = root.Q<Button>("closeButton");
            
            OpenMenu(Main);
            CloseMenu(Settings);

        }

        private void OnEnable()
        {
            //main
            startButton.RegisterCallback<ClickEvent>(OnSartClicked);
            settingsButton.RegisterCallback<ClickEvent>(OnSettingsClicked);
            quitButton.RegisterCallback<ClickEvent>(OnQuitClicked);
            
            //settings
            masterVolumeSlider.RegisterCallback<ChangeEvent<float>>(OnMasterVolumeChanged);
            fullScreenToggle.RegisterCallback<ChangeEvent<bool>>(OnFullScreenToggleChanged);
            closeButton.RegisterCallback<ClickEvent>(OnClose);
        }

        private void OnDisable()
        {
            //main
            startButton.UnregisterCallback<ClickEvent>(OnSartClicked);
            settingsButton.UnregisterCallback<ClickEvent>(OnSettingsClicked);
            quitButton.UnregisterCallback<ClickEvent>(OnQuitClicked);
            
            //settings
            masterVolumeSlider.UnregisterCallback<ChangeEvent<float>>(OnMasterVolumeChanged);
            fullScreenToggle.UnregisterCallback<ChangeEvent<bool>>(OnFullScreenToggleChanged);
            closeButton.UnregisterCallback<ClickEvent>(OnClose);
        }

        //events--------------------------------------------------------------------------------------------------------
        //main
        private void OnSartClicked(ClickEvent click)
        {
            LoadScene();
        }

        private void OnSettingsClicked(ClickEvent click)
        {
            OpenMenu(Settings);
            CloseMenu(Main);
        }
        
        private void OnQuitClicked(ClickEvent click)
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
            OpenMenu(Main);
            CloseMenu(Settings);
        }
        
        //functions-----------------------------------------------------------------------------------------------------
        //main
        protected void LoadScene()
        {
            SceneManager.LoadScene(playScene);
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
