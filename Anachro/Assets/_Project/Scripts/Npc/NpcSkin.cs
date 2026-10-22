using UnityEngine;

namespace Anachronism.Npc
{
    /// <summary>
    /// Apparence d'un PNJ : modèle et Animator piloté par la seule vitesse horizontale.
    /// Le même composant habille les joueurs : un joueur et un PNJ s'animent avec exactement le même code (règle réseau 5).
    /// </summary>
    // Posé sur l'enfant « Model ». Tout se règle dans le prefab : le skin tiré prend la place de l'aperçu,
    // avec sa position, sa rotation et son échelle. Le script n'impose aucune taille.
    public class NpcSkin : MonoBehaviour
    {
        private Vector3 _modelPosition = Vector3.zero;
        private Quaternion _modelRotation = Quaternion.identity;
        private Vector3 _modelScale = Vector3.one;

        private GameObject _model;
        private Animator _animator;
        private int _speedHash;
        private bool _hasSpeedParam;
        private NpcProfile _appliedSkin;
        private float _baseSpeed;
        private float _sprintMultiplier;

        public NpcProfile AppliedSkin => _appliedSkin;
        public GameObject Model => _model;

        private void Awake()
        {
            // L'aperçu posé dans le prefab laisse la place au skin tiré, qui garde exactement son placement.
            if (transform.childCount > 0)
            {
                Transform preview = transform.GetChild(0);
                _modelPosition = preview.localPosition;
                _modelRotation = preview.localRotation;
                _modelScale = preview.localScale;
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
        }

        /// <param name="skin">Profil dont on prend l'apparence (modèle, Animator, échelle).</param>
        /// <param name="baseSpeed">Vitesse de marche de référence : en dessous, l'Animator passe de l'arrêt à la marche.</param>
        /// <param name="sprintMultiplier">Multiplicateur de course : au-delà de la marche, l'Animator passe à la course.</param>
        public void Apply(NpcProfile skin, float baseSpeed, float sprintMultiplier)
        {
            _baseSpeed = baseSpeed;
            _sprintMultiplier = sprintMultiplier;
            if (skin == _appliedSkin && _model != null)
                return;

            Clear();
            _appliedSkin = skin;
            if (skin == null || skin.ModelPrefab == null)
                return;

            _model = Instantiate(skin.ModelPrefab, transform);
            _model.transform.SetLocalPositionAndRotation(_modelPosition, _modelRotation);
            _model.transform.localScale = _modelScale;

            _animator = _model.GetComponentInChildren<Animator>();
            if (_animator == null)
                return;

            _animator.speed = skin.AnimPlaybackSpeed;
            _animator.applyRootMotion = false;   // c'est le NavMeshAgent (ou le contrôleur du joueur) qui déplace
            if (skin.AnimatorController != null)
                _animator.runtimeAnimatorController = skin.AnimatorController;

            _speedHash = Animator.StringToHash(skin.SpeedParameter);
            _hasSpeedParam = false;
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.nameHash == _speedHash) { _hasSpeedParam = true; break; }
            }

            if (!_hasSpeedParam)
                Debug.LogWarning($"Animator de {name} : paramètre '{skin.SpeedParameter}' introuvable.", this);
        }

        public void Clear()
        {
            if (_model != null)
                Destroy(_model);

            _model = null;
            _animator = null;
            _hasSpeedParam = false;
            _appliedSkin = null;
        }

        public void SetSpeed(float horizontalSpeed, float dt)
        {
            if (_animator == null || !_hasSpeedParam) return;

            _animator.SetFloat(_speedHash, ToAnimValue(horizontalSpeed), 0.1f, dt);
        }

        private float ToAnimValue(float speed)
        {
            float halfWalk = _baseSpeed * 0.5f;
            float walkMax = _baseSpeed * 1.15f;                       // tolère le bruit de vitesse
            float sprintSpeed = _baseSpeed * _sprintMultiplier;

            if (halfWalk <= 0f) return 0f;
            if (speed < halfWalk) return speed / halfWalk;            // idle -> marche
            if (speed <= walkMax || sprintSpeed <= walkMax) return 1f;   // plateau de marche
            return 1f + Mathf.Clamp01((speed - walkMax) / (sprintSpeed - walkMax));   // marche -> course
        }
    }
}
