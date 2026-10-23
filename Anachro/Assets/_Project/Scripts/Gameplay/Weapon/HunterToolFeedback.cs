using UnityEngine;

namespace Anachronism.Gameplay
{
    // FX et son du tir (placeholders, C03-4). Identiques quoi qu'on touche : seul le serveur sait si c'etait le mechant.
    public class HunterToolFeedback : MonoBehaviour
    {
        [SerializeField] private HunterTool _tool;
        [Tooltip("Trait du tir, de l'outil jusqu'au point touche.")]
        [SerializeField] private LineRenderer _tracer;
        [Tooltip("Petit marqueur au point touche (sans collider).")]
        [SerializeField] private Transform _impactMarker;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _shotClip;
        [SerializeField, Min(0f)] private float _fxDuration = 0.1f;

        private float _hideTime;

        private void Awake()
        {
            if (_tool == null || _tracer == null || _impactMarker == null)
            {
                Debug.LogError("HunterToolFeedback : outil, trait ou marqueur manquant.", this);
                enabled = false;
                return;
            }

            HideFx();
        }

        private void OnEnable()
        {
            _tool.ShotFired += OnShotFired;
        }

        private void OnDisable()
        {
            _tool.ShotFired -= OnShotFired;
            HideFx();
        }

        private void Update()
        {
            if (_hideTime > 0f && Time.time >= _hideTime)
                HideFx();
        }

        private void OnShotFired(bool hasHit, Vector3 point)
        {
            _tracer.SetPosition(0, _tracer.transform.position);
            _tracer.SetPosition(1, point);
            _tracer.enabled = true;

            _impactMarker.position = point;
            _impactMarker.gameObject.SetActive(hasHit);

            // Son optionnel tant que l'audio (Olivier) n'a pas fourni le vrai.
            if (_audioSource != null && _shotClip != null)
                _audioSource.PlayOneShot(_shotClip);

            _hideTime = Time.time + _fxDuration;
        }

        private void HideFx()
        {
            _tracer.enabled = false;
            _impactMarker.gameObject.SetActive(false);
            _hideTime = 0f;
        }
    }
}