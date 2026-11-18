using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Cycle33.Core;

namespace Cycle33.Systems
{
    public class PlayerPlacementController : MonoBehaviour
    {
        public static PlayerPlacementController Instance { get; private set; }

        [SerializeField] private Camera mainCamera;
        public Species? SelectedSpeciesForPlacement { get; private set; }
        public Vector2Int? HoveredGridCell { get; private set; }
        public string LastGroundInspectMessage { get; private set; } = "Klik tanah untuk info cacing, atau pilih hewan untuk membeli.";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (mainCamera == null) mainCamera = Camera.main;
        }

        public void SelectSpeciesToBuy(Species species)
        {
            if (SimulationClock.Instance != null && SimulationClock.Instance.IsPaused) return;
            if (CycleManager.Instance != null && CycleManager.Instance.IsGameOver) return;

            if (species == Species.Worm)
            {
                // ECON-04: Worm langsung dibeli lewat tombol UI tanpa klik posisi grid
                if (EconomyService.Instance.TryBuyWorm())
                {
                    LastGroundInspectMessage = $"Membeli 1 Worm! Total Worm di tanah: {HabitatManager.Instance.WormCount}";
                }
                SelectedSpeciesForPlacement = null;
            }
            else
            {
                SelectedSpeciesForPlacement = species;
                LastGroundInspectMessage = $"Mode Beli [{species}]: Klik sel HIJAU di grid untuk menaruh.";
            }
        }

        public void CancelSelection()
        {
            SelectedSpeciesForPlacement = null;
            LastGroundInspectMessage = "Mode beli dibatalkan.";
        }

        private void Update()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null || CycleManager.Instance == null || CycleManager.Instance.IsGameOver) return;

            // Jangan proses input klik grid jika game sedang di-Pause
            if (SimulationClock.Instance != null && SimulationClock.Instance.IsPaused)
            {
                HoveredGridCell = null;
                return;
            }

            if (Mouse.current == null) return;

            // Bila kursor berada di atas elemen Canvas UI Unity, jangan highlight grid di bawahnya
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                HoveredGridCell = null;
                return;
            }

            UpdateHoveredCell();

            // Klik Kanan untuk batal memilih hewan
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelSelection();
                return;
            }

            // Klik Kiri pada tile grid
            if (Mouse.current.leftButton.wasPressedThisFrame && HoveredGridCell.HasValue)
            {
                Vector2Int gridPos = HoveredGridCell.Value;

                if (SelectedSpeciesForPlacement.HasValue)
                {
                    // ECON-03: Beli & taruh hewan di sel walkable
                    bool bought = EconomyService.Instance.TryBuyAnimalAtCell(SelectedSpeciesForPlacement.Value, gridPos);
                    if (bought)
                    {
                        LastGroundInspectMessage = $"Berhasil membeli {SelectedSpeciesForPlacement.Value} di sel {gridPos}!";
                    }
                    else
                    {
                        LastGroundInspectMessage = $"Gagal! Sel {gridPos} terhalang objek atau hewan tidak tersedia!";
                    }
                }
                else
                {
                    // Bagian 7.2: Klik tanah menampilkan jumlah cacing
                    GridCell cell = GridService.Instance.GetCell(gridPos);
                    string grassStatus = (cell != null && cell.Grass != null)
                        ? (cell.Grass.IsParasite ? "Rumput Parasit" : "Rumput Sehat")
                        : "Tanah Kosong";
                    LastGroundInspectMessage = $"Sel {gridPos} [{grassStatus}] | Cacing: {HabitatManager.Instance.WormCount}";
                }
            }
        }

        private void UpdateHoveredCell()
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitWorld = ray.GetPoint(enter);
                Vector2Int cellPos = GridService.Instance.WorldToGrid(hitWorld);

                if (GridService.Instance.IsInBounds(cellPos))
                {
                    HoveredGridCell = cellPos;
                    return;
                }
            }
            HoveredGridCell = null;
        }
    }
}