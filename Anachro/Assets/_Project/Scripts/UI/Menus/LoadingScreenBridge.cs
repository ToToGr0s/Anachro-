using FishNet;
using FishNet.Managing.Client;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    /// <summary>
    /// Affiche l'écran de chargement d'Hanaé (LoadScreenScript + LoadingScreen.uxml) pendant les chargements de scène
    /// réseau (la map au lancement de la partie ou en rejoignant), avec leur progression.
    /// </summary>
    [RequireComponent(typeof(UIDocument), typeof(LoadScreenScript))]
    public sealed class LoadingScreenBridge : MonoBehaviour
    {
        private UIDocument _document;
        private LoadScreenScript _loadScreen;
        private SceneManager _sceneManager;
        private ClientManager _clientManager;

        private void Awake()
        {
            // Vit dans Boot : doit survivre au chargement de la map qu'il affiche.
            DontDestroyOnLoad(gameObject);
            _document = GetComponent<UIDocument>();
            _loadScreen = GetComponent<LoadScreenScript>();
        }

        private void Start()
        {
            SetShown(false);
            _sceneManager = InstanceFinder.SceneManager;
            if (_sceneManager == null)
            {
                Debug.LogError("[LoadingScreenBridge] Pas de NetworkManager : lancer le jeu depuis Boot.");
                return;
            }

            _sceneManager.OnLoadStart += OnLoadStart;
            _sceneManager.OnLoadPercentChange += OnLoadPercentChange;
            _sceneManager.OnLoadEnd += OnLoadEnd;
            // Connexion perdue en plein chargement : OnLoadEnd n'arrive jamais, l'écran resterait sur le titre.
            _clientManager = InstanceFinder.ClientManager;
            _clientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void OnDestroy()
        {
            if (_sceneManager == null)
                return;

            _sceneManager.OnLoadStart -= OnLoadStart;
            _sceneManager.OnLoadPercentChange -= OnLoadPercentChange;
            _sceneManager.OnLoadEnd -= OnLoadEnd;
            if (_clientManager != null)
                _clientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        private void OnLoadStart(SceneLoadStartEventArgs args)
        {
            _loadScreen.UpdateBar(0f);
            SetShown(true);
        }

        // Sa barre va de 0 à 100 : FishNet donne la progression de 0 à 1.
        private void OnLoadPercentChange(SceneLoadPercentEventArgs args) => _loadScreen.UpdateBar(args.Percent * 100f);

        private void OnLoadEnd(SceneLoadEndEventArgs args) => SetShown(false);

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped)
                SetShown(false);
        }

        private void SetShown(bool isShown) =>
            _document.rootVisualElement.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
