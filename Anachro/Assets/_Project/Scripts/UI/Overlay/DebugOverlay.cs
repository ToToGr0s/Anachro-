using System.Text;
using Anachronism.Network;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Timing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    /// <summary>
    /// Infos de debug affichées avec F1, dans tous les builds : ping, FPS, tick, joueurs, PNJ, phase.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugOverlay : MonoBehaviour
    {
        // Texte reconstruit 4 fois par seconde, pas à chaque frame : lisible et sans allocation continue.
        private const float RefreshInterval = 0.25f;

        private readonly StringBuilder _text = new StringBuilder(256);

        private UIDocument _document;
        private Label _label;
        private NetworkManager _networkManager;
        private SteamLobby _steamLobby;
        private bool _isVisible;
        private float _nextRefreshTime;
        private int _frameCount;
        private float _frameTime;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            _label = _document.rootVisualElement.Q<Label>("debug-label");
            SetVisible(_isVisible);
        }

        private void Start()
        {
            _networkManager = InstanceFinder.NetworkManager;
            if (_networkManager != null)
                _steamLobby = _networkManager.GetComponent<SteamLobby>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
                SetVisible(!_isVisible);

            _frameCount++;
            _frameTime += Time.unscaledDeltaTime;
            if (Time.unscaledTime < _nextRefreshTime)
                return;

            float fps = _frameTime > 0f ? _frameCount / _frameTime : 0f;
            _frameCount = 0;
            _frameTime = 0f;
            _nextRefreshTime = Time.unscaledTime + RefreshInterval;

            if (_isVisible)
                Refresh(fps);
        }

        private void Refresh(float fps)
        {
            _text.Clear();
            _text.Append("FPS : ").Append(Mathf.RoundToInt(fps)).Append('\n');

            if (_networkManager == null || !_networkManager.IsClientStarted)
            {
                _text.Append("Hors ligne");
                _label.text = _text.ToString();
                return;
            }

            bool isHost = _networkManager.IsServerStarted;
            TimeManager timeManager = _networkManager.TimeManager;
            _text.Append(isHost ? "Hôte" : "Client").Append('\n');
            _text.Append("Ping : ");
            if (isHost)
                _text.Append("0 ms (hôte)");
            else
                _text.Append(timeManager.RoundTripTime).Append(" ms");
            _text.Append('\n');
            _text.Append("Tick : ").Append(timeManager.TickRate).Append(" Hz  #").Append(timeManager.Tick).Append('\n');

            if (_steamLobby != null)
                _text.Append("Joueurs : ").Append(_steamLobby.MemberCount).Append(" / ").Append(_steamLobby.MaxPlayers).Append('\n');

            _text.Append("PNJ : ").Append(NetworkNpc.ClientCount).Append('\n');

            MatchManager match = MatchManager.Instance;
            if (match != null)
                _text.Append("Phase : ").Append(match.Phase).Append("  ").Append(Mathf.CeilToInt(match.RemainingTime)).Append(" s");

            _label.text = _text.ToString();
        }

        private void SetVisible(bool isVisible)
        {
            _isVisible = isVisible;
            if (_label != null)
                _label.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

            // Affiche tout de suite au lieu d'attendre le prochain rafraîchissement.
            _nextRefreshTime = 0f;
        }
    }
}
