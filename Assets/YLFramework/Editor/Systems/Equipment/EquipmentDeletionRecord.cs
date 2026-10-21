using System;
using UnityEngine;

namespace YLFramework.EquipmentSystem
{
    internal sealed class EquipmentDeletionRecord : ScriptableObject
    {
        public EquipmentsConfig root;
        public string path, guid, memory;
        public byte[] assetBytes, metaBytes;
        public int[] indices;
        public bool deleted, wasDirty;
        [NonSerialized] public bool applied;
    }
}
