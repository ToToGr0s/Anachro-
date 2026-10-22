using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    public sealed class MainMenuScript : MenuBase
    {
        [SerializeField] private string _playScene;

        private VisualElement _mainGroup;
        private Button _startButton;
        private Button _settingsButton;
        private Button _quitButton;
        private SettingsPanel _settings;

        private void OnEnable()
        {
            _mainGroup = Root.Q<VisualElement>("MainMenuGroup");
            _startButton = Root.Q<Button>("startButton");
            _settingsButton = Root.Q<Button>("settingsButton");
            _quitButton = Root.Q<Button>("quitButton");
            _settings = new SettingsPanel(Root, "SettingsMenuGroup");

            OpenMenu(_mainGroup);
            CloseMenu(_settings.Group);

            _startButton.clicked += OnStartClicked;
            _settingsButton.clicked += OnSettingsClicked;
            _quitButton.clicked += ExitGame;
            _settings.Closed += OnSettingsClosed;
            _settings.Register();
        }

        private void OnDisable()
        {
            _startButton.clicked -= OnStartClicked;
            _settingsButton.clicked -= OnSettingsClicked;
            _quitButton.clicked -= ExitGame;
            _settings.Closed -= OnSettingsClosed;
            _settings.Unregister();
        }

        //events--------------------------------------------------------------------------------------------------------
        private void OnStartClicked()
        {
            // TODO (Hana) : passer par LobbyMenu / SteamLobby (créer ou rejoindre une partie) au lieu de charger la scène en local.
            SceneManager.LoadScene(_playScene);
        }

        private void OnSettingsClicked()
        {
            OpenMenu(_settings.Group);
            CloseMenu(_mainGroup);
        }

        private void OnSettingsClosed()
        {
            OpenMenu(_mainGroup);
            CloseMenu(_settings.Group);
        }
    }
}
