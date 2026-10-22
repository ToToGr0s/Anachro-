using UnityEngine;
#if UNITY_EDITOR

namespace Anachronism.Gameplay
{
    // TEST LOCAL SEULEMENT (scenes Sandbox) : fait apparaitre l'objet pose, comme le fera le serveur en ligne.
    // En ligne, c'est NetworkPlacement (Theo) qui valide puis spawne : pas sur un prefab joueur.
    public class SandboxPlacementSpawner : MonoBehaviour
    {
        [SerializeField] private FugitivePlacer _placer;
        [SerializeField] private GameplayTuning _tuning;

        private void Awake()
        {
            if (_placer == null || _tuning == null)
            {
                Debug.LogError("SandboxPlacementSpawner : placer ou reglages manquant.", this);
                enabled = false;
            }
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
            if (definition == null || definition.Mesh == null)
            {
                Debug.LogWarning("SandboxPlacementSpawner : pas de mesh a faire apparaitre.", this);
                return;
            }

            // WorldObject porte un NetworkObject : l'instancier hors reseau fait lever une erreur FishNet.
            // On recree donc son visuel et sa boite sous un objet vide, place comme le serveur le placera.
            GameObject placed = new GameObject(definition.DisplayName + " (test)", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            Quaternion rotation = PlacementRules.RotationFor(request.Normal, _tuning);
            // PositionFor donne le centre de la boite ; le pivot du mesh en est decale de BoundsCenter.
            placed.transform.SetPositionAndRotation(
                PlacementRules.PositionFor(request.Position, request.Normal, definition.Dimensions, _tuning) - rotation * definition.BoundsCenter,
                rotation);
            placed.GetComponent<MeshFilter>().sharedMesh = definition.Mesh;
            placed.GetComponent<MeshRenderer>().sharedMaterial = definition.Material;
            BoxCollider box = placed.GetComponent<BoxCollider>();
            box.center = definition.BoundsCenter;
            box.size = definition.Dimensions;
            // Meme calque que WorldObject : la pose suivante le traite comme un objet deja pose.
            int placedMask = _tuning.PlacedObjectsMask.value;
            if (placedMask != 0)
                placed.layer = (int)Mathf.Log(placedMask & -placedMask, 2);
        }
    }
}
#endif