using FishNet.Object;
using UnityEngine;

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
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (!IsOwner)
                return;

            SetOwnerComponentsEnabled(false);
            if (_hiddenSceneCamera != null)
                _hiddenSceneCamera.SetActive(true);

            // MouseLook verrouille la souris : de retour au menu, il faut pouvoir cliquer.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
