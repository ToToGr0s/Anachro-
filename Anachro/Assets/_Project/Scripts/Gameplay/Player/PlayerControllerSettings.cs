using UnityEngine;

namespace Anachronism.Gameplay
{
    // Données du personnage du joueur, utilisées par PlayerController.

    [CreateAssetMenu(menuName = "Anachronism/Player Controller Settings", fileName = "PlayerControllerSettings")]
    public class PlayerControllerSettings : ScriptableObject
    {
        [Header("Corps (GDD 2.3)")]
        [SerializeField, Min(0.5f)] private float _height = 1.8f;
        [SerializeField, Min(0.1f)] private float _radius = 0.4f;
        [SerializeField, Range(0f, 2f)] private float _walkSpeed = 2f;
        [SerializeField, Min(0f)] private float _jumpSpeed = 5f;
        [Tooltip("Multiplicateur de la vitesse de marche pendant la course (1.25 = 25 % plus vite).")]
        [SerializeField, Range(1f, 2f)] private float _sprintMultiplier = 1.25f;
        [SerializeField] private float _gravity = -9.81f;

        [Header("Terrain")]
        [SerializeField, Range(0f, 80f)] private float _maxSlopeAngle = 45f;
        [SerializeField, Min(0f)] private float _maxStepHeight = 0.3f;
        [Tooltip("Plus grand = la camera rattrape plus vite apres une marche.")]
        [SerializeField, Min(1f)] private float _stepSmoothing = 14f;

        [Header("Camera (GDD 2.2)")]
        [SerializeField, Min(0.1f)] private float _cameraHeight = 1.75f;
        [SerializeField, Range(30f, 120f)] private float _fieldOfView = 90f;
        [SerializeField, Range(0f, 89f)] private float _maxPitch = 70f;
        [SerializeField, Min(0f)] private float _lookSensitivity = 0.1f;

        public float Height => _height;
        public float Radius => _radius;
        public float WalkSpeed => _walkSpeed;
        public float JumpSpeed => _jumpSpeed;
        public float SprintMultiplier => _sprintMultiplier;
        public float Gravity => _gravity;
        public float MaxSlopeAngle => _maxSlopeAngle;
        public float MaxStepHeight => _maxStepHeight;
        public float StepSmoothing => _stepSmoothing;
        public float CameraHeight => _cameraHeight;
        public float FieldOfView => _fieldOfView;
        public float MaxPitch => _maxPitch;
        public float LookSensitivity => _lookSensitivity;
    }
}
