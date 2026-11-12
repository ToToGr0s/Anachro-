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
        [SerializeField, Range(0f, 10f)] private float _walkSpeed = 2f;
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
        [Tooltip("Recul de la camera derriere l axe du joueur (m). Evite de voir a travers un mur quand on s y colle.")]
        [SerializeField, Range(0f, 0.3f)] private float _cameraBackOffset = 0.1f;
        [Tooltip("Plan proche de la camera (m). Doit rester plus petit que le rayon du joueur, sinon le mur est coupe.")]
        [SerializeField, Range(0.01f, 0.3f)] private float _nearClipPlane = 0.1f;
        [Tooltip("Augmentation du FOV pendant la course (degres).")]
        [SerializeField, Range(0f, 20f)] private float _sprintFovBoost = 6f;
        [Tooltip("Vitesse a laquelle le FOV rejoint sa cible (plus grand = plus brusque).")]
        [SerializeField, Min(1f)] private float _fovSmoothing = 8f;

        [Header("Anti-triche (verifie par l hote)")]
        [Tooltip("Marge sur la vitesse de course avant de recaler un joueur (lag, arrondis).")]
        [SerializeField, Range(1f, 2f)] private float _serverSpeedTolerance = 1.3f;
        [Tooltip("Deplacement accepte d un coup apres un trou de paquets, en secondes de course.")]
        [SerializeField, Range(0.2f, 3f)] private float _serverMoveBurst = 1f;
        [Tooltip("Marge sur la hauteur de saut (m).")]
        [SerializeField, Range(0f, 2f)] private float _serverHeightTolerance = 0.3f;
        [Tooltip("Marge sur la duree d un saut avant de considerer qu il vole (s).")]
        [SerializeField, Range(0f, 3f)] private float _serverAirTimeTolerance = 0.5f;

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
        public float CameraBackOffset => _cameraBackOffset;
        public float NearClipPlane => _nearClipPlane;
        public float SprintFovBoost => _sprintFovBoost;
        public float FovSmoothing => _fovSmoothing;
        public float ServerSpeedTolerance => _serverSpeedTolerance;
        public float ServerMoveBurst => _serverMoveBurst;
        public float ServerHeightTolerance => _serverHeightTolerance;
        public float ServerAirTimeTolerance => _serverAirTimeTolerance;
    }
}
