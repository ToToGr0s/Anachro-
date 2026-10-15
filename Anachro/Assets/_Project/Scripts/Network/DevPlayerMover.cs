using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anachronism.Network
{
    /// <summary>
    /// Déplacement minimal (ZQSD / flèches) pour tester la synchro réseau : latence, interpolation, à-coups.
    /// Client-autoritaire via le NetworkTransform : c'est le seul point accepté par la règle réseau 8.
    /// </summary>
    // TODO (Théo) : retirer de Player_Base quand le contrôleur de Célian (C01) le remplace.
    public sealed class DevPlayerMover : NetworkBehaviour
    {
        [SerializeField] private float _speed = 5f;
        [SerializeField] private Color _ownerColor = new Color(0.2f, 0.6f, 1f);

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Sa propre capsule en couleur : on se repère tout de suite parmi les autres joueurs.
            if (IsOwner && TryGetComponent(out Renderer capsuleRenderer))
                capsuleRenderer.material.color = _ownerColor;
        }

        private void Update()
        {
            if (!IsOwner)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            // Touches physiques : wKey/aKey = Z/Q sur un clavier AZERTY.
            Vector3 direction = Vector3.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                direction.z += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                direction.z -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                direction.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                direction.x -= 1f;

            if (direction != Vector3.zero)
                transform.position += direction.normalized * (_speed * Time.deltaTime);
        }
    }
}
