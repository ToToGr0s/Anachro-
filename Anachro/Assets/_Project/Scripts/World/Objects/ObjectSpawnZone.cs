using UnityEngine;

namespace Anachronism.World
{
    /// <summary>
    /// Boîte posée à la main sur la map (sol, table, chaise…) : « des objets peuvent apparaître ici ».
    /// C'est l'ObjectSpawner qui décide quel objet y tient. La taille est réglée ici, pas avec l'échelle du transform.
    /// </summary>
    public sealed class ObjectSpawnZone : MonoBehaviour
    {
        // Largeur et profondeur : la surface utilisable. Hauteur : jusqu'où chercher la surface sous le haut de la boîte.
        [SerializeField] private Vector3 _size = new Vector3(2f, 1f, 2f);

        /// <summary>Surface au sol, pour répartir les objets entre les zones.</summary>
        public float Area => _size.x * _size.z;
        public float Height => _size.y;

        /// <summary>Point au hasard sur le dessus de la boîte, d'où l'on cherche la surface vers le bas.</summary>
        public Vector3 RandomTopPoint()
        {
            var local = new Vector3(Random.Range(-0.5f, 0.5f) * _size.x, _size.y * 0.5f, Random.Range(-0.5f, 0.5f) * _size.z);
            return transform.TransformPoint(local);
        }

        /// <summary>Vrai si l'empreinte de l'objet (rectangle centré, tourné) reste entièrement dans la zone.</summary>
        public bool ContainsFootprint(Vector3 center, Quaternion rotation, Vector2 halfSize)
        {
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + rotation * new Vector3(x * halfSize.x, 0f, z * halfSize.y);
                    Vector3 local = transform.InverseTransformPoint(corner);
                    if (Mathf.Abs(local.x) > _size.x * 0.5f || Mathf.Abs(local.z) > _size.z * 0.5f)
                        return false;
                }
            }
            return true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.25f);
            Gizmos.DrawCube(Vector3.zero, _size);
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 1f);
            Gizmos.DrawWireCube(Vector3.zero, _size);
        }
    }
}
