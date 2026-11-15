using System.Collections.Generic;
using UnityEngine;
using Cycle33.Core;
using Cycle33.Data;

namespace Cycle33.Systems
{
    public class GridCell
    {
        public Vector2Int Coords;
        public bool BlocksMovement;
        public bool BlocksGrass;
        public bool IsCaveEntry;
        public ObjectType? StaticObject;
        public GrassInstance Grass;
    }

    public class GrassInstance
    {
        public Vector2Int Coords;
        public bool IsParasite;
        public bool IsReserved; // Dipakai sementara di P3 sebelum TargetRegistry di P4
        public GameObject VisualGO;
    }

    public class GridService : MonoBehaviour
    {
        public static GridService Instance { get; private set; }

        private GridCell[,] _grid;
        private Vector2Int _gridSize;
        public Vector2Int? CaveEntryCell { get; private set; }
        public List<Vector2Int> PondEdgeCells { get; private set; } = new List<Vector2Int>();
        public Vector3 PondCenterWorld { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void InitializeGrid(GlobalRulesSO rules, StageConfigSO stage, List<ObjectDataSO> objectDatabase)
        {
            stage.ValidateCalendar(); // CFG-01

            Dictionary<ObjectType, ObjectDataSO> objectDb = new Dictionary<ObjectType, ObjectDataSO>();
            foreach (var obj in objectDatabase)
            {
                if (obj != null) objectDb[obj.objectType] = obj;
            }

            _gridSize = rules.gridSize;
            _grid = new GridCell[_gridSize.x, _gridSize.y];

            for (int x = 0; x < _gridSize.x; x++)
            {
                for (int y = 0; y < _gridSize.y; y++)
                {
                    _grid[x, y] = new GridCell { Coords = new Vector2Int(x, y) };
                }
            }

            CaveEntryCell = null;
            PondEdgeCells.Clear();

            foreach (var placement in stage.staticObjects)
            {
                if (placement.objectType == ObjectType.Cave && !stage.hasCave) continue;
                if (!objectDb.TryGetValue(placement.objectType, out ObjectDataSO data)) continue;

                for (int dx = 0; dx < data.size.x; dx++)
                {
                    for (int dy = 0; dy < data.size.y; dy++)
                    {
                        Vector2Int pos = placement.bottomLeftGridPos + new Vector2Int(dx, dy);
                        if (!IsInBounds(pos)) continue;

                        GridCell cell = _grid[pos.x, pos.y];
                        cell.StaticObject = placement.objectType;
                        cell.BlocksMovement = data.blocksMovement;
                        cell.BlocksGrass = data.blocksGrass;
                    }
                }

                if (data.prefab != null)
                {
                    Vector3 spawnPos = GridToWorld(placement.bottomLeftGridPos);
                    Instantiate(data.prefab, spawnPos, Quaternion.identity, transform);
                }

                if (placement.objectType == ObjectType.Cave)
                {
                    Vector2Int entry = placement.bottomLeftGridPos + data.caveEntryOffset;
                    if (IsInBounds(entry))
                    {
                        _grid[entry.x, entry.y].BlocksMovement = false;
                        _grid[entry.x, entry.y].IsCaveEntry = true;
                        CaveEntryCell = entry;
                    }
                }
                else if (placement.objectType == ObjectType.Pond)
                {
                    PondCenterWorld = GridToWorld(placement.bottomLeftGridPos) + new Vector3(data.size.x * 0.5f, 0f, data.size.y * 0.5f);
                    CachePondEdges(placement.bottomLeftGridPos, data.size);
                }
            }
        }

        private void CachePondEdges(Vector2Int bottomLeft, Vector2Int size)
        {
            for (int x = bottomLeft.x - 1; x <= bottomLeft.x + size.x; x++)
            {
                for (int y = bottomLeft.y - 1; y <= bottomLeft.y + size.y; y++)
                {
                    Vector2Int p = new Vector2Int(x, y);
                    if (IsInBounds(p) && !_grid[p.x, p.y].BlocksMovement)
                    {
                        PondEdgeCells.Add(p);
                    }
                }
            }
        }

        public bool IsInBounds(Vector2Int pos) => pos.x >= 0 && pos.x < _gridSize.x && pos.y >= 0 && pos.y < _gridSize.y;
        public bool IsWalkable(Vector2Int pos) => IsInBounds(pos) && !_grid[pos.x, pos.y].BlocksMovement;
        public GridCell GetCell(Vector2Int pos) => IsInBounds(pos) ? _grid[pos.x, pos.y] : null;

        public Vector3 GridToWorld(Vector2Int gridPos) => new Vector3(gridPos.x + 0.5f, 0f, gridPos.y + 0.5f);
        public Vector2Int WorldToGrid(Vector3 worldPos) => new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.z));

        public List<GridCell> GetEmptyGrassCells()
        {
            List<GridCell> list = new List<GridCell>();
            if (_grid == null) return list;

            foreach (var cell in _grid)
            {
                if (!cell.BlocksGrass && cell.Grass == null) list.Add(cell);
            }
            return list;
        }

        public int GetTotalGrassCapableCells()
        {
            int count = 0;
            if (_grid == null) return 0;

            foreach (var cell in _grid)
            {
                if (!cell.BlocksGrass) count++;
            }
            return count;
        }

        // A* Pathfinding pada Grid
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int target)
        {
            if (!IsWalkable(target)) return null;
            if (start == target) return new List<Vector2Int> { target };

            List<Vector2Int> openSet = new List<Vector2Int> { start };
            Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            Dictionary<Vector2Int, float> gScore = new Dictionary<Vector2Int, float> { [start] = 0f };
            Dictionary<Vector2Int, float> fScore = new Dictionary<Vector2Int, float> { [start] = Heuristic(start, target) };

            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            while (openSet.Count > 0)
            {
                openSet.Sort((a, b) => fScore[a].CompareTo(fScore[b]));
                Vector2Int current = openSet[0];

                if (current == target) return ReconstructPath(cameFrom, current);

                openSet.RemoveAt(0);

                foreach (var d in dirs)
                {
                    Vector2Int neighbor = current + d;
                    if (!IsWalkable(neighbor)) continue;

                    float tentativeG = gScore[current] + 1f;
                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Heuristic(neighbor, target);
                        if (!openSet.Contains(neighbor)) openSet.Add(neighbor);
                    }
                }
            }
            return null;
        }

        private float Heuristic(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
        {
            List<Vector2Int> path = new List<Vector2Int> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }
            path.Reverse();
            return path;
        }
    }
}