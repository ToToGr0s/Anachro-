using System;
using Anachronism.Gameplay;
using Anachronism.World;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Tir du chasseur (T06). Le client n'envoie que la direction visée : l'hôte tire depuis l'œil qu'il connaît, refait
    /// le rayon avec les mêmes règles que le client (ShotRules), décide de ce qui est touché et applique les conséquences.
    /// Le tireur reçoit le résultat (TargetRpc) ; les autres ne voient que le trait et l'impact, jamais le résultat.
    /// </summary>
    public sealed class NetworkShooting : NetworkBehaviour
    {
        [SerializeField] private HunterTool _tool;
        [SerializeField] private GameplayTuning _tuning;
        // TODO (Théo) : l'époque viendra de la carte choisie au lobby (T09), comme pour NetworkPlacement et ObjectSpawner.
        [SerializeField] private EraDefinition _era;
        // Œil du joueur : chez l'hôte la caméra d'un autre joueur est éteinte, mais son transform suit le NetworkTransform.
        [SerializeField] private Transform _eye;

        // Côté serveur : premier tick où ce joueur peut de nouveau tirer.
        private uint _nextShotTick;
        private ObjectSpawner _objects;
        // Créé une fois : un délégué recréé à chaque tir allouerait.
        private Func<Collider, bool> _isCapturedVillain;

        private void Awake()
        {
            _isCapturedVillain = collider => ShotTargetResolver.IsCapturedVillain(collider, MatchManager.Instance);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Avec le pool, l'instance peut resservir à un autre joueur.
            _nextShotTick = 0;
            // Résolu une fois : le tir ne cherche plus dans la scène (l'ObjectSpawner est dans la map, comme ce joueur).
            _objects = FindAnyObjectByType<ObjectSpawner>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner && _tool != null)
                _tool.ShotRequested += OnShotRequested;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_tool != null)
                _tool.ShotRequested -= OnShotRequested;
        }

        private void OnShotRequested(ShotRequest request)
        {
            // L'origine reste chez le client : elle ne sert qu'à son propre trait.
            ShootServerRpc(request.Direction);
        }

        // RequireOwnership (par défaut) : un client ne peut pas tirer au nom d'un autre joueur.
        [ServerRpc]
        private void ShootServerRpc(Vector3 direction)
        {
            MatchManager match = MatchManager.Instance;
            var request = new ShotRequest(_eye != null ? _eye.position : Vector3.zero, direction);
            if (!CanShoot(match, request, out string reason))
            {
                RefusedRequests.Report("Tir", Owner.ClientId, reason, this);
                return;
            }

            _nextShotTick = TimeManager.Tick
                + TimeManager.TimeToTicks(_tuning.ShootCooldown * _tuning.ServerCooldownTolerance, TickRounding.RoundDown);

            bool hasHit = ShotRules.Raycast(request, _tuning, NetworkObject.transform, _isCapturedVillain, out RaycastHit hit);
            ShotTargetKind kind = hasHit ? ShotTargetResolver.KindOf(hit.collider, _era.Era, match) : ShotTargetKind.Nothing;
            Vector3 point = hasHit ? hit.point : request.Origin + request.Direction * _tuning.ShootDistance;

            ShotResult result = ShotRules.ResultFor(kind);
            ShotDebugLog.Record(Owner.ClientId, request.Origin, point, hasHit, result);

            // Avant les conséquences : une capture peut finir la partie, le tireur doit quand même voir son résultat.
            TargetShotResult(Owner, result);
            ObserversShotFired(hasHit, point);
            ApplyConsequences(match, kind, hasHit ? hit.collider : null);
        }

        // Toute la RPC est hostile (règle réseau 2) : réglages, phase, rôle, cadence, direction.
        [Server]
        private bool CanShoot(MatchManager match, ShotRequest request, out string reason)
        {
            if (match == null || _tuning == null || _era == null || _eye == null)
            {
                reason = "MatchManager, réglages, époque ou œil manquant";
                return false;
            }

            // Les chasseurs n'arrivent sur la map qu'à la première pose : pas de tir pendant le repérage.
            if (match.Phase != GamePhase.Searching && match.Phase != GamePhase.LastChance)
            {
                reason = $"phase {match.Phase}";
                return false;
            }

            if (match.GetRoleServer(Owner) != PlayerRole.Guardian)
            {
                reason = "pas un chasseur";
                return false;
            }

            if (TimeManager.Tick < _nextShotTick)
            {
                reason = "cadence";
                return false;
            }

            if (!ShotRules.IsValid(request, out ShotError error))
            {
                reason = error.ToString();
                return false;
            }

            reason = null;
            return true;
        }

        [Server]
        private void ApplyConsequences(MatchManager match, ShotTargetKind kind, Collider hit)
        {
            switch (kind)
            {
                case ShotTargetKind.Anachronism:
                    // La barre baisse de ce que la pose avait ajouté : même calcul que NetworkPlacement. Le nombre de
                    // fugitifs compte aussi les capturés et les partis, il ne change donc pas pendant la partie.
                    WorldObject found = hit.GetComponentInParent<WorldObject>();
                    match.RegisterShot(-_tuning.GetDisruption(found.Definition.Size, match.CountVillainsServer()));
                    DespawnShot(found);
                    break;
                case ShotTargetKind.PeriodObject:
                    // L'outil désintègre ce qu'il touche, objet de l'époque compris : l'erreur coûte la pénalité.
                    match.RegisterShot(_tuning.WrongShotPenalty);
                    DespawnShot(hit.GetComponentInParent<WorldObject>());
                    break;
                case ShotTargetKind.Npc:
                    match.RegisterShot(_tuning.WrongShotPenalty);
                    break;
                case ShotTargetKind.Guardian:
                    match.RegisterShot(_tuning.TeammateShotPenalty);
                    break;
                case ShotTargetKind.Villain:
                    match.CaptureVillain(hit.GetComponentInParent<NetworkObject>().Owner);
                    break;
            }
        }

        [Server]
        private void DespawnShot(WorldObject worldObject)
        {
            if (_objects == null)
            {
                Debug.LogError("[NetworkShooting] Pas d'ObjectSpawner dans la map : objet touché laissé en place.", this);
                return;
            }

            _objects.DespawnShot(worldObject);
        }

        [TargetRpc]
        private void TargetShotResult(NetworkConnection connection, ShotResult result)
        {
            if (_tool != null)
                _tool.ApplyResult(result);
        }

        // Le tireur a déjà son trait (local, sans attendre l'hôte). Le point d'impact ne dit rien de ce qui a été touché.
        [ObserversRpc(ExcludeOwner = true)]
        private void ObserversShotFired(bool hasHit, Vector3 point)
        {
            if (_tool != null)
                _tool.ShowRemoteShot(hasHit, point);
        }
    }
}
