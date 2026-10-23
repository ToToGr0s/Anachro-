using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Anachronism.Gameplay
{
    // Relais entre le PlayerInput (UnityEvents) et le deplacement.
    // TODO (Celian) : scanner (clic droit), GDD 2.1.
    public class InputManager : MonoBehaviour
    {
        [SerializeField] private CharacterMovement _characterMovement;

        [SerializeField] private FugitivePlacer _placer;   // optionnel : seul le fugitif en a un
        [SerializeField] private HunterTool _hunterTool;   // optionnel : seul le chasseur en a un

        private void OnEnable()
        {
            if (_placer != null)
                _placer.PlacingChanged += OnPlacingChanged;
        }

        private void OnDisable()
        {
            if (_placer != null)
                _placer.PlacingChanged -= OnPlacingChanged;

            // Desabonne : on ne recevra plus PlacingChanged(false), donc le joueur ne doit pas rester bloque.
            if (_characterMovement != null)
                _characterMovement.SetMovementLocked(false);
        }

        public void OnPrimaryAction(InputAction.CallbackContext context)
        {
            // Le clic qui reprend la souris (MouseLook) ne doit ni poser ni tirer.
            if (!context.started || Cursor.lockState != CursorLockMode.Locked)
                return;

            // Un seul outil actif a la fois (RoleTools) : la pose pour le mechant, le tir pour le chasseur.
            if (_placer != null && _placer.enabled)
                _placer.TryPlace();
            else if (_hunterTool != null && _hunterTool.enabled)
                _hunterTool.TryShoot();
        }

        public void OnSelectSize(InputAction.CallbackContext context)
        {
            if (!context.started || _placer == null)
                return;

            // Une seule action, trois touches : on regarde laquelle a declenche.
            // TODO (Celian) : manette (epaulettes) quand le GDD le precisera.
            if (!(context.control is KeyControl key))
                return;

            switch (key.keyCode)
            {
                case Key.Digit1: _placer.SelectSize(AnachronismSize.Small); break;
                case Key.Digit2: _placer.SelectSize(AnachronismSize.Medium); break;
                case Key.Digit3: _placer.SelectSize(AnachronismSize.Large); break;
            }
        }

        // Molette vers le bas : taille suivante ; vers le haut : precedente.
        public void OnScrollSize(InputAction.CallbackContext context)
        {
            if (!context.performed || _placer == null)
                return;

            float scroll = context.ReadValue<float>();
            if (scroll == 0f)
                return;

            _placer.CycleSize(scroll < 0f ? 1 : -1);
        }

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

        private void OnPlacingChanged(bool isPlacing)
        {
            if (_characterMovement != null)
                _characterMovement.SetMovementLocked(isPlacing);
        }
    }
}
