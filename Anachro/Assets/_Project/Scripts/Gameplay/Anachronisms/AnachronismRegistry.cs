using UnityEngine;

namespace Anachronism.Gameplay
{
    [CreateAssetMenu(fileName = "AnachronismRegistry", menuName = "Anachronism/AnachronismRegistry")]
    public class AnachronismRegistry : ScriptableObject
    {
        [SerializeField] private AnachronismDefinition[] _anachronisms;
        public AnachronismDefinition[] Anachronisms => _anachronisms;

        public AnachronismDefinition Get(byte id) => id < _anachronisms.Length ? _anachronisms[id] : null;

        public byte IdOf(AnachronismDefinition definition)
        {
            int index = System.Array.IndexOf(_anachronisms, definition);
            if (index < 0) { Debug.LogError("Anachronisme absent du registre.", this); return byte.MaxValue; }
            return (byte)index;
        }

        public AnachronismDefinition RandomOfSize(AnachronismSize size, EraDefinition era)
        {
            var candidates = new System.Collections.Generic.List<AnachronismDefinition>();
            foreach (AnachronismDefinition definition in _anachronisms)
            {
                if (definition.Size == size && definition.IsAllowedIn(era))
                    candidates.Add(definition);
            }
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }
    }
}
