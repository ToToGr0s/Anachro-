using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    public sealed class InGameUI : MenuBase
    {
        private const string DeregulationStaticText = "Dérèglement : ";
        private const string FullStaticText = "prêt";

        [SerializeField] private string _mainMenuScene;

        // Le rôle n'est connu qu'au début de partie : SetIsFugitive le met à jour à ce moment-là.
        private bool _isFugitive;
        private bool _isPopUpOpen;

        // Dernières valeurs affichées : les Update* peuvent être appelées à chaque frame,
        // on ne reconstruit le texte (allocation) que quand il change.
        private int _timerSeconds = -1;
        private int _deregulationPercent = -1;
        private int _bigToolPercent = -1;
        private int _mediumToolPercent = -1;
        private int _smallToolPercent = -1;

        //in game
        private VisualElement _inGameGroup;
        private ProgressBar _deregulationProgressBar;
        private Label _timer;

        //deregulation tools
        private VisualElement _deregulationToolsGroup;
        private Button _bigTool;
        private Button _mediumTool;
        private Button _smallTool;
        private ProgressBar _bigToolProgressBar;
        private ProgressBar _mediumToolProgressBar;
        private ProgressBar _smallToolProgressBar;

        //pause menu
        private VisualElement _pauseGroup;
        private Button _resumeButton;
        private Button _settingsButton;
        private Button _quitButton;

        //pop-up
        private VisualElement _popUpGroup;
        private Button _mainMenuButton;
        private Button _quitQuitButton;
        private Button _cancelButton;

        private SettingsPanel _settings;

        private void OnEnable()
        {
            //in game
            _inGameGroup = Root.Q<VisualElement>("inGameGroup");
            _deregulationProgressBar = Root.Q<ProgressBar>("deregulationBar");
            _timer = Root.Q<Label>("timer");

            //deregulation tools : toujours récupérés, seul l'affichage dépend du rôle
            _deregulationToolsGroup = Root.Q<VisualElement>("anachronismToolsGroup");
            _bigTool = Root.Q<Button>("bigDisruptionButton");
            _mediumTool = Root.Q<Button>("mediumDisruptionButton");
            _smallTool = Root.Q<Button>("smallDisruptionButton");
            _bigToolProgressBar = Root.Q<ProgressBar>("bigDisruptionBar");
            _mediumToolProgressBar = Root.Q<ProgressBar>("mediumDisruptionBar");
            _smallToolProgressBar = Root.Q<ProgressBar>("smallDisruptionBar");

            //pause
            _pauseGroup = Root.Q<VisualElement>("pauseMenuGroup");
            _resumeButton = Root.Q<Button>("resumeButton");
            _settingsButton = Root.Q<Button>("settingsButton");
            _quitButton = Root.Q<Button>("quitButton");

            //pop up
            _popUpGroup = Root.Q<VisualElement>("popUpGroup");
            _mainMenuButton = Root.Q<Button>("mainMenuButton");
            _quitQuitButton = Root.Q<Button>("quitQuitButton");
            _cancelButton = Root.Q<Button>("cancelButton");

            _settings = new SettingsPanel(Root, "settingsGroup");

            // Arbre neuf : les textes sont à reconstruire au prochain Update*.
            _timerSeconds = _deregulationPercent = _bigToolPercent = _mediumToolPercent = _smallToolPercent = -1;
            _isPopUpOpen = false;

            OpenMenu(_inGameGroup);
            SetIsFugitive(_isFugitive);
            CloseMenu(_pauseGroup);
            CloseMenu(_popUpGroup);
            CloseMenu(_settings.Group);

            _bigTool.clicked += OnBigToolClicked;
            _mediumTool.clicked += OnMediumToolClicked;
            _smallTool.clicked += OnSmallToolClicked;
            _resumeButton.clicked += OnResume;
            _settingsButton.clicked += OnSettings;
            _quitButton.clicked += OnQuit;
            _mainMenuButton.clicked += OnMainMenu;
            _quitQuitButton.clicked += ExitGame;
            _cancelButton.clicked += OnCancel;
            _settings.Closed += OnSettingsClosed;
            _settings.Register();
        }

        private void OnDisable()
        {
            _bigTool.clicked -= OnBigToolClicked;
            _mediumTool.clicked -= OnMediumToolClicked;
            _smallTool.clicked -= OnSmallToolClicked;
            _resumeButton.clicked -= OnResume;
            _settingsButton.clicked -= OnSettings;
            _quitButton.clicked -= OnQuit;
            _mainMenuButton.clicked -= OnMainMenu;
            _quitQuitButton.clicked -= ExitGame;
            _cancelButton.clicked -= OnCancel;
            _settings.Closed -= OnSettingsClosed;
            _settings.Unregister();
        }

        //events--------------------------------------------------------------------------------------------------------
        //deregulation tools
        private void OnBigToolClicked()
        {
            // TODO (Hana) : lever un event C# écouté par le gameplay (l'UI ne touche pas au réseau).
        }

        private void OnMediumToolClicked()
        {
            // TODO (Hana) : idem OnBigToolClicked.
        }

        private void OnSmallToolClicked()
        {
            // TODO (Hana) : idem OnBigToolClicked.
        }

        //pause
        // TODO (Hana) : rien n'ouvre encore le menu pause (Échap) ; à voir avec PauseMenu qui gère déjà Échap.
        private void OnResume()
        {
            if (!_isPopUpOpen)
            {
                OpenMenu(_inGameGroup);
                CloseMenu(_pauseGroup);
            }
        }

        private void OnSettings()
        {
            if (!_isPopUpOpen)
            {
                OpenMenu(_settings.Group);
                CloseMenu(_pauseGroup);
            }
        }

        private void OnQuit()
        {
            if (!_isPopUpOpen)
            {
                OpenMenu(_popUpGroup);
                _isPopUpOpen = true;
            }
        }

        //pop up
        private void OnMainMenu()
        {
            // TODO (Hana) : passer par SteamLobby pour quitter la partie (arrêt de FishNet) au lieu de charger la scène.
            // En l'état, l'hôte qui clique ici casse la partie de tout le monde.
            SceneManager.LoadScene(_mainMenuScene);
        }

        private void OnCancel()
        {
            CloseMenu(_popUpGroup);
            _isPopUpOpen = false;
        }

        //settings
        private void OnSettingsClosed()
        {
            OpenMenu(_pauseGroup);
            CloseMenu(_settings.Group);
        }

        //functions-----------------------------------------------------------------------------------------------------
        //some functions here are to be used from other programs
        public void SetIsFugitive(bool isFugitive)
        {
            _isFugitive = isFugitive;
            if (_isFugitive)
            {
                OpenMenu(_deregulationToolsGroup);
            }
            else
            {
                CloseMenu(_deregulationToolsGroup);
            }
        }

        //in game
        public void UpdateDeregulationBar(float factor)
        {
            SetProgress(_deregulationProgressBar, factor);
            int percent = ToPercent(factor);
            if (percent != _deregulationPercent)
            {
                _deregulationPercent = percent;
                _deregulationProgressBar.title = DeregulationStaticText + percent + "%";
            }
        }

        public void UpdateTimer(int minutes, int seconds)
        {
            int totalSeconds = minutes * 60 + seconds;
            if (totalSeconds != _timerSeconds)
            {
                _timerSeconds = totalSeconds;
                _timer.text = $"{minutes:00}:{seconds:00}";
            }
        }

        //deregulation tools
        public void UpdateBigToolBar(float factor)
        {
            UpdateToolBar(_bigToolProgressBar, factor, ref _bigToolPercent);
        }

        public void UpdateMediumToolBar(float factor)
        {
            UpdateToolBar(_mediumToolProgressBar, factor, ref _mediumToolPercent);
        }

        public void UpdateSmallToolBar(float factor)
        {
            UpdateToolBar(_smallToolProgressBar, factor, ref _smallToolPercent);
        }

        private static void UpdateToolBar(ProgressBar bar, float factor, ref int lastPercent)
        {
            SetProgress(bar, factor);
            int percent = ToPercent(factor);
            if (percent != lastPercent)
            {
                lastPercent = percent;
                bar.title = percent < 100 ? percent + "%" : FullStaticText;
            }
        }

        private static int ToPercent(float factor)
        {
            return (int)(Mathf.Clamp01(factor) * 100);
        }
    }
}
