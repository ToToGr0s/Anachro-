using UnityEngine;

namespace Anachronism.Npc
{
    /// <summary>
    /// Apparences tirables pour les PNJ et les joueurs : seul l'index est envoyé sur le réseau,
    /// cette table doit donc être la même partout.
    /// </summary>
    [CreateAssetMenu(menuName = "Anachronism/Npc Skin Table", fileName = "NpcSkins")]
    public class NpcSkinTable : ScriptableObject
    {
        [Tooltip("Seuls le modèle, l'Animator, l'échelle et le paramètre de vitesse de chaque profil sont utilisés.")]
        [SerializeField] private NpcProfile[] _skins = new NpcProfile[0];

        public int Count => _skins.Length;

        public NpcProfile Get(int index) =>
            index >= 0 && index < _skins.Length ? _skins[index] : null;

        public int RandomIndex() => _skins.Length == 0 ? 0 : Random.Range(0, _skins.Length);
    }
}
