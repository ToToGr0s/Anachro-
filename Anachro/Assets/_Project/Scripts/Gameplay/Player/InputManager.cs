using UnityEngine;
using UnityEngine.InputSystem;

namespace Anachronism.Gameplay
{
    // Relais entre le PlayerInput (UnityEvents) et le deplacement.
    // TODO (Celian) : tir / pose d'objet (clic gauche) et scanner (clic droit), GDD 2.1.
    public class InputManager : MonoBehaviour
    {
        [SerializeField] private CharacterMovement _characterMovement;

        public void OnMove(InputAction.CallbackContext context)
        {
            _characterMovement.Move(context.ReadValue<Vector3>());
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                _characterMovement.Jump();
            }
        }

        // Maintenu = court. canceled arrive au relachement (et a la desactivation de la map).
        public void OnSprint(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                _characterMovement.SetSprinting(true);
            }
            else if (context.canceled)
            {
                _characterMovement.SetSprinting(false);
            }
        }
    }
}
