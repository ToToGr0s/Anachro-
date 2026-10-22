using System.Collections.Generic;
using UnityEngine;

namespace Anachronism.Gameplay
{
    // Apercu de l'objet a poser : le vrai mesh de la definition, vert si PlacementRules l'accepte, rouge sinon.
    // Le fantome est place au centre de la boite de pose (PlacementRules.PositionFor).
    // Visuel seul, aucune regle ici.
    public class PlacementGhost : MonoBehaviour
    {
        // Un modele de fantome par definition, cree une seule fois : Show est appele a chaque frame.
        private class GhostVisual
        {
            public GameObject Root;
            public Renderer[] Renderers;
            public Material[][] ValidSlots;
            public Material[][] InvalidSlots;
            public int AppliedState = -1;   // -1 : rien d'applique, 0 : rouge, 1 : vert
        }

        [SerializeField] private Material _validMaterial;
        [SerializeField] private Material _invalidMaterial;

        private readonly Dictionary<AnachronismDefinition, GhostVisual> _visuals = new Dictionary<AnachronismDefinition, GhostVisual>();
        private GhostVisual _current;
        private bool _isValid;

        private void Awake()
        {
            transform.localScale = Vector3.one;   // les dimensions viennent du modele, plus d'un cube mis a l'echelle

            if (_validMaterial == null || _invalidMaterial == null)
                Debug.LogError("PlacementGhost : materiau vert ou rouge manquant.", this);
        }

        public void Show(AnachronismDefinition definition)
        {
            if (definition == null || definition.Mesh == null)
            {
                Hide();
                return;
            }

            if (!_visuals.TryGetValue(definition, out GhostVisual visual))
            {
                visual = CreateVisual(definition);
                _visuals.Add(definition, visual);
            }

            if (_current == visual)
                return;

            Hide();
            _current = visual;
            _current.Root.SetActive(true);
            ApplyMaterials(_current);
        }

        public void SetValid(bool isValid)
        {
            _isValid = isValid;
            if (_current != null)
                ApplyMaterials(_current);
        }

        public void Hide()
        {
            if (_current == null)
                return;

            _current.Root.SetActive(false);
            _current = null;
        }

        private void ApplyMaterials(GhostVisual visual)
        {
            int state = _isValid ? 1 : 0;
            if (visual.AppliedState == state)
                return;

            Material[][] slots = _isValid ? visual.ValidSlots : visual.InvalidSlots;
            for (int i = 0; i < visual.Renderers.Length; i++)
                visual.Renderers[i].sharedMaterials = slots[i];

            visual.AppliedState = state;
        }

        // Mesh seul, sans collider : le fantome ne doit pas bloquer les rayons de visee.
        private GhostVisual CreateVisual(AnachronismDefinition definition)
        {
            GameObject root = new GameObject(definition.DisplayName + " (fantome)");
            root.SetActive(false);
            root.transform.SetParent(transform, false);

            // Le pivot du mesh n'est pas forcement au centre de sa boite : on le decale pour que la boite soit centree sur le fantome.
            GameObject model = new GameObject("Modele", typeof(MeshFilter), typeof(MeshRenderer));
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = -definition.BoundsCenter;
            model.GetComponent<MeshFilter>().sharedMesh = definition.Mesh;
            model.GetComponent<MeshRenderer>().sharedMaterials = new Material[Mathf.Max(1, definition.Mesh.subMeshCount)];

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            GhostVisual visual = new GhostVisual
            {
                Root = root,
                Renderers = renderers,
                ValidSlots = new Material[renderers.Length][],
                InvalidSlots = new Material[renderers.Length][]
            };

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                int slotCount = Mathf.Max(1, renderers[i].sharedMaterials.Length);
                visual.ValidSlots[i] = FilledArray(_validMaterial, slotCount);
                visual.InvalidSlots[i] = FilledArray(_invalidMaterial, slotCount);
            }

            return visual;
        }

        private static Material[] FilledArray(Material material, int count)
        {
            Material[] slots = new Material[count];
            for (int i = 0; i < count; i++)
                slots[i] = material;

            return slots;
        }
    }
}