using UnityEngine;

namespace Anachronism.Npc
{
    /// <summary>
    /// Apparence d'un PNJ : modèle et Animator piloté par la seule vitesse horizontale.
    /// Le même composant habille les joueurs : un joueur et un PNJ s'animent avec exactement le même code (règle réseau 5).
    /// </summary>
    public class NpcSkin : MonoBehaviour
    {
        private GameObject _model;
        private Animator _animator;
        private int _speedHash;
        private bool _hasSpeedParam;
        private NpcProfile _appliedSkin;
        private float _baseSpeed;
        private float _sprintMultiplier;
        private Vector3 _localOffset;

        public NpcProfile AppliedSkin => _appliedSkin;
        public GameObject Model => _model;

        /// <param name="skin">Profil dont on prend l'apparence (modèle, Animator, échelle).</param>
        /// <param name="baseSpeed">Vitesse de marche de référence : en dessous, l'Animator passe de l'arrêt à la marche.</param>
        /// <param name="sprintMultiplier">Multiplicateur de course : au-delà de la marche, l'Animator passe à la course.</param>
        /// <param name="localOffset">Décalage du modèle (un joueur a son pivot au centre de la capsule, un PNJ aux pieds).</param>
        public void Apply(NpcProfile skin, float baseSpeed, float sprintMultiplier, Vector3 localOffset = default)
        {
            _baseSpeed = baseSpeed;
            _sprintMultiplier = sprintMultiplier;
            _localOffset = localOffset;
            if (skin == _appliedSkin && _model != null)
                return;

            Clear();
            _appliedSkin = skin;
            if (skin == null || skin.ModelPrefab == null)
                return;

            _model = Instantiate(skin.ModelPrefab, transform);
            _model.transform.localRotation = Quaternion.identity;
            PlaceModel();

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

        // Hauteur et échelle viennent du profil : un même réglage vaut pour les PNJ et les joueurs qui portent ce skin.
        private void PlaceModel()
        {
            _model.transform.localPosition = _localOffset + Vector3.up * _appliedSkin.ModelHeightOffset;
            _model.transform.localScale = Vector3.one * _appliedSkin.ModelScale;
        }

#if UNITY_EDITOR
        // Réglage en direct : changer la hauteur ou l'échelle du profil pendant le Play déplace le modèle tout de suite.
        private void LateUpdate()
        {
            if (_model != null && _appliedSkin != null)
                PlaceModel();
        }
#endif

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
