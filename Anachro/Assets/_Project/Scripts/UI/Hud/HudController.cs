using Anachronism.Gameplay;
using Anachronism.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudController : MonoBehaviour
    {
        private const string HunterClass = "hud--hunter";
        private const string FugitiveClass = "hud--fugitive";
        private const string CountdownVisibleClass = "end-countdown--visible";

        private static readonly string[] DisruptionTierClasses = { "disruption--tier-1", "disruption--tier-2", "disruption--tier-3" };

        private static HudController _persistentHud;

        [SerializeField] private float[] _disruptionTierThresholds = { 0.25f, 0.5f, 0.75f };

        private FugitivePlacer _placer;

        private const float ShotResultDuration = 0.5f;
        private static readonly string[] ShotResultClasses = { "", "reticle--found", "reticle--period", "reticle--innocent", "reticle--villain" };

        private HunterTool _hunterTool;
        private VisualElement _reticle;
        private float _shotResultEndTime;

        [Header("Aperçu sans réseau (menu ⋮ du composant, en Play)")]
        [SerializeField] private float[] _previewCooldowns = { 15f, 30f, 45f };

        private UIDocument _document;
        private MatchManager _match;
        private bool _isBound;
        private bool _isPlayingPhase;

        private VisualElement _root;
        private VisualElement _disruption;
        private VisualElement _disruptionFill;
        private Label _disruptionValue;
        private Label _roleLabel;
        private VisualElement _endCountdownBar;
        private Label _endCountdown;
        private ItemBarView _itemBar;

        private PlayerRole _previewRole = PlayerRole.None;
        private readonly float[] _previewNextCooldownTimes = new float[ItemBarView.SlotCount];

        private bool _isShown;
        private bool _hasShownRole;
        private PlayerRole _shownRole;
        private int _shownPercent;
        private float _shownRatio;
        private int _shownTier;
        private int _shownCountdown;

        private bool IsPreviewing => _previewRole != PlayerRole.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _persistentHud = null;

        private void Awake()
        {
            if (_persistentHud != null && _persistentHud != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            _persistentHud = this;
            if (transform.parent != null)
                transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);
            _document = GetComponent<UIDocument>();
        }

        private void OnDestroy()
        {
            if (_persistentHud == this)
                _persistentHud = null;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            VisualElement documentRoot = _document.rootVisualElement;
            _root = documentRoot.Q<VisualElement>("hud-root");
            _disruption = documentRoot.Q<VisualElement>("disruption");
            _disruptionFill = documentRoot.Q<VisualElement>("disruption-fill");
            _disruptionValue = documentRoot.Q<Label>("disruption-value");
            _roleLabel = documentRoot.Q<Label>("role-label");
            _endCountdownBar = documentRoot.Q<VisualElement>("end-countdown-bar");
            _endCountdown = documentRoot.Q<Label>("end-countdown");
            _reticle = documentRoot.Q<VisualElement>("reticle");
            _itemBar = new ItemBarView(documentRoot);

            if (_root == null)
            {
                Debug.LogError("[HudController] Hud.uxml manquant ou sans « hud-root » sur l'UIDocument.", this);
                enabled = false;
                return;
            }

            ResetShownValues();
            _isShown = true;
            SetShown(false);

            FugitivePlacer.Enabled += BindPlacer;
            FugitivePlacer.Disabled += UnbindPlacer;
            // HUD réactivé alors que le joueur est déjà là : l'event Enabled est passé sans nous.
            if (FugitivePlacer.Current != null)
                BindPlacer(FugitivePlacer.Current);

            HunterTool.Enabled += BindHunterTool;
            HunterTool.Disabled += UnbindHunterTool;
            if (HunterTool.Current != null)
                BindHunterTool(HunterTool.Current);
        }

        private void Start()
        {
            TryBind();
        }

        private void OnDisable()
        {
            FugitivePlacer.Enabled -= BindPlacer;
            FugitivePlacer.Disabled -= UnbindPlacer;
            UnbindPlacer(_placer);

            HunterTool.Enabled -= BindHunterTool;
            HunterTool.Disabled -= UnbindHunterTool;
            UnbindHunterTool(_hunterTool);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Unbind();
        }

        private void Update()
        {
            if (_shotResultEndTime > 0f && Time.time >= _shotResultEndTime)
                ShowShotResult(ShotResult.Missed);

            if (IsPreviewing)
            {
                UpdatePreview();
                return;
            }

            if (_isBound && _match == null)
                Unbind();

            if (!_isBound)
                return;

            bool isShown = _isPlayingPhase && _match.LocalRole != PlayerRole.None;
            SetShown(isShown);
            if (!isShown)
                return;

            SetDisruption(_match.Degradation);

            SetEndCountdown(_match.Phase == GamePhase.LastChance, _match.RemainingTime);

            _itemBar.Tick(Time.deltaTime);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryBind();

        private void OnSceneUnloaded(Scene scene)
        {
            if (_isBound && _match == null)
                Unbind();
        }

        private void TryBind()
        {
            if (_isBound && _match == null)
                Unbind();

            if (!_isBound && MatchManager.Instance != null)
                Bind(MatchManager.Instance);
        }

        private void Bind(MatchManager match)
        {
            _match = match;
            _match.PhaseChanged += OnPhaseChanged;
            _match.LocalRoleAssigned += OnLocalRoleAssigned;
            _isBound = true;

            OnPhaseChanged(_match.Phase);
            OnLocalRoleAssigned(_match.LocalRole);
        }

        private void Unbind()
        {
            if (!_isBound)
                return;

            _match.PhaseChanged -= OnPhaseChanged;
            _match.LocalRoleAssigned -= OnLocalRoleAssigned;
            _match = null;
            _isBound = false;
            _isPlayingPhase = false;
            SetShown(false);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            _isPlayingPhase = phase != GamePhase.Lobby && phase != GamePhase.End;

            if (_isPlayingPhase)
                return;

            _itemBar.ClearCooldowns();
            _itemBar.SetSelected(0);
        }

        private void OnLocalRoleAssigned(PlayerRole role) => SetRole(role);

        private void SetShown(bool isShown)
        {
            if (isShown == _isShown)
                return;

            _isShown = isShown;
            _root.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
            if (isShown)
            {
                // Caché, le HUD ne décomptait plus : on repart de l'état du placer.
                SyncWithPlacer();
                return;
            }

            _itemBar.ClearCooldowns();
            _itemBar.SetSelected(0);
            _roleLabel.text = string.Empty;
            _root.EnableInClassList(HunterClass, false);
            _root.EnableInClassList(FugitiveClass, false);
            ResetShownValues();
        }

        private void SetRole(PlayerRole role)
        {
            if (_hasShownRole && role == _shownRole)
                return;

            _hasShownRole = true;
            _shownRole = role;
            _root.EnableInClassList(HunterClass, role == PlayerRole.Guardian);
            _root.EnableInClassList(FugitiveClass, role == PlayerRole.Villain);
            _roleLabel.text = DescribeRole(role);
            SyncWithPlacer();
        }

        private void SetDisruption(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (Mathf.Abs(ratio - _shownRatio) > 0.001f)
            {
                _shownRatio = ratio;
                _disruptionFill.style.width = Length.Percent(ratio * 100f);
                SetDisruptionTier(ratio);
            }

            int percent = Mathf.FloorToInt(ratio * 100f);
            if (percent == _shownPercent)
                return;

            _shownPercent = percent;
            _disruptionValue.text = $"{percent} %";
        }

        private void SetDisruptionTier(float ratio)
        {
            int tier = 0;
            foreach (float threshold in _disruptionTierThresholds)
            {
                if (ratio >= threshold)
                    tier++;
            }

            tier = Mathf.Min(tier, DisruptionTierClasses.Length);
            if (tier == _shownTier)
                return;

            _shownTier = tier;
            for (int i = 0; i < DisruptionTierClasses.Length; i++)
                _disruption.EnableInClassList(DisruptionTierClasses[i], i == tier - 1);
        }

        private void SetEndCountdown(bool isVisible, float remainingSeconds)
        {
            int seconds = isVisible ? Mathf.CeilToInt(remainingSeconds) : -1;
            if (seconds == _shownCountdown)
                return;

            _shownCountdown = seconds;
            _endCountdownBar.EnableInClassList(CountdownVisibleClass, isVisible);
            if (isVisible)
                _endCountdown.text = $"{seconds / 60}:{seconds % 60:00}";
        }

        private void ResetShownValues()
        {
            _hasShownRole = false;
            _shownPercent = -1;
            _shownRatio = -1f;
            _shownTier = -1;
            _shownCountdown = int.MinValue;
        }

        // TODO (Olivier) : passer à PlayerRole.Hunter / Fugitive quand Théo renommera l'enum (Archi T03), ici, dans SetRole et dans l'aperçu.
        private static string DescribeRole(PlayerRole role) => role switch
        {
            PlayerRole.Guardian => "Chasseur",
            PlayerRole.Villain => "Fugitif",
            _ => string.Empty
        };

        [ContextMenu("Aperçu : chasseur")]
        private void PreviewHunter() => StartPreview(PlayerRole.Guardian);

        [ContextMenu("Aperçu : fugitif")]
        private void PreviewFugitive() => StartPreview(PlayerRole.Villain);

        [ContextMenu("Aperçu : arrêter")]
        private void StopPreview()
        {
            _previewRole = PlayerRole.None;
            _itemBar.ClearCooldowns();
            SetShown(false);
            ResetShownValues();
            Unbind();
            TryBind();
        }

        private void StartPreview(PlayerRole role)
        {
            _previewRole = role;
            System.Array.Clear(_previewNextCooldownTimes, 0, _previewNextCooldownTimes.Length);
        }

        private void UpdatePreview()
        {
            SetShown(true);
            SetRole(_previewRole);

            float disruption = Mathf.PingPong(Time.time * 0.05f, 1f);
            SetDisruption(disruption);
            SetEndCountdown(disruption >= 0.8f, (1f - disruption) * 60f);

            if (_previewRole != PlayerRole.Villain)
                return;

            // Un vrai placer pilote deja la barre : l'apercu ne la touche pas, il se contente de la faire decompter.
            if (_placer == null)
            {
                _itemBar.SetSelected((int)(Time.time / 3f) % ItemBarView.SlotCount);
                for (int i = 0; i < ItemBarView.SlotCount && i < _previewCooldowns.Length; i++)
                {
                    if (Time.time < _previewNextCooldownTimes[i])
                        continue;

                    _itemBar.SetCooldown(i, _previewCooldowns[i], _previewCooldowns[i]);
                    _previewNextCooldownTimes[i] = Time.time + _previewCooldowns[i] + 2f;
                }
            }

            _itemBar.Tick(Time.deltaTime);
        }

        private void BindPlacer(FugitivePlacer placer)
        {
            UnbindPlacer(_placer);
            _placer = placer;
            _placer.SizeSelected += OnSizeSelected;
            _placer.CooldownChanged += OnCooldownChanged;
            SyncWithPlacer();
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

        // Reponse du serveur seulement : jamais pendant la visee, sinon le reticule trahirait le mechant.
        private void OnShotResolved(ShotResult result)
        {
            ShowShotResult(result);
            _shotResultEndTime = result == ShotResult.Missed ? 0f : Time.time + ShotResultDuration;
        }

        private void ShowShotResult(ShotResult result)
        {
            if (_reticle == null)
                return;

            for (int i = 1; i < ShotResultClasses.Length; i++)
                _reticle.EnableInClassList(ShotResultClasses[i], i == (int)result);
            if (result == ShotResult.Missed)
                _shotResultEndTime = 0f;
        }

        // Le placer garde sa taille et sa recharge quand le HUD est caché ou réinitialisé : la barre se recale dessus.
        private void SyncWithPlacer()
        {
            _itemBar.SetSelected(_placer != null ? (int)_placer.SelectedSize : 0);
            if (_placer == null || _placer.CooldownRemaining <= 0f)
                return;

            for (int i = 0; i < ItemBarView.SlotCount; i++)
                _itemBar.SetCooldown(i, _placer.CooldownRemaining, _placer.CooldownDuration);
        }

        private void UnbindPlacer(FugitivePlacer placer)
        {
            if (placer == null || placer != _placer)
                return;

            _placer.SizeSelected -= OnSizeSelected;
            _placer.CooldownChanged -= OnCooldownChanged;
            _placer = null;
        }

        private void OnSizeSelected(AnachronismSize size) => _itemBar.SetSelected((int)size);

        // La recharge est globale : poser un objet bloque les trois emplacements, quelle que soit sa taille.
        // ItemBarView décompte ensuite tout seul (Tick).
        private void OnCooldownChanged(AnachronismSize size, float duration)
        {
            for (int i = 0; i < ItemBarView.SlotCount; i++)
                _itemBar.SetCooldown(i, duration, duration);
        }
    }
}
