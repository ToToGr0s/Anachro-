using System.Collections.Generic;
using UnityEngine;

namespace Anachronism.Npc
{
    internal sealed class NpcGroup
    {
        private readonly List<NpcAgent> _members = new();

        public NpcGroup(NpcAgent leader, Vector3 commonPoint, bool willDiscuss)
        {
            Leader = leader;
            CommonPoint = commonPoint;
            WillDiscuss = willDiscuss;
            _members.Add(leader);
        }

        public NpcAgent Leader { get; }
        public Vector3 CommonPoint { get; }
        public bool WillDiscuss { get; }
        public int Count => _members.Count;
        public IReadOnlyList<NpcAgent> Members => _members;
        public bool IsValid => Leader != null && _members.Count >= 2 && _members[0] == Leader;

        public void Add(NpcAgent member) => _members.Add(member);

        public void Remove(NpcAgent member) => _members.Remove(member);
    }
}
