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

        public string DisplayName => _displayName;
        public Era Era => _era;
        public AnachronismSize Size => _size;
        public Sprite Icon => _icon;
        public Mesh Mesh => _mesh;
        public Material Material => _material;
    }
}
