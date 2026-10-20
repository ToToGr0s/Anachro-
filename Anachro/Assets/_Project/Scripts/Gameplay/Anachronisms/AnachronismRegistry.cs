using System.Collections.Generic;
using UnityEngine;

namespace Anachronism.Gameplay
{
    [CreateAssetMenu(fileName = "AnachronismRegistry", menuName = "Anachronism/AnachronismRegistry")]
    public class AnachronismRegistry : ScriptableObject
    {
        // Ne jamais reordonner ni supprimer : l'id reseau est l'index (un build different ne serait plus d'accord avec l'hote).
        // Toujours ajouter a la fin.
        [SerializeField] private AnachronismDefinition[] _anachronisms = new AnachronismDefinition[0];

        public IReadOnlyList<AnachronismDefinition> Anachronisms => _anachronisms;

        // Hors limites ou case vide : null. L'id vient d'une RPC client (hostile), l'appelant serveur doit rejeter l'intention.
        public AnachronismDefinition Get(byte id) =>
            _anachronisms != null && id < _anachronisms.Length ? _anachronisms[id] : null;

        public bool TryGetId(AnachronismDefinition definition, out byte id)
        {
            id = 0;
            if (definition == null || _anachronisms == null)
                return false;

            int index = System.Array.IndexOf(_anachronisms, definition);
            if (index < 0 || index > byte.MaxValue)
                return false;

            id = (byte)index;
            return true;
        }
        // Serveur uniquement : tirage de l'hote (UnityEngine.Random). Pas dans une boucle chaude.
        public AnachronismDefinition RandomOfSize(AnachronismSize size, EraDefinition era)
        {
            var candidates = new List<AnachronismDefinition>();
            foreach (AnachronismDefinition definition in _anachronisms)
            {
                if (definition == null)
                    continue;

                if (definition.Size == size && definition.IsAllowedIn(era))
                    candidates.Add(definition);
            }
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        private void OnValidate()
        {
            if (_anachronisms == null)
                return;

            if (_anachronisms.Length > byte.MaxValue + 1)
                Debug.LogError("Registre : 256 anachronismes maximum (l'id tient sur un byte).", this);

            var seen = new HashSet<AnachronismDefinition>();
            for (int i = 0; i < _anachronisms.Length; i++)
            {
                if (_anachronisms[i] == null)
                    Debug.LogError($"Registre : la case {i} est vide.", this);
                else if (!seen.Add(_anachronisms[i]))
                    Debug.LogError($"Registre : doublon a la case {i} ({_anachronisms[i].name}).", this);
            }
        }
    }
}