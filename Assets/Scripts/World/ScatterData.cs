using System;
using UnityEngine;

namespace AshenRealm.World
{
    /// <summary>
    /// The valley's trees, grass, flowers and wheat as instances instead of merged geometry: a few variant meshes and a
    /// position, yaw and scale for each copy, grouped by 66 m cell and kind so whole groups are culled at once.
    /// Written by the valley generator.
    /// </summary>
    public sealed class ScatterData : ScriptableObject
    {
        [Serializable]
        public sealed class Kind
        {
            public string Name;
            /// <summary>A tree is a trunk on the world material and boughs on the foliage material; cards are one mesh.</summary>
            public Mesh[] Meshes;
            public Material[] Materials;
            public float CullDistance;
            public bool Shadows;
        }

        [Serializable]
        public struct Group
        {
            public int Kind;
            public Bounds Bounds;
            public int Start;
            public int Count;
        }

        [Serializable]
        public struct Instance
        {
            public Vector3 Position;
            public float Yaw;
            public float Scale;
        }

        public Kind[] Kinds = new Kind[0];
        public Group[] Groups = new Group[0];
        public Instance[] Instances = new Instance[0];
    }
}
