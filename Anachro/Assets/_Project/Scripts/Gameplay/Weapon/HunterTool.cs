using System;
using UnityEngine;

namespace Anachronism.Gameplay
{
    // Cote client du chasseur : vise, tire un rayon et prepare l'intention de tir.
    // Le serveur (T06, Theo) refait le rayon et decide du resultat : ici on ne decide rien de definitif.
    public class HunterTool : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private GameplayTuning _tuning;

        // TODO (Celian) : temps de jeu en ticks (TimeManager) pour le cooldown (regle reseau 6).
        private float _readyTime;

        // L'intention de tir : le reseau (Theo) l'enverra au serveur avec ShootServerRpc.
        public event Action<ShotRequest> ShotRequested;
        // Local, pour les FX et le son : hasHit = le rayon local a touche quelque chose (pas le resultat du serveur).
        public event Action<bool, Vector3> ShotFired;
        // Resultat renvoye par le serveur, pour le retour visuel.
        public event Action<ShotResult> ShotResolved;
        // Le HUD (persistant, hors scene) suit l'outil qui s'active : seul celui du joueur local l'est (RoleTools).
        public static event Action<HunterTool> Enabled;
        public static event Action<HunterTool> Disabled;
        // Pour un abonne arrive apres l'activation (HUD reactive) : il rattrape l'outil deja actif.
        public static HunterTool Current { get; private set; }

        public bool IsReady => Time.time >= _readyTime;

        private void Awake()
        {
            if (_camera == null || _tuning == null)
            {
                Debug.LogError("HunterTool : camera ou reglages manquant.", this);
                enabled = false;
            }
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
            // RoleTools peut rallumer l'outil meme si Awake l'a eteint (camera ou reglages manquants) : on ne tire pas.
            if (!enabled || !IsReady || _camera == null || _tuning == null)
                return false;

            Transform view = _camera.transform;
            ShotRequest request = new ShotRequest(view.position, view.forward);
            if (!ShotRules.IsValid(request, view.position, _tuning, out _))
                return false;

            bool hasHit = ShotRules.Raycast(request, _tuning, out RaycastHit hit);
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

        // Test sans serveur (menu du composant, en Play) : simule la reponse du serveur.
        [ContextMenu("Test : objet trouve")] private void TestObjectFound() => ApplyResult(ShotResult.RightObjectFound);
        [ContextMenu("Test : objet d'epoque")] private void TestPeriodObject() => ApplyResult(ShotResult.WrongObjectFound);
        [ContextMenu("Test : innocent")] private void TestInnocent() => ApplyResult(ShotResult.Innocent);
        [ContextMenu("Test : mechant")] private void TestVillain() => ApplyResult(ShotResult.Villain);
    }
}