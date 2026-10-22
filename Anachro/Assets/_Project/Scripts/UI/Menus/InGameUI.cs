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

        //settings
        private VisualElement _settingsGroup;
        private Slider _masterVolumeSlider;
        private Toggle _fullScreenToggle;
        private Button _closeButton;

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

            //settings
            _settingsGroup = Root.Q<VisualElement>("settingsGroup");
            _masterVolumeSlider = Root.Q<Slider>("masterVolumeSlider");
            _fullScreenToggle = Root.Q<Toggle>("fullScreenToggle");
            _closeButton = Root.Q<Button>("closeButton");

            OpenMenu(_inGameGroup);
            SetIsFugitive(_isFugitive);
            CloseMenu(_pauseGroup);
            CloseMenu(_popUpGroup);
            CloseMenu(_settingsGroup);
            _isPopUpOpen = false;

            //deregulation tools
            _bigTool.RegisterCallback<ClickEvent>(OnBigToolClicked);
            _mediumTool.RegisterCallback<ClickEvent>(OnMediumToolClicked);
            _smallTool.RegisterCallback<ClickEvent>(OnSmallToolClicked);

            //pause
            _resumeButton.RegisterCallback<ClickEvent>(OnResume);
            _settingsButton.RegisterCallback<ClickEvent>(OnSettings);
            _quitButton.RegisterCallback<ClickEvent>(OnQuit);

            //pop up
            _mainMenuButton.RegisterCallback<ClickEvent>(OnMainMenu);
            _quitQuitButton.RegisterCallback<ClickEvent>(OnQuitQuit);
            _cancelButton.RegisterCallback<ClickEvent>(OnCancel);

            //settings
            _masterVolumeSlider.RegisterCallback<ChangeEvent<float>>(OnMasterVolumeChanged);
            _fullScreenToggle.RegisterCallback<ChangeEvent<bool>>(OnFullScreenToggleChanged);
            _closeButton.RegisterCallback<ClickEvent>(OnClose);
        }

        private void OnDisable()
        {
            //deregulation tools
            _bigTool.UnregisterCallback<ClickEvent>(OnBigToolClicked);
            _mediumTool.UnregisterCallback<ClickEvent>(OnMediumToolClicked);
            _smallTool.UnregisterCallback<ClickEvent>(OnSmallToolClicked);

            //pause
            _resumeButton.UnregisterCallback<ClickEvent>(OnResume);
            _settingsButton.UnregisterCallback<ClickEvent>(OnSettings);
            _quitButton.UnregisterCallback<ClickEvent>(OnQuit);

            //pop up
            _mainMenuButton.UnregisterCallback<ClickEvent>(OnMainMenu);
            _quitQuitButton.UnregisterCallback<ClickEvent>(OnQuitQuit);
            _cancelButton.UnregisterCallback<ClickEvent>(OnCancel);

            //settings
            _masterVolumeSlider.UnregisterCallback<ChangeEvent<float>>(OnMasterVolumeChanged);
            _fullScreenToggle.UnregisterCallback<ChangeEvent<bool>>(OnFullScreenToggleChanged);
            _closeButton.UnregisterCallback<ClickEvent>(OnClose);
        }

        //events--------------------------------------------------------------------------------------------------------
        //deregulation tools
        private void OnBigToolClicked(ClickEvent clicked)
        {
            // TODO (Hana) : lever un event C# écouté par le gameplay (l'UI ne touche pas au réseau).
        }

        private void OnMediumToolClicked(ClickEvent clicked)
        {
            // TODO (Hana) : idem OnBigToolClicked.
        }

        private void OnSmallToolClicked(ClickEvent clicked)
        {
            // TODO (Hana) : idem OnBigToolClicked.
        }

        //pause
        // TODO (Hana) : rien n'ouvre encore le menu pause (Échap) ; à voir avec PauseMenu qui gère déjà Échap.
        private void OnResume(ClickEvent clicked)
        {
            if (!_isPopUpOpen)
            {
                OpenMenu(_inGameGroup);
                CloseMenu(_pauseGroup);
            }
        }

        private void OnSettings(ClickEvent clicked)
        {
            if (!_isPopUpOpen)
            {
                OpenMenu(_settingsGroup);
                CloseMenu(_pauseGroup);
            }
        }

        private void OnQuit(ClickEvent clicked)
        {
            if (!_isPopUpOpen)
            {
                OpenMenu(_popUpGroup);
                _isPopUpOpen = true;
            }
        }

        //pop up
        private void OnMainMenu(ClickEvent clicked)
        {
            // TODO (Hana) : passer par SteamLobby pour quitter la partie (arrêt de FishNet) au lieu de charger la scène.
            // En l'état, l'hôte qui clique ici casse la partie de tout le monde.
            SceneManager.LoadScene(_mainMenuScene);
        }

        private void OnQuitQuit(ClickEvent clicked)
        {
            ExitGame();
        }

        private void OnCancel(ClickEvent clicked)
        {
            CloseMenu(_popUpGroup);
            _isPopUpOpen = false;
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
            OpenMenu(_pauseGroup);
            CloseMenu(_settingsGroup);
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
            _deregulationProgressBar.value = factor;
            _deregulationProgressBar.title = DeregulationStaticText + (int)(factor * 100) + "%";
        }

        public void UpdateTimer(int minutes, int seconds)
        {
            _timer.text = minutes.ToString("00") + ":" + seconds.ToString("00");
        }

        //deregulation tools
        public void UpdateBigToolBar(float factor)
        {
            UpdateToolBar(_bigToolProgressBar, factor);
        }

        public void UpdateMediumToolBar(float factor)
        {
            UpdateToolBar(_mediumToolProgressBar, factor);
        }

        public void UpdateSmallToolBar(float factor)
        {
            UpdateToolBar(_smallToolProgressBar, factor);
        }

        private static void UpdateToolBar(ProgressBar bar, float factor)
        {
            bar.value = factor;
            bar.title = factor < 1 ? (int)(factor * 100) + "%" : FullStaticText;
        }
    }
}
