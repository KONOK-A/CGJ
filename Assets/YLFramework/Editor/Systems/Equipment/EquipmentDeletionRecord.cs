using System;
using UnityEngine;

namespace YLFramework.EquipmentSystem
{
    internal sealed class EquipmentDeletionRecord : ScriptableObject
    {
        public EquipmentsConfig root;
        public string path;
        public string guid;
        public string memory;
        public byte[] assetBytes;
        public byte[] metaBytes;
        public int[] indices;
        public bool deleted;
        public bool wasDirty;
        [NonSerialized] public bool IsApplied;
    }
}
