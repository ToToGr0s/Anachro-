using Anachronism.Network;
using UnityEngine;

namespace Anachronism.Gameplay
{
    // Active l'outil du role local : la pose pour le mechant, le tir pour le chasseur. Jamais les deux.
    public class RoleTools : MonoBehaviour
    {
        [SerializeField] private FugitivePlacer _placer;
        [SerializeField] private HunterTool _hunterTool;
        [Tooltip("Scene de test sans partie (pas de MatchManager) : role utilise a la place.")]
        [SerializeField] private PlayerRole _offlineRole = PlayerRole.Guardian;

        private MatchManager _match;

        // OnEnable/OnDisable plutot que Start/OnDestroy : PlayerNetworkSetup allume RoleTools chez l'owner et l'eteint
        // a la deconnexion. Eteint, il eteint aussi les outils : avec le pool, l'instance peut resservir a un joueur distant.
        private void OnEnable()
        {
            _match = MatchManager.Instance;
            if (_match == null)
            {
                ApplyRole(_offlineRole);
                return;
            }

            _match.LocalRoleAssigned += ApplyRole;
            ApplyRole(_match.LocalRole);
        }

        private void OnDisable()
        {
            if (_match != null)
                _match.LocalRoleAssigned -= ApplyRole;
            _match = null;
            ApplyRole(PlayerRole.None);
        }

        private void ApplyRole(PlayerRole role)
        {
            if (_placer != null)
                _placer.enabled = role == PlayerRole.Villain;
            if (_hunterTool != null)
                _hunterTool.enabled = role == PlayerRole.Guardian;
        }
    }
}