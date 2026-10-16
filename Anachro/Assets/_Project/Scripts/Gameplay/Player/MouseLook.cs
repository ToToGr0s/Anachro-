using UnityEngine;
using UnityEngine.InputSystem;

namespace Anachronism.Gameplay
{
    // Orientation first person: la souris tourne le corps (axe Y) et incline la camera (axe X).
    // TODO (Celian) : passer par une action "Look" (manette: joystick droit, GDD 2.1) au lieu de Mouse.current.
    [RequireComponent(typeof(Camera))]
    public class MouseLook : MonoBehaviour
    {
        [SerializeField] private PlayerControllerSettings _settings;
        [SerializeField] private Transform _playerBody; // parent: rotation horizontale

        private float _pitch;
        private float _yaw;

        private void Awake()
        {
            if (_settings == null)
            {
                Debug.LogError("MouseLook: PlayerControllerSettings manquant.", this);
                enabled = false;
                return;
            }

            GetComponent<Camera>().fieldOfView = _settings.FieldOfView;
        }

        private void Start()
        {
            _yaw = _playerBody.eulerAngles.y;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (Mouse.current == null) return;

            // Echap libere la souris (la pause du GDD 2.1 viendra du menu)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Clic gauche pour recapturer la souris
            if (Cursor.lockState != CursorLockMode.Locked && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 delta = Mouse.current.delta.ReadValue() * _settings.LookSensitivity;

            // Haut/bas: camera seulement, limitee (GDD 2.2: -70 a +70)
            _pitch = Mathf.Clamp(_pitch - delta.y, -_settings.MaxPitch, _settings.MaxPitch);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // Gauche/droite: tourne le corps
            _yaw += delta.x;
            _playerBody.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }
    }
}
