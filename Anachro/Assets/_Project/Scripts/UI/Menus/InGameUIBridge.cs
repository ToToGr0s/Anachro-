using Anachronism.Core;
using Anachronism.Gameplay;
using Anachronism.Network;
using FishNet;
using FishNet.Managing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    /// <summary>
    /// Branche l'écran de jeu d'Hanaé (InGameUI + PlayMenu.uxml) sur la partie en ligne : dérèglement, chrono, outils du
    /// fugitif, rôle, phase, réticule, menu Échap et réglages de l'hôte. Écoute MatchManager et les outils du joueur local,
    /// ne modifie jamais l'état réseau ; les réglages de l'hôte passent par HostTools, qui revérifie que ce poste est l'hôte.
    /// </summary>
    // Reprend la logique de l'ancien HudController et de l'ancien PauseMenu (UI provisoire retirée).
    [RequireComponent(typeof(UIDocument), typeof(InGameUI))]
    public sealed class InGameUIBridge : MonoBehaviour
    {
        private const string HunterClass = "play-role--hunter";
        private const string FugitiveClass = "play-role--fugitive";
        private const string SelectedToolClass = "tool--selected";
        private const float ShotResultDuration = 0.5f;

        private static readonly string[] ShotResultClasses = { "reticle--found", "reticle--period", "reticle--innocent", "reticle--villain" };

        private UIDocument _document;
        private InGameUI _ui;
        private NetworkManager _networkManager;
        private MatchManager _match;
        private FugitivePlacer _placer;
        private HunterTool _hunterTool;

        private VisualElement _inGameGroup;
        private Label _timer;
        private Label _roleLabel;
        private Label _phaseLabel;
        private Label _endCountdown;
        private VisualElement _reticle;
        // Indexés comme AnachronismSize : petit, moyen, grand.
        private readonly Button[] _toolButtons = new Button[3];
        private readonly ProgressBar[] _toolBars = new ProgressBar[3];
        private VisualElement _hostSection;
        private Button _reloadNpcsButton;
        private Button _rerollSkinsButton;
        private Label _hostStatus;

        // Dernières valeurs affichées : InGameUI reconstruit ses textes à chaque appel, on ne l'appelle que sur changement.
        private bool _isShown;
        private int _shownPercent = -1;
        private int _shownSeconds = int.MinValue;
        private GamePhase _shownPhase;
        private int _shownToolPercent = -1;
        private float _shotResultEndTime;

        private void Awake()
        {
            // Vit dans Boot comme le menu du lobby : doit survivre au chargement de la map.
            DontDestroyOnLoad(gameObject);
            _document = GetComponent<UIDocument>();
            _ui = GetComponent<InGameUI>();
        }

        private void OnEnable()
        {
            // UIDocument reconstruit son arbre à chaque activation : on relit les éléments ici.
            VisualElement root = _document.rootVisualElement;
            _inGameGroup = root.Q<VisualElement>("inGameGroup");
            _timer = root.Q<Label>("timer");
            _roleLabel = root.Q<Label>("roleLabel");
            _phaseLabel = root.Q<Label>("phaseLabel");
            _endCountdown = root.Q<Label>("endCountdown");
            _reticle = root.Q<VisualElement>("reticle");
            _toolButtons[(int)AnachronismSize.Small] = root.Q<Button>("smallDisruptionButton");
            _toolButtons[(int)AnachronismSize.Medium] = root.Q<Button>("mediumDisruptionButton");
            _toolButtons[(int)AnachronismSize.Large] = root.Q<Button>("bigDisruptionButton");
            _toolBars[(int)AnachronismSize.Small] = root.Q<ProgressBar>("smallDisruptionBar");
            _toolBars[(int)AnachronismSize.Medium] = root.Q<ProgressBar>("mediumDisruptionBar");
            _toolBars[(int)AnachronismSize.Large] = root.Q<ProgressBar>("bigDisruptionBar");
            _hostSection = root.Q<VisualElement>("hostSection");
            _reloadNpcsButton = root.Q<Button>("reloadNpcsButton");
            _rerollSkinsButton = root.Q<Button>("rerollSkinsButton");
            _hostStatus = root.Q<Label>("hostStatus");

            if (_inGameGroup == null || _timer == null || _roleLabel == null || _phaseLabel == null || _endCountdown == null
                || _reticle == null || _hostSection == null || _reloadNpcsButton == null || _rerollSkinsButton == null
                || _hostStatus == null)
            {
                Debug.LogError("[InGameUIBridge] Éléments manquants dans l'UXML (PlayMenu.uxml attendu).", this);
                enabled = false;
                return;
            }

            _reloadNpcsButton.clicked += OnReloadNpcsClicked;
            _rerollSkinsButton.clicked += OnRerollSkinsClicked;

            FugitivePlacer.Enabled += BindPlacer;
            FugitivePlacer.Disabled += UnbindPlacer;
            // Pont réactivé alors que le joueur est déjà là : l'event Enabled est passé sans nous.
            if (FugitivePlacer.Current != null)
                BindPlacer(FugitivePlacer.Current);

            HunterTool.Enabled += BindHunterTool;
            HunterTool.Disabled += UnbindHunterTool;
            if (HunterTool.Current != null)
                BindHunterTool(HunterTool.Current);

            GameMenuState.SetAvailable(true);
            ResetShownValues();
            _isShown = true;
            SetShown(false);
        }

        private void OnDisable()
        {
            if (_reloadNpcsButton != null)
                _reloadNpcsButton.clicked -= OnReloadNpcsClicked;
            if (_rerollSkinsButton != null)
                _rerollSkinsButton.clicked -= OnRerollSkinsClicked;

            FugitivePlacer.Enabled -= BindPlacer;
            FugitivePlacer.Disabled -= UnbindPlacer;
            UnbindPlacer(_placer);

            HunterTool.Enabled -= BindHunterTool;
            HunterTool.Disabled -= UnbindHunterTool;
            UnbindHunterTool(_hunterTool);

            Unbind();
            GameMenuState.SetAvailable(false);
        }

        private void Start()
        {
            _networkManager = InstanceFinder.NetworkManager;
            if (_networkManager == null)
            {
                Debug.LogError("[InGameUIBridge] Pas de NetworkManager : lancer le jeu depuis Boot.");
                enabled = false;
            }
        }

        private void Update()
        {
            UpdatePause();

            // MatchManager vit dans la map : il apparaît et disparaît avec elle.
            if (_match == null && MatchManager.Instance != null)
                Bind(MatchManager.Instance);
            else if (_match != null && _match != MatchManager.Instance)
                Unbind();

            if (_shotResultEndTime > 0f && Time.time >= _shotResultEndTime)
                ShowShotResult(ShotResult.Missed);

            bool isShown = _match != null && IsPlayingPhase(_match.Phase) && _match.LocalRole != PlayerRole.None;
            SetShown(isShown);
            if (!isShown)
                return;

            RefreshDisruption();
            RefreshTime();
            RefreshToolCooldowns();
        }

        // Échap ouvre et ferme le menu d'Hanaé ; en ligne seulement (au lobby compris), comme l'ancien menu pause.
        private void UpdatePause()
        {
            bool isOnline = _networkManager.IsClientStarted;
            if (_ui.IsPaused && !isOnline)
            {
                // Déconnecté avec le menu ouvert : on le ferme, l'écran titre reprend la main.
                _ui.ClosePause();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (_ui.IsPaused)
            {
                _ui.ClosePause();
            }
            else if (isOnline)
            {
                SetVisible(_hostSection, HostTools.IsHost);
                _hostStatus.text = string.Empty;
                _ui.OpenPause();
            }
        }

        private void Bind(MatchManager match)
        {
            _match = match;
            _match.PhaseChanged += OnPhaseChanged;
            _match.LocalRoleAssigned += OnLocalRoleAssigned;
            OnLocalRoleAssigned(_match.LocalRole);
        }

        private void Unbind()
        {
            if (_match != null)
            {
                _match.PhaseChanged -= OnPhaseChanged;
                _match.LocalRoleAssigned -= OnLocalRoleAssigned;
            }

            _match = null;
            SetShown(false);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            // Nouvelle manche : l'outil choisi repart du petit.
            if (!IsPlayingPhase(phase))
                SetSelectedTool(AnachronismSize.Small);
        }

        private void OnLocalRoleAssigned(PlayerRole role)
        {
            _ui.SetFugitive(role == PlayerRole.Villain);
            _roleLabel.text = DescribeRole(role);
            _roleLabel.EnableInClassList(HunterClass, role == PlayerRole.Guardian);
            _roleLabel.EnableInClassList(FugitiveClass, role == PlayerRole.Villain);
        }

        // visibility et non display : InGameUI se sert de display pour passer du jeu au menu pause.
        private void SetShown(bool isShown)
        {
            if (isShown == _isShown || _inGameGroup == null)
                return;

            _isShown = isShown;
            _inGameGroup.style.visibility = isShown ? Visibility.Visible : Visibility.Hidden;
            if (isShown)
            {
                // Caché, l'écran ne suivait plus le placer : on repart de son état.
                SyncWithPlacer();
                return;
            }

            ResetShownValues();
        }

        private void RefreshDisruption()
        {
            float ratio = Mathf.Clamp01(_match.Degradation);
            int percent = Mathf.FloorToInt(ratio * 100f);
            if (percent == _shownPercent)
                return;

            _shownPercent = percent;
            _ui.UpdateDeregulationBar(ratio);
        }

        // Chrono d'Hanaé pour le repérage ; vide pendant la recherche (durée secrète, RemainingTime vaut 0) ;
        // à la dernière chance, le compte à rebours ajouté prend le relais.
        private void RefreshTime()
        {
            GamePhase phase = _match.Phase;
            int seconds = Mathf.CeilToInt(_match.RemainingTime);
            if (seconds == _shownSeconds && phase == _shownPhase)
                return;

            bool isPhaseChanged = phase != _shownPhase;
            _shownSeconds = seconds;
            _shownPhase = phase;
            if (isPhaseChanged)
                _phaseLabel.text = DescribePhase(phase);

            bool isLastChance = phase == GamePhase.LastChance;
            _endCountdown.text = isLastChance && seconds > 0 ? $"{seconds / 60}:{seconds % 60:00}" : string.Empty;
            if (!isLastChance && seconds > 0)
                _ui.UpdateTimer(seconds / 60, seconds % 60);
            else
                _timer.text = string.Empty;
        }

        // La recharge est globale : poser un objet bloque les trois tailles, quelle qu'elle soit.
        private void RefreshToolCooldowns()
        {
            float ratio = 1f;
            if (_placer != null && _placer.CooldownDuration > 0f)
                ratio = 1f - Mathf.Clamp01(_placer.CooldownRemaining / _placer.CooldownDuration);

            int percent = Mathf.FloorToInt(ratio * 100f);
            if (percent == _shownToolPercent)
                return;

            _shownToolPercent = percent;
            foreach (ProgressBar bar in _toolBars)
                _ui.UpdateDeregulationBar(bar, ratio);
        }

        private void ResetShownValues()
        {
            _shownPercent = -1;
            _shownSeconds = int.MinValue;
            _shownPhase = (GamePhase)byte.MaxValue;
            _shownToolPercent = -1;
        }

        private void BindPlacer(FugitivePlacer placer)
        {
            UnbindPlacer(_placer);
            _placer = placer;
            _placer.SizeSelected += SetSelectedTool;
            SyncWithPlacer();
        }

        private void UnbindPlacer(FugitivePlacer placer)
        {
            if (placer == null || placer != _placer)
                return;

            _placer.SizeSelected -= SetSelectedTool;
            _placer = null;
        }

        private void SyncWithPlacer()
        {
            SetSelectedTool(_placer != null ? _placer.SelectedSize : AnachronismSize.Small);
            _shownToolPercent = -1;
        }

        private void SetSelectedTool(AnachronismSize size)
        {
            for (int i = 0; i < _toolButtons.Length; i++)
                _toolButtons[i]?.EnableInClassList(SelectedToolClass, i == (int)size);
        }

        private void BindHunterTool(HunterTool tool)
        {
            UnbindHunterTool(_hunterTool);
            _hunterTool = tool;
            _hunterTool.ShotResolved += OnShotResolved;
        }

        private void UnbindHunterTool(HunterTool tool)
        {
            if (tool == null || tool != _hunterTool)
                return;

            _hunterTool.ShotResolved -= OnShotResolved;
            _hunterTool = null;
        }

        // Réponse du serveur seulement : jamais pendant la visée, sinon le réticule trahirait le méchant.
        // Time.time ne sert qu'à la durée d'affichage locale (aucune règle de jeu), comme les FX.
        private void OnShotResolved(ShotResult result)
        {
            ShowShotResult(result);
            _shotResultEndTime = result == ShotResult.Missed ? 0f : Time.time + ShotResultDuration;
        }

        private void ShowShotResult(ShotResult result)
        {
            string shown = ShotResultClass(result);
            foreach (string resultClass in ShotResultClasses)
                _reticle.EnableInClassList(resultClass, resultClass == shown);
            if (result == ShotResult.Missed)
                _shotResultEndTime = 0f;
        }

        private void OnReloadNpcsClicked() =>
            _hostStatus.text = HostTools.TryRespawnNpcs() ? "PNJ rechargés." : "Pas de PNJ dans cette map.";

        private void OnRerollSkinsClicked() =>
            _hostStatus.text = HostTools.TryRerollNpcSkins() ? "Nouveaux skins tirés." : "Pas de PNJ dans cette map.";

        private static bool IsPlayingPhase(GamePhase phase) => phase != GamePhase.Lobby && phase != GamePhase.End;

        private static void SetVisible(VisualElement element, bool isVisible) =>
            element.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

        // Un coéquipier touché s'affiche comme un innocent : une erreur, quelle qu'elle soit.
        private static string ShotResultClass(ShotResult result) => result switch
        {
            ShotResult.RightObjectFound => "reticle--found",
            ShotResult.WrongObjectFound => "reticle--period",
            ShotResult.Innocent or ShotResult.Teammate => "reticle--innocent",
            ShotResult.Villain => "reticle--villain",
            _ => null
        };

        // TODO (Olivier) : passer à PlayerRole.Hunter / Fugitive quand Théo renommera l'enum (Archi T03).
        private static string DescribeRole(PlayerRole role) => role switch
        {
            PlayerRole.Guardian => "Chasseur",
            PlayerRole.Villain => "Fugitif",
            _ => string.Empty
        };

        private static string DescribePhase(GamePhase phase) => phase switch
        {
            GamePhase.Scouting => "Les fugitifs repèrent la map",
            GamePhase.Searching => "Recherche",
            GamePhase.LastChance => "Les fugitifs sont repérés, dernière chance",
            _ => string.Empty
        };
    }
}
