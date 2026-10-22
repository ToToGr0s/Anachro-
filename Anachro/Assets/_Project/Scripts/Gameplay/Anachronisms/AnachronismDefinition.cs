using UnityEngine;

namespace Anachronism.Gameplay
{
    /// <summary>
    /// Un objet du jeu, qu'il apparaisse avec la map ou qu'un fugitif le pose : c'est l'époque de la map qui décide
    /// s'il est normal ou anachronique (EraRules). Tous les objets partagent le même prefab réseau (WorldObject),
    /// qui prend le mesh et le matériau d'ici : ajouter un objet = créer cet asset et l'ajouter au registre.
    /// </summary>
    [CreateAssetMenu(fileName = "AnachronismDefinition", menuName = "Anachronism/AnachronismDefinition")]
    public class AnachronismDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        // Époque à laquelle l'objet appartient (celle de son onglet dans le tableau des objets).
        [SerializeField] private Era _era;
        // Donne le % de barre et la recharge de pose (GameplayTuning), pas la taille affichée.
        [SerializeField] private AnachronismSize _size;
        [SerializeField] private Sprite _icon;
        [SerializeField] private Mesh _mesh;
        // Le mesh est importé à sa taille réelle : aucune échelle à régler ici.
        [SerializeField] private Material _material;
        [Tooltip("Surfaces ou l'objet peut etre pose : Floor, Wall, ou les deux.")]
        [SerializeField] private PlacementSurfaceType _allowedSurfaces = PlacementSurfaceType.Floor | PlacementSurfaceType.Wall;

        public string DisplayName => _displayName;
        public Era Era => _era;
        public AnachronismSize Size => _size;
        public Sprite Icon => _icon;
        public Mesh Mesh => _mesh;
        public Material Material => _material;
        public PlacementSurfaceType AllowedSurfaces => _allowedSurfaces;
        // Boîte de pose = boîte du mesh : plus de dimensions saisies à la main qui divergent du modèle.
        public Vector3 Dimensions => _mesh != null ? _mesh.bounds.size : Vector3.zero;
        // Le pivot du mesh n'est pas forcément au centre : décalage du centre de la boîte dans le repère de l'objet.
        public Vector3 BoundsCenter => _mesh != null ? _mesh.bounds.center : Vector3.zero;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_mesh == null)
                Debug.LogWarning($"{name} : mesh manquant, l'objet ne pourra ni apparaitre ni etre pose.", this);

            if (_allowedSurfaces == PlacementSurfaceType.None)
                Debug.LogWarning($"{name} : aucune surface permise, l'objet ne pourra jamais etre pose.", this);
        }
#endif
    }
}
