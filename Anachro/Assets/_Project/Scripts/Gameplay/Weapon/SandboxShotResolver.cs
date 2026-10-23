#if UNITY_EDITOR
using FishNet.Object;
using UnityEngine;

namespace Anachronism.Gameplay
{
    // TEST LOCAL SEULEMENT (scenes Sandbox) : fait le travail du serveur pour le tir, sans reseau.
    // Il refait le rayon depuis l'intention, comme le fait l'hote (NetworkShooting), et renvoie le resultat a l'outil.
    public class SandboxShotResolver : MonoBehaviour
    {
        [SerializeField] private HunterTool _tool;
        [SerializeField] private GameplayTuning _tuning;

        private NetworkObject _networkObject;

        private void Awake()
        {
            if (_tool == null || _tuning == null)
            {
                Debug.LogError("SandboxShotResolver : outil ou reglages manquant.", this);
                enabled = false;
                return;
            }

            _networkObject = GetComponentInParent<NetworkObject>();
        }

        private void OnEnable()
        {
            _tool.ShotRequested += OnShotRequested;
        }

        private void OnDisable()
        {
            _tool.ShotRequested -= OnShotRequested;
        }

        private void OnShotRequested(ShotRequest request)
        {
            // Joueur spawne en reseau : le resultat viendra de l'hote, une reponse locale le doublerait.
            if (_networkObject != null && _networkObject.IsSpawned)
                return;

            ShotTargetKind kind = ShotTargetKind.Nothing;
            if (ShotRules.Raycast(request, _tuning, _tool.ShooterRoot, null, out RaycastHit hit))
            {
                // Une fois par tir (pas dans Update) : le GetComponentInParent reste ponctuel.
                SandboxShotTarget target = hit.collider.GetComponentInParent<SandboxShotTarget>();
                if (target != null)
                    kind = target.Kind;
            }

            _tool.ApplyResult(ShotRules.ResultFor(kind));
        }
    }
}
#endif