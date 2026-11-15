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
        public GameObject prefab;
    }
}