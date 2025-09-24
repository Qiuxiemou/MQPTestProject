using UnityEngine;
using System.Collections.Generic;

namespace Unity.FPS.Game
{
    [System.Serializable]
    public class WorldState
    {
        public float Time; // Game time of snapshot
        public List<EntityState> Entities = new List<EntityState>();
    }

    [System.Serializable]
    public class EntityState
    {
        public string Id;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Health;
    }
}
