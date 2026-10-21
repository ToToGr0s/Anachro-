using UnityEngine;
#if UNITY_EDITOR || DEBUG

namespace Anachronism.Gameplay
{
    // TEST LOCAL SEULEMENT (scenes Sandbox) : fait apparaitre l'objet pose, comme le fera le serveur en ligne.
    // En ligne, c'est NetworkPlacement (Theo) qui valide puis spawne : pas sur un prefab joueur.
    public class SandboxPlacementSpawner : MonoBehaviour
    {
        [SerializeField] private FugitivePlacer _placer;
        [SerializeField] private GameplayTuning _tuning;

        private void OnEnable()
        {
            _placer.PlacementRequested += OnPlacementRequested;
        }

        private void OnDisable()
        {
            _placer.PlacementRequested -= OnPlacementRequested;
        }

        private void OnPlacementRequested(PlacementRequest request, AnachronismDefinition definition)
        {
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogWarning("SandboxPlacementSpawner : pas de prefab a faire apparaitre.", this);
                return;
            }

            // Le prefab porte un NetworkObject : l'instancier hors reseau fait lever une erreur FishNet.
            // On recopie donc seulement ses enfants (le visuel) sous un objet vide place comme le prefab le serait.
            GameObject placed = new GameObject(definition.Prefab.name + " (test)");
            placed.transform.SetPositionAndRotation(
                PlacementRules.PositionFor(request.Position, request.Normal, definition.Dimensions, _tuning),
                PlacementRules.RotationFor(request.Normal, _tuning));
            foreach (Transform child in definition.Prefab.transform)
                Instantiate(child.gameObject, placed.transform, false);
        }
    }
}
#endif