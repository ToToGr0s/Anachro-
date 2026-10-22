using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    public sealed class MainMenuScript : MenuBase
    {
        [SerializeField] private string _playScene;

        //main
        private VisualElement _mainGroup;
        private Button _startButton;
        private Button _settingsButton;
        private Button _quitButton;

        //settings
        private VisualElement _settingsGroup;
        private Slider _masterVolumeSlider;
        private Toggle _fullScreenToggle;
        private Button _closeButton;

        private void OnEnable()
        {
            //main
            _mainGroup = Root.Q<VisualElement>("MainMenuGroup");
            _startButton = Root.Q<Button>("startButton");
            _settingsButton = Root.Q<Button>("settingsButton");
            _quitButton = Root.Q<Button>("quitButton");

            //settings
            _settingsGroup = Root.Q<VisualElement>("SettingsMenuGroup");
            _masterVolumeSlider = Root.Q<Slider>("masterVolumeSlider");
            _fullScreenToggle = Root.Q<Toggle>("fullScreenToggle");
            _closeButton = Root.Q<Button>("closeButton");

            OpenMenu(_mainGroup);
            CloseMenu(_settingsGroup);

            //main
            _startButton.RegisterCallback<ClickEvent>(OnStartClicked);
            _settingsButton.RegisterCallback<ClickEvent>(OnSettingsClicked);
            _quitButton.RegisterCallback<ClickEvent>(OnQuitClicked);

            //settings
            _masterVolumeSlider.RegisterCallback<ChangeEvent<float>>(OnMasterVolumeChanged);
            _fullScreenToggle.RegisterCallback<ChangeEvent<bool>>(OnFullScreenToggleChanged);
            _closeButton.RegisterCallback<ClickEvent>(OnClose);
        }

        private void OnDisable()
        {
            //main
            _startButton.UnregisterCallback<ClickEvent>(OnStartClicked);
            _settingsButton.UnregisterCallback<ClickEvent>(OnSettingsClicked);
            _quitButton.UnregisterCallback<ClickEvent>(OnQuitClicked);

            //settings
            _masterVolumeSlider.UnregisterCallback<ChangeEvent<float>>(OnMasterVolumeChanged);
            _fullScreenToggle.UnregisterCallback<ChangeEvent<bool>>(OnFullScreenToggleChanged);
            _closeButton.UnregisterCallback<ClickEvent>(OnClose);
        }

        //events--------------------------------------------------------------------------------------------------------
        //main
        private void OnStartClicked(ClickEvent clicked)
        {
            // TODO (Hana) : passer par LobbyMenu / SteamLobby (créer ou rejoindre une partie) au lieu de charger la scène en local.
            SceneManager.LoadScene(_playScene);
        }

        private void OnSettingsClicked(ClickEvent clicked)
        {
            OpenMenu(_settingsGroup);
            CloseMenu(_mainGroup);
        }

        private void OnQuitClicked(ClickEvent clicked)
        {
            ExitGame();
        }

        //settings
        private void OnMasterVolumeChanged(ChangeEvent<float> changed)
        {
            ChangeVolume(changed.newValue);
        }

        private void OnFullScreenToggleChanged(ChangeEvent<bool> changed)
        {
            SetFullScreen(changed.newValue);
        }

        private void OnClose(ClickEvent clicked)
        {
            OpenMenu(_mainGroup);
            CloseMenu(_settingsGroup);
        }
    }
}
