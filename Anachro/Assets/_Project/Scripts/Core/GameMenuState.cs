using System;
using UnityEngine;

namespace Anachronism.Core
{
    /// <summary>
    /// Menu Échap ouvert ou non. Le joueur local écoute pour couper ses entrées et libérer la souris,
    /// sans que l'UI ait à connaître le joueur.
    /// </summary>
    public static class GameMenuState
    {
        public static bool IsOpen { get; private set; }

        /// <summary>Un menu Échap existe (Boot chargé). Sinon, dans une scène de test, le joueur libère la souris lui-même.</summary>
        public static bool IsAvailable { get; private set; }

        public static event Action<bool> OpenChanged;

        public static void SetAvailable(bool isAvailable)
        {
            IsAvailable = isAvailable;
            if (!isAvailable)
                SetOpen(false);
        }

        public static void SetOpen(bool isOpen)
        {
            if (IsOpen == isOpen)
                return;

            IsOpen = isOpen;
            OpenChanged?.Invoke(isOpen);
        }

        // Domain reload désactivé dans ce projet : sans ce reset, un menu resté ouvert à la sortie du Play
        // le serait encore au Play suivant.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            IsOpen = false;
            IsAvailable = false;
            OpenChanged = null;
        }
    }
}
