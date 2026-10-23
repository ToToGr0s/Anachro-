using System.Collections.Generic;
using Anachronism.Gameplay;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Derniers tirs résolus par l'hôte (rayon refait par lui, ce qu'il a touché), pour le panel F5 : affichage des
    /// rayons dans le monde et liste des résultats. Côté hôte seulement, jamais envoyé aux clients.
    /// </summary>
    public static class ShotDebugLog
    {
        public readonly struct Shot
        {
            public readonly float Time;
            public readonly int ClientId;
            public readonly Vector3 Origin;
            public readonly Vector3 Point;
            public readonly bool HasHit;
            public readonly ShotResult Result;

            public Shot(float time, int clientId, Vector3 origin, Vector3 point, bool hasHit, ShotResult result)
            {
                Time = time;
                ClientId = clientId;
                Origin = origin;
                Point = point;
                HasHit = hasHit;
                Result = result;
            }
        }

        private const int MaxShots = 20;
        private static readonly Queue<Shot> Recent = new Queue<Shot>();

        public static IEnumerable<Shot> Shots => Recent;

        // Domain reload désactivé dans ce projet : les tirs d'un Play ne doivent pas rester au suivant.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Recent.Clear();

        public static void Record(int clientId, Vector3 origin, Vector3 point, bool hasHit, ShotResult result)
        {
            Recent.Enqueue(new Shot(UnityEngine.Time.unscaledTime, clientId, origin, point, hasHit, result));
            while (Recent.Count > MaxShots)
                Recent.Dequeue();
        }
    }
}
