using Anachronism.World;
using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR

namespace Anachronism.Gameplay
{
    // TEST LOCAL SEULEMENT (scenes Sandbox) : fait apparaitre l'objet pose, comme le fera le serveur en ligne.
    // En ligne, c'est NetworkPlacement (Theo) qui valide puis spawne : ce composant ne fait alors plus rien.
    public class SandboxPlacementSpawner : MonoBehaviour
    {
        [SerializeField] private FugitivePlacer _placer;
        [SerializeField] private GameplayTuning _tuning;
        [SerializeField] private WorldObject _worldObjectPrefab;

        private Transform _inactiveParent;
        private NetworkObject _networkObject;

        private void Awake()
        {
            if (_placer == null || _tuning == null || _worldObjectPrefab == null)
            {
                Debug.LogError("SandboxPlacementSpawner : placer, reglages ou prefab WorldObject manquant.", this);
                enabled = false;
                return;
            }

            _networkObject = GetComponentInParent<NetworkObject>();

            // Parent inactif : une copie du prefab y reste sans Awake tant qu'on n'a pas retire ses composants reseau.
            _inactiveParent = new GameObject("Parent inactif (copies de test)").transform;
            _inactiveParent.SetParent(transform, false);
            _inactiveParent.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _placer.ItemPlaced += OnItemPlaced;
        }

        private void OnDisable()
        {
            _placer.ItemPlaced -= OnItemPlaced;
        }

        private void OnItemPlaced(PlacementRequest request, AnachronismDefinition definition)
        {
            // Joueur spawne en reseau : l'objet viendra de l'hote, une copie locale le doublerait.
            if (_networkObject != null && _networkObject.IsSpawned)
                return;

            if (definition == null || definition.Mesh == null)
            {
                Debug.LogWarning("SandboxPlacementSpawner : pas de mesh a faire apparaitre.", this);
                return;
            }

            // Meme prefab que le jeu (WorldObject) : memes composants, meme calque, meme obstacle de NavMesh.
            // Il porte un NetworkObject et un NetworkBehaviour qui levent une erreur FishNet hors reseau : on instancie donc
            // sous un parent inactif (aucun Awake), on retire ces deux composants, puis on sort la copie du parent.
            WorldObject copy = Instantiate(_worldObjectPrefab, _inactiveParent);
            GameObject placed = copy.gameObject;
            // Tous les NetworkBehaviour (pas seulement WorldObject) : un composant reseau ajoute plus tard au prefab ne casse pas le test.
            foreach (NetworkBehaviour networkBehaviour in placed.GetComponentsInChildren<NetworkBehaviour>(true))
                DestroyImmediate(networkBehaviour);
            DestroyImmediate(placed.GetComponent<NetworkObject>());
            placed.name = definition.DisplayName + " (test)";

            Quaternion rotation = PlacementRules.RotationFor(request.Normal, _tuning);
            // PositionFor donne le centre de la boite ; le pivot du mesh en est decale de BoundsCenter.
            placed.transform.SetParent(null, false);
            placed.transform.SetPositionAndRotation(
                PlacementRules.PositionFor(request.Position, request.Normal, definition.Dimensions, _tuning) - rotation * definition.BoundsCenter,
                rotation);

            WorldObject.ApplyDefinition(definition, placed.GetComponent<MeshFilter>(), placed.GetComponent<MeshRenderer>(),
                placed.GetComponent<BoxCollider>(), placed.GetComponent<NavMeshObstacle>(), this);
        }
    }
}
#endif