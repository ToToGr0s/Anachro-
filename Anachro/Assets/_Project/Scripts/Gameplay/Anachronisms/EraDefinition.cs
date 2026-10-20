using Anachronism.Npc;
using System.Collections.Generic;
using UnityEngine;

namespace Anachronism.Gameplay
{
    [CreateAssetMenu(fileName = "EraDefinition", menuName = "Anachronism/EraDefinition")]
    public class EraDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private NpcSkinTable _npcs;
        [SerializeField] private GameObject[] _eraProps;
        [SerializeField] private AudioClip _music;
        [SerializeField] private string _mapScene;
        public string DisplayName => _displayName;
        public NpcSkinTable Npcs => _npcs;
        public IReadOnlyList<GameObject> EraProps => _eraProps;
        public AudioClip Music => _music;
        public string MapScene => _mapScene;
    }
}
