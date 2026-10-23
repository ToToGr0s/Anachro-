using System;
using UnityEngine;

namespace Anachronism.Core
{
    /// <summary>
    /// Menu Échap ouvert ou non, et écran de fin affiché. Le joueur local écoute pour couper ses entrées et libérer la
    /// souris, sans que l'UI ait à connaître le joueur.
    /// </summary>
    public static class GameMenuState
    {
        public static bool IsOpen { get; private set; }

        /// <summary>Fin de partie : les joueurs attendent au bureau, la souris sert au menu du lobby (Rejouer, Quitter).</summary>
        public static bool IsEndScreen { get; private set; }

        /// <summary>Entrées du joueur coupées et souris libre : menu Échap ou écran de fin.</summary>
        public static bool IsInputBlocked => IsOpen || IsEndScreen;

        /// <summary>Un menu Échap existe (Boot chargé). Sinon, dans une scène de test, le joueur libère la souris lui-même.</summary>
        public static bool IsAvailable { get; private set; }

        public static event Action<bool> OpenChanged;
        public static event Action<bool> InputBlockedChanged;

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

            bool wasBlocked = IsInputBlocked;
            IsOpen = isOpen;
            OpenChanged?.Invoke(isOpen);
            if (IsInputBlocked != wasBlocked)
                InputBlockedChanged?.Invoke(IsInputBlocked);
        }

        public static void SetEndScreen(bool isEndScreen)
        {
            if (IsEndScreen == isEndScreen)
                return;

            bool wasBlocked = IsInputBlocked;
            IsEndScreen = isEndScreen;
            if (IsInputBlocked != wasBlocked)
                InputBlockedChanged?.Invoke(IsInputBlocked);
        }

        // Domain reload désactivé dans ce projet : sans ce reset, un menu resté ouvert à la sortie du Play
        // le serait encore au Play suivant.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            IsOpen = false;
            IsEndScreen = false;
            IsAvailable = false;
            OpenChanged = null;
            InputBlockedChanged = null;
        }
    }
}
