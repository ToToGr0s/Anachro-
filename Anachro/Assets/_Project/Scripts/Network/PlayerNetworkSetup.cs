using Anachronism.Core;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anachronism.Network
{
    /// <summary>
    /// Branche le contrôleur de Célian sur le réseau. Le prefab est livré avec les entrées, la caméra et le déplacement
    /// désactivés : seul l'owner les allume. Les autres instances ne font que suivre le NetworkTransform (client-autoritaire,
    /// seul point accepté par la règle réseau 8).
    /// Hors ligne (copie posée dans une scène de test, « Is Networked » décoché sur le NetworkObject) : la copie se pilote seule.
    /// </summary>
    public sealed class PlayerNetworkSetup : NetworkBehaviour
    {
        [Tooltip("Composants actifs seulement chez l'owner : PlayerInput, InputManager, CharacterMovement, MouseLook, Camera, AudioListener.")]
        [SerializeField] private Behaviour[] _ownerOnlyComponents = new Behaviour[0];
        [Tooltip("Composants actifs seulement chez l'owner fugitif (FugitivePlacer). Hors ligne : toujours actifs, pour tester la pose.")]
        [SerializeField] private Behaviour[] _fugitiveOnlyComponents = new Behaviour[0];
        [SerializeField] private CapsuleCollider _capsule;
        [SerializeField] private Camera _camera;
        [SerializeField] private PlayerInput _playerInput;

        private GameObject _hiddenSceneCamera;
        // Mémorisé plutôt que relu dans IsOwner : à l'arrêt (déconnexion, despawn), l'ownership peut déjà être retirée.
        private bool _hasLocalControl;
        // Mémorisé : MatchManager.Instance peut déjà être détruit quand on se désabonne (fin de partie, retour au menu).
        private MatchManager _match;

        private void Start()
        {
            // Jamais initialisée par FishNet : c'est la copie d'une scène de test hors ligne.
            if (!GetIsNetworked())
                EnableLocalControl();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Écrit dans les deux sens : avec le pool, une même instance peut servir à un joueur distant puis à l'owner.
            // L'owner garde sa capsule en trigger pour que ses propres requêtes l'ignorent (CharacterMovement). Un joueur
            // distant doit au contraire bloquer l'owner, comme un PNJ : sinon traverser quelqu'un trahit que c'est un joueur.
            if (_capsule != null)
                _capsule.isTrigger = IsOwner;

            if (!IsOwner)
                return;

            // La caméra de vue d'ensemble de la map rendrait par-dessus la nôtre et doublerait l'AudioListener.
            Camera sceneCamera = Camera.main;
            if (sceneCamera != null && sceneCamera != _camera)
            {
                _hiddenSceneCamera = sceneCamera.gameObject;
                _hiddenSceneCamera.SetActive(false);
            }

            EnableLocalControl();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            DisableLocalControl();
        }

        private void OnDestroy()
        {
            // Filet de sécurité : l'événement est statique, un abonnement oublié appellerait un objet détruit.
            if (_hasLocalControl)
                GameMenuState.OpenChanged -= OnMenuOpenChanged;
            if (_match != null)
                _match.LocalRoleAssigned -= OnLocalRoleAssigned;
        }

        private void EnableLocalControl()
        {
            if (_hasLocalControl)
                return;

            _hasLocalControl = true;
            SetComponentsEnabled(_ownerOnlyComponents, true);
            GameMenuState.OpenChanged += OnMenuOpenChanged;
            OnMenuOpenChanged(GameMenuState.IsOpen);

            // Hors ligne il n'y a pas de rôle : la copie de test peut toujours poser.
            if (!GetIsNetworked())
            {
                SetComponentsEnabled(_fugitiveOnlyComponents, true);
                return;
            }

            // Le rôle arrive par TargetRpc, avant ou après le spawn du joueur, et change d'une partie à l'autre.
            _match = MatchManager.Instance;
            if (_match == null)
            {
                Debug.LogWarning("[PlayerNetworkSetup] Pas de MatchManager : les composants du fugitif restent éteints.", this);
                return;
            }

            _match.LocalRoleAssigned += OnLocalRoleAssigned;
            OnLocalRoleAssigned(_match.LocalRole);
        }

        private void DisableLocalControl()
        {
            if (!_hasLocalControl)
                return;

            _hasLocalControl = false;
            GameMenuState.OpenChanged -= OnMenuOpenChanged;
            if (_match != null)
                _match.LocalRoleAssigned -= OnLocalRoleAssigned;
            _match = null;
            SetComponentsEnabled(_fugitiveOnlyComponents, false);
            SetComponentsEnabled(_ownerOnlyComponents, false);
            if (_hiddenSceneCamera != null)
                _hiddenSceneCamera.SetActive(true);
            _hiddenSceneCamera = null;

            // MouseLook verrouille la souris : de retour au menu, il faut pouvoir cliquer.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Menu Échap ouvert : souris libre pour cliquer, et plus d'entrées (le joueur s'arrête au lieu de garder
        // la dernière touche enfoncée : désactiver les actions envoie leur « canceled »).
        private void OnMenuOpenChanged(bool isOpen)
        {
            if (_playerInput != null)
            {
                if (isOpen)
                    _playerInput.DeactivateInput();
                else
                    _playerInput.ActivateInput();
            }

            Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isOpen;
        }

        // Côté client seulement, pour l'affichage et les entrées : l'hôte revérifie le rôle à chaque pose.
        private void OnLocalRoleAssigned(PlayerRole role) =>
            SetComponentsEnabled(_fugitiveOnlyComponents, role == PlayerRole.Villain);

        private static void SetComponentsEnabled(Behaviour[] components, bool isEnabled)
        {
            foreach (Behaviour component in components)
            {
                if (component != null)
                    component.enabled = isEnabled;
            }
        }
    }
}
