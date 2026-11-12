using System;
using FishNet.Object;
using UnityEngine;

namespace Anachronism.Gameplay
{
    // Cote client du chasseur : vise, tire un rayon et prepare l'intention de tir.
    // L'hote (NetworkShooting) refait le rayon et decide du resultat : ici on ne decide rien de definitif.
    public class HunterTool : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private GameplayTuning _tuning;

        // TODO (Celian) : temps de jeu en ticks (TimeManager) pour le cooldown (regle reseau 6).
        private float _readyTime;
        // Le rayon ignore le corps du tireur : la camera est au-dessus de sa capsule.
        private Transform _shooterRoot;

        // L'intention de tir : NetworkShooting l'envoie a l'hote (ShootServerRpc).
        public event Action<ShotRequest> ShotRequested;
        // FX et son : hasHit = le rayon a touche quelque chose (pas le resultat). Local chez le tireur, envoye par l'hote chez les autres.
        public event Action<bool, Vector3> ShotFired;
        // Resultat renvoye par le serveur, pour le retour visuel.
        public event Action<ShotResult> ShotResolved;
        // Le HUD (persistant, hors scene) suit l'outil qui s'active : seul celui du joueur local l'est (PlayerNetworkSetup).
        public static event Action<HunterTool> Enabled;
        public static event Action<HunterTool> Disabled;
        // Pour un abonne arrive apres l'activation (HUD reactive) : il rattrape l'outil deja actif.
        public static HunterTool Current { get; private set; }

        public bool IsReady => Time.time >= _readyTime;
        public Transform ShooterRoot => _shooterRoot;

        private void Awake()
        {
            if (_camera == null || _tuning == null)
            {
                Debug.LogError("HunterTool : camera ou reglages manquant.", this);
                enabled = false;
            }

            // Hors ligne (scene de test sans NetworkObject), la racine de la hierarchie fait office de corps.
            NetworkObject body = GetComponentInParent<NetworkObject>(true);
            _shooterRoot = body != null ? body.transform : transform.root;
        }

        // Domain reload desactive dans ce projet : sans ce reset, un abonne reste branche d'un Play a l'autre.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Enabled = null;
            Disabled = null;
            Current = null;
        }

        private void OnEnable()
        {
            Current = this;
            Enabled?.Invoke(this);
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;
            Disabled?.Invoke(this);
        }

        public bool TryShoot()
        {
            // PlayerNetworkSetup peut rallumer l'outil meme si Awake l'a eteint (camera ou reglages manquants) : on ne tire pas.
            if (!enabled || !IsReady || _camera == null || _tuning == null)
                return false;

            Transform view = _camera.transform;
            ShotRequest request = new ShotRequest(view.position, view.forward);
            if (!ShotRules.IsValid(request, out _))
                return false;

            bool hasHit = ShotRules.Raycast(request, _tuning, _shooterRoot, null, out RaycastHit hit);
            Vector3 point = hasHit ? hit.point : request.Origin + request.Direction * _tuning.ShootDistance;

            _readyTime = Time.time + _tuning.ShootCooldown;
            ShotRequested?.Invoke(request);
            ShotFired?.Invoke(hasHit, point);
            return true;
        }

        // Appele par le reseau quand le serveur a tranche.
        public void ApplyResult(ShotResult result)
        {
            ShotResolved?.Invoke(result);
        }

        // Tir d'un autre joueur, vu chez nous : memes FX que le tireur, point d'impact calcule par l'hote.
        public void ShowRemoteShot(bool hasHit, Vector3 point)
        {
            ShotFired?.Invoke(hasHit, point);
        }

#if UNITY_EDITOR
        // Test sans serveur (menu du composant, en Play) : simule la reponse du serveur.
        [ContextMenu("Test : objet trouve")] private void TestObjectFound() => ApplyResult(ShotResult.RightObjectFound);
        [ContextMenu("Test : objet d'epoque")] private void TestPeriodObject() => ApplyResult(ShotResult.WrongObjectFound);
        [ContextMenu("Test : innocent")] private void TestInnocent() => ApplyResult(ShotResult.Innocent);
        [ContextMenu("Test : mechant")] private void TestVillain() => ApplyResult(ShotResult.Villain);
        [ContextMenu("Test : coequipier")] private void TestTeammate() => ApplyResult(ShotResult.Teammate);
#endif
    }
}