using System.Collections.Generic;
using Anachronism.Core;
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

        private Camera _camera;
        private CharacterMovement _movement;
        private Renderer[] _bodyRenderers; // corps du joueur (hors camera): cache a sa propre vue
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

            _camera = GetComponent<Camera>();
            _camera.fieldOfView = _settings.FieldOfView;
            // Plan proche plus petit que le rayon du joueur: sinon, colle a un mur, ses coins passent derriere et on voit a travers.
            _camera.nearClipPlane = _settings.NearClipPlane;
            _movement = _playerBody.GetComponentInChildren<CharacterMovement>(true);

            // Les renderers sous la camera (future arme en main, GDD 2.3) restent visibles: c'est la vue du joueur.
            var bodyRenderers = new List<Renderer>();
            foreach (Renderer bodyRenderer in _playerBody.GetComponentsInChildren<Renderer>(true))
            {
                if (!bodyRenderer.transform.IsChildOf(transform))
                    bodyRenderers.Add(bodyRenderer);
            }
            _bodyRenderers = bodyRenderers.ToArray();
        }

        // Le corps (et donc son ombre) ne doit pas etre visible depuis la camera du joueur lui-meme : en regardant vers le bas,
        // l'ombre de la capsule remplit l'ecran. Actif seulement tant que ce script l'est : un autre joueur
        // (controleur desactive) garde son corps visible.
        // TODO (Theo) : une fois en reseau, n'activer ce script que pour l'owner (IsOwner).
        private void OnEnable()
        {
            SetBodyVisible(false);
        }

        private void OnDisable()
        {
            SetBodyVisible(true);
        }

        private void SetBodyVisible(bool isVisible)
        {
            if (_bodyRenderers == null) return;

            foreach (Renderer bodyRenderer in _bodyRenderers)
            {
                if (bodyRenderer != null)
                    bodyRenderer.enabled = isVisible;
            }
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

            // Echap ouvre le menu (UI/PauseMenu), qui libere la souris : pas de regard tant qu'il est ouvert.
            if (GameMenuState.IsOpen) return;

            // Scene de test sans Boot, donc sans menu : Echap libere la souris lui-meme.
            if (!GameMenuState.IsAvailable && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Clic gauche pour recapturer la souris (perdue en changeant de fenetre)
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

        // Apres CharacterMovement.Update, et hors des sorties anticipees d'Update (pas de souris, curseur libre):
        // le FOV s'elargit un peu pendant la course, en douceur, pour donner une impression de vitesse.
        private void LateUpdate()
        {
            float target = _settings.FieldOfView;
            if (_movement != null && _movement.IsSprinting)
                target += _settings.SprintFovBoost;

            float blend = 1f - Mathf.Exp(-_settings.FovSmoothing * Time.deltaTime);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, target, blend);
        }
    }
}
