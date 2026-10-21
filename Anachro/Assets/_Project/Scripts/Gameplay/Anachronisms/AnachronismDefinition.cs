using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

namespace Anachronism.Gameplay
{
    [CreateAssetMenu(fileName = "AnachronismDefinition", menuName = "Anachronism/AnachronismDefinition")]
    public class AnachronismDefinition : ScriptableObject
    {
        [SerializeField] private AnachronismSize _size;
        [SerializeField] private NetworkObject _prefab;
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;
        [Tooltip("% de la barre de degradation retire quand l'objet est trouve (0-100)")]
        [SerializeField][Range(0, 100)] private float _disruptionPercent;
        [SerializeField] private EraDefinition[] _allowedEras;
        [Tooltip("Surfaces ou l'objet peut etre pose : Floor, Wall, ou les deux.")]
        [SerializeField] private PlacementSurfaceType _allowedSurfaces = PlacementSurfaceType.Floor | PlacementSurfaceType.Wall;
        [Tooltip("Boite englobante en metres (largeur X, hauteur Y, profondeur Z). Le pivot du prefab doit etre au centre de cette boite.")]
        [SerializeField] private Vector3 _dimensions = new Vector3(0.3f, 0.3f, 0.3f);

        public AnachronismSize Size => _size;
        public NetworkObject Prefab => _prefab;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public float DisruptionPercent => _disruptionPercent;
        public IReadOnlyList<EraDefinition> AllowedEras => _allowedEras;
        public PlacementSurfaceType AllowedSurfaces => _allowedSurfaces;
        public Vector3 Dimensions => _dimensions;

        public bool IsAllowedIn(EraDefinition era)
        {
            if (era == null)
                return false;   // pas d'epoque connue : on ne devine pas

            if (_allowedEras == null || _allowedEras.Length == 0)
                return true;   // liste vide = permis dans toutes les epoques

            return System.Array.IndexOf(_allowedEras, era) >= 0;
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_prefab == null)
                Debug.LogWarning($"{name} : prefab manquant, a assigner avant la pose en ligne.", this);

            WarnIfDimensionsDifferFromPrefab();

            if (_allowedSurfaces == PlacementSurfaceType.None)
                Debug.LogWarning($"{name} : aucune surface permise, l'objet ne pourra jamais etre pose.", this);

            if (_dimensions.x <= 0f || _dimensions.y <= 0f || _dimensions.z <= 0f)
                Debug.LogWarning($"{name} : dimensions a zero ou negatives, la pose ne marchera pas.", this);
        }

        // Dimensions saisies a la main : on verifie qu'elles collent au modele du prefab, sinon centre, appui et
        // chevauchement sont calcules sur une mauvaise boite (objets qui fusionnent ou traversent les meubles).
        private void WarnIfDimensionsDifferFromPrefab()
        {
            if (_prefab == null)
                return;

            if (_prefab.GetComponent<Renderer>() != null)
                Debug.LogWarning($"{name} : un Renderer sur la racine du prefab n'apparait pas dans le fantome ; mets le visuel dans un enfant.", this);

            bool hasBounds = false;
            Bounds bounds = default;
            Matrix4x4 toRoot = _prefab.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in _prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;

                Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                Bounds mesh = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 sign = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                    Vector3 point = matrix.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents, sign));
                    if (!hasBounds)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            if (!hasBounds)
                return;

            if (Vector3.Distance(bounds.size, _dimensions) > 0.05f)
                Debug.LogWarning($"{name} : Dimensions {_dimensions} differentes de la taille du modele {bounds.size}.", this);

            if (bounds.center.magnitude > 0.05f)
                Debug.LogWarning($"{name} : le pivot du prefab n'est pas au centre du modele (decalage {bounds.center}).", this);
        }
#endif
    }
}
