using UnityEngine;
using Cycle33.Data;

namespace Cycle33.Systems
{
    public class GridVisualizer : MonoBehaviour
    {
        public static GridVisualizer Instance { get; private set; }

        [Header("Tile Colors")]
        public Color walkableColor = new Color(0.35f, 0.65f, 0.28f, 1f);
        public Color walkableAltColor = new Color(0.31f, 0.60f, 0.25f, 1f); // Pola papan catur
        public Color blockedColor = new Color(0.45f, 0.28f, 0.25f, 1f);
        public Color caveEntryColor = new Color(0.25f, 0.55f, 0.75f, 1f);

        [Header("Hover Highlight Colors")]
        public Color hoverValidBuyColor = new Color(0.2f, 1f, 0.2f, 1f);   // Hijau terang saat mau taruh hewan di sel valid
        public Color hoverInvalidBuyColor = new Color(1f, 0.15f, 0.15f, 1f); // Merah terang saat kursor di sel terhalang
        public Color hoverInspectColor = new Color(0.95f, 0.9f, 0.3f, 1f);   // Kuning saat mode klik biasa

        [Range(0.75f, 0.98f)]
        public float tileFillRatio = 0.92f; // Menyisakan celah garis antar tile

        private Renderer[,] _tileRenderers;
        private Vector2Int _gridSize;
        private Material _sharedBaseMaterial;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void GenerateVisualGrid(GlobalRulesSO rules)
        {
            _gridSize = rules.gridSize;
            _tileRenderers = new Renderer[_gridSize.x, _gridSize.y];

            // Buat 1 material dasar yang kompatibel dengan URP (Unity 6) maupun Built-in RP
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _sharedBaseMaterial = new Material(shader);

            GameObject container = new GameObject("GridTiles_VisualContainer");
            container.transform.SetParent(transform);

            for (int x = 0; x < _gridSize.x; x++)
            {
                for (int y = 0; y < _gridSize.y; y++)
                {
                    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = $"Tile_{x}_{y}";
                    quad.transform.SetParent(container.transform);

                    // Posisikan sedikit di atas y = 0, menghadap ke atas
                    quad.transform.position = new Vector3(x + 0.5f, 0.01f, y + 0.5f);
                    quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    quad.transform.localScale = new Vector3(tileFillRatio, tileFillRatio, 1f);

                    // Hapus collider agar tidak mengganggu pergerakan hewan
                    Collider col = quad.GetComponent<Collider>();
                    if (col != null) Destroy(col);

                    Renderer rend = quad.GetComponent<Renderer>();
                    rend.material = new Material(_sharedBaseMaterial);
                    _tileRenderers[x, y] = rend;
                }
            }

            RefreshAllTileColors();
        }

        private void Update()
        {
            if (_tileRenderers == null) return;
            RefreshAllTileColors();
        }

        public void RefreshAllTileColors()
        {
            Vector2Int? hovered = PlayerPlacementController.Instance != null
                ? PlayerPlacementController.Instance.HoveredGridCell
                : null;

            bool isBuyingAnimal = PlayerPlacementController.Instance != null &&
                                  PlayerPlacementController.Instance.SelectedSpeciesForPlacement.HasValue;

            for (int x = 0; x < _gridSize.x; x++)
            {
                for (int y = 0; y < _gridSize.y; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    GridCell cell = GridService.Instance.GetCell(pos);
                    if (cell == null) continue;

                    Color targetColor;

                    // Jika tile ini sedang ditunjuk oleh mouse
                    if (hovered.HasValue && hovered.Value == pos)
                    {
                        if (isBuyingAnimal)
                        {
                            targetColor = !cell.BlocksMovement ? hoverValidBuyColor : hoverInvalidBuyColor;
                        }
                        else
                        {
                            targetColor = hoverInspectColor;
                        }
                    }
                    else
                    {
                        // Warna dasar tile berdasarkan status keterlewatan (walkable vs blocked)
                        if (cell.IsCaveEntry)
                        {
                            targetColor = caveEntryColor;
                        }
                        else if (cell.BlocksMovement)
                        {
                            targetColor = blockedColor;
                        }
                        else
                        {
                            bool isEven = (x + y) % 2 == 0;
                            targetColor = isEven ? walkableColor : walkableAltColor;
                        }
                    }

                    Renderer r = _tileRenderers[x, y];
                    if (r != null)
                    {
                        // Set warna untuk URP (_BaseColor) dan Standard (_Color)
                        r.material.color = targetColor;
                        if (r.material.HasProperty("_BaseColor"))
                            r.material.SetColor("_BaseColor", targetColor);
                    }
                }
            }
        }
    }
}