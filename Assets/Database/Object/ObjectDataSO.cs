using System.Collections.Generic;
using UnityEngine;
using Cycle33.Core;

namespace Cycle33.Data
{
    [CreateAssetMenu(fileName = "ObjectData", menuName = "Cycle33/ObjectData")]
    public class ObjectDataSO : ScriptableObject
    {
        public ObjectType objectType;
        public Vector2Int size = Vector2Int.one;
        public int lightningWeight = 0;
        public bool blocksMovement = true;
        public bool blocksGrass = true;
        public Vector2Int caveEntryOffset = new Vector2Int(1, 0);

        [Header("Default Prefab")]
        public GameObject prefab;

        [Header("Grass Cycle Variants (Cycle 1 = Elemen 0, Cycle 2 = Elemen 1, dst.)")]
        public List<GameObject> cycleVariantPrefabs = new List<GameObject>();

        // Mengambil prefab berdasarkan nomor Cycle saat ini (berputar otomatis jika cycle > jumlah variasi)
        public GameObject GetPrefabForCycle(int currentCycle)
        {
            if (cycleVariantPrefabs != null && cycleVariantPrefabs.Count > 0)
            {
                int index = Mathf.Max(0, currentCycle - 1) % cycleVariantPrefabs.Count;
                if (cycleVariantPrefabs[index] != null)
                {
                    return cycleVariantPrefabs[index];
                }
            }
            return prefab;
        }
    }
}