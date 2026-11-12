using System.Collections.Generic;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Demandes de clients refusées par l'hôte (tir, pose…). Un client modifié peut en envoyer des centaines par seconde :
    /// la console n'en reçoit qu'une par seconde et par client, le reste est seulement compté. Les dernières restent
    /// lisibles dans le panel de debug (F5).
    /// </summary>
    public static class RefusedRequests
    {
        public readonly struct Entry
        {
            public readonly float Time;
            public readonly string Source;
            public readonly int ClientId;
            public readonly string Reason;

            public Entry(float time, string source, int clientId, string reason)
            {
                Time = time;
                Source = source;
                ClientId = clientId;
                Reason = reason;
            }
        }

        private const int MaxEntries = 30;
        private const float LogInterval = 1f;

        private static readonly Queue<Entry> Recent = new Queue<Entry>();
        private static readonly Dictionary<(string, int), float> NextLogTime = new Dictionary<(string, int), float>();
        private static readonly Dictionary<(string, int), int> Muted = new Dictionary<(string, int), int>();

        public static IEnumerable<Entry> Entries => Recent;
        public static int TotalCount { get; private set; }

        // Domain reload désactivé dans ce projet : sans ce reset, les refus d'un Play resteraient au suivant.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Recent.Clear();
            NextLogTime.Clear();
            Muted.Clear();
            TotalCount = 0;
        }

        public static void Report(string source, int clientId, string reason, Object context)
        {
            float now = UnityEngine.Time.unscaledTime;
            TotalCount++;
            Recent.Enqueue(new Entry(now, source, clientId, reason));
            while (Recent.Count > MaxEntries)
                Recent.Dequeue();

            var key = (source, clientId);
            if (NextLogTime.TryGetValue(key, out float next) && now < next)
            {
                Muted[key] = Muted.TryGetValue(key, out int count) ? count + 1 : 1;
                return;
            }

            int muted = Muted.TryGetValue(key, out int skipped) ? skipped : 0;
            Muted[key] = 0;
            NextLogTime[key] = now + LogInterval;
            string extra = muted > 0 ? $" (+{muted} refus non affichés)" : string.Empty;
            Debug.LogWarning($"[{source}] Refusé (client {clientId}) : {reason}{extra}.", context);
        }
    }
}
