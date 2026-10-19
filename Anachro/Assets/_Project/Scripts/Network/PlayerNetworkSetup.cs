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
    /// </summary>
    public sealed class PlayerNetworkSetup : NetworkBehaviour
    {
        [Tooltip("Composants actifs seulement chez l'owner : PlayerInput, InputManager, CharacterMovement, MouseLook, Camera, AudioListener.")]
        [SerializeField] private Behaviour[] _ownerOnlyComponents = new Behaviour[0];
        [SerializeField] private CapsuleCollider _capsule;
        [SerializeField] private Camera _camera;
        [SerializeField] private PlayerInput _playerInput;

        private GameObject _hiddenSceneCamera;

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsOwner)
            {
                // CharacterMovement met sa capsule en trigger pour que ses propres requêtes l'ignorent. Un joueur distant
                // doit au contraire bloquer l'owner, comme un PNJ : sinon traverser quelqu'un trahit que c'est un joueur.
                if (_capsule != null)
                    _capsule.isTrigger = false;
                return;
            }

            // La caméra de vue d'ensemble de la map rendrait par-dessus la nôtre et doublerait l'AudioListener.
            Camera sceneCamera = Camera.main;
            if (sceneCamera != null && sceneCamera != _camera)
            {
                _hiddenSceneCamera = sceneCamera.gameObject;
                _hiddenSceneCamera.SetActive(false);
            }

            SetOwnerComponentsEnabled(true);
            GameMenuState.OpenChanged += OnMenuOpenChanged;
            OnMenuOpenChanged(GameMenuState.IsOpen);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (!IsOwner)
                return;

            GameMenuState.OpenChanged -= OnMenuOpenChanged;
            SetOwnerComponentsEnabled(false);
            if (_hiddenSceneCamera != null)
                _hiddenSceneCamera.SetActive(true);

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

        private void SetOwnerComponentsEnabled(bool isEnabled)
        {
            foreach (Behaviour component in _ownerOnlyComponents)
            {
                if (component != null)
                    component.enabled = isEnabled;
            }
        }
    }
}
