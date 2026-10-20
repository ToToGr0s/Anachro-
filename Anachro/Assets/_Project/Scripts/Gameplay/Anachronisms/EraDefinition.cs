using UnityEngine;

namespace Anachronism.Gameplay
{
    // TODO (Celian) : ajouter `[SerializeField] private NpcSkinTable _npcs;` (+ using Anachronism.Npc) une fois Feat-online-player-pnj mergee dans Dev.
    [CreateAssetMenu(fileName = "EraDefinition", menuName = "Anachronism/EraDefinition")]
    public class EraDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private GameObject[] _eraProps;
        [SerializeField] private AudioClip _music;
        [SerializeField] private string _mapScene;
        public string DisplayName => _displayName;
        public GameObject[] EraProps => _eraProps;
        public AudioClip Music => _music;
        public string MapScene => _mapScene;
    }
}
