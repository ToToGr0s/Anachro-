using Anachronism.Gameplay;
using Anachronism.World;
using FishNet.Object;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Pose d'un objet par un fugitif (T07). Le client envoie son intention : objet tenu, point visé, normale.
    /// L'hôte revalide avec les mêmes règles que l'aperçu (PlacementRules, PlacementSpace), puis ObjectSpawner fait
    /// apparaître le WorldObject chez tout le monde et MatchManager monte la barre.
    /// </summary>
    public sealed class NetworkPlacement : NetworkBehaviour
    {
        [SerializeField] private FugitivePlacer _placer;
        [SerializeField] private GameplayTuning _tuning;
        [SerializeField] private AnachronismRegistry _registry;
        // TODO (Théo) : l'époque viendra de la carte choisie au lobby (T09), comme pour ObjectSpawner et FugitivePlacer.
        [SerializeField] private EraDefinition _era;
        // Œil du joueur : chez l'hôte la caméra d'un autre joueur est éteinte, mais son transform suit le NetworkTransform.
        [SerializeField] private Transform _eye;

        private ObjectSpawner _objects;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner && _placer != null)
                _placer.ItemPlaced += OnItemPlaced;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_placer != null)
                _placer.ItemPlaced -= OnItemPlaced;
        }

        private void OnItemPlaced(PlacementRequest request, AnachronismDefinition definition)
        {
            if (_registry == null || !_registry.TryGetId(definition, out byte definitionId))
            {
                Debug.LogError($"[NetworkPlacement] {(definition != null ? definition.name : "null")} absent du registre : pose non envoyée.", this);
                return;
            }

            // La taille n'est pas envoyée : l'hôte la lit dans la définition.
            PlaceServerRpc(definitionId, request.Position, request.Normal);
        }

        // RequireOwnership (par défaut) : un client ne peut pas poser au nom d'un autre joueur.
        // TODO (Théo) : aucune cadence vérifiée ici (choix du 22/10) ; un client modifié peut enchaîner les poses
        // et remplir la barre. À fermer avec une recharge côté hôte (GameplayTuning.GetReloadSeconds, en ticks).
        [ServerRpc]
        private void PlaceServerRpc(byte definitionId, Vector3 position, Vector3 normal)
        {
            MatchManager match = MatchManager.Instance;
            if (!CanPlace(match, definitionId, position, normal, out AnachronismDefinition definition, out string reason))
            {
                Debug.LogWarning($"[NetworkPlacement] Pose refusée (client {Owner.ClientId}) : {reason}.", this);
                return;
            }

            if (_objects == null)
                _objects = FindAnyObjectByType<ObjectSpawner>();
            if (_objects == null)
            {
                Debug.LogError("[NetworkPlacement] Pas d'ObjectSpawner dans la map : pose impossible.", this);
                return;
            }

            // Même placement que l'aperçu : PositionFor donne le centre de la boîte, le pivot du mesh en est décalé de BoundsCenter.
            Quaternion rotation = PlacementRules.RotationFor(normal, _tuning);
            Vector3 pivot = PlacementRules.PositionFor(position, normal, definition.Dimensions, _tuning) - rotation * definition.BoundsCenter;
            if (!_objects.SpawnPlaced(definition, pivot, rotation))
            {
                Debug.LogError($"[NetworkPlacement] {definition.name} ne peut pas être affiché par les clients : pose annulée.", this);
                return;
            }

            match.RegisterPlacement(_tuning.GetDisruption(definition.Size, match.CountVillainsServer()));
        }

        // Toute la RPC est hostile (règle réseau 2) : rôle, phase, objet, distance, surface, ligne de vue, appui, place libre.
        [Server]
        private bool CanPlace(MatchManager match, byte definitionId, Vector3 position, Vector3 normal,
            out AnachronismDefinition definition, out string reason)
        {
            definition = null;

            if (match == null || _tuning == null || _registry == null || _era == null || _eye == null)
            {
                reason = "MatchManager, réglages, registre, époque ou œil manquant";
                return false;
            }

            if (!match.IsPlayingPhase)
            {
                reason = $"phase {match.Phase}";
                return false;
            }

            if (!match.IsActiveVillainServer(Owner))
            {
                reason = "pas un fugitif en jeu";
                return false;
            }

            definition = _registry.Get(definitionId);
            if (definition == null || definition.Mesh == null || !IsPlaceableInEra(definition))
            {
                reason = $"objet {definitionId} non posable dans cette époque";
                return false;
            }

            var request = new PlacementRequest(definition.Size, position, normal);
            Vector3 eye = _eye.position;
            if (!PlacementRules.IsValid(request, definition.AllowedSurfaces, eye, _tuning, out PlacementError error)
                || !PlacementSpace.Check(request, definition.Dimensions, _tuning, eye, _tuning.PlacementMask, _tuning.PlacedObjectsMask, out error))
            {
                reason = error.ToString();
                return false;
            }

            reason = null;
            return true;
        }

        private bool IsPlaceableInEra(AnachronismDefinition definition)
        {
            foreach (AnachronismDefinition placeable in _era.PlaceableObjects)
            {
                if (placeable == definition)
                    return true;
            }

            return false;
        }
    }
}
