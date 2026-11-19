using System;
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
        public string LastGroundInspectMessage { get; private set; } = "Klik tanah untuk info cacing, atau pilih hewan/worm untuk ditaruh.";

        // Event agar HUD langsung merespons perubahan pilihan (mati/nyala sprite button)
        public event Action<Species?> OnSelectedSpeciesChanged;

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

            // Jika menekan tombol spesies yang sama lagi, batalkan pilihan agar sprite nyala kembali
            if (SelectedSpeciesForPlacement.HasValue && SelectedSpeciesForPlacement.Value == species)
            {
                CancelSelection();
                return;
            }

            SelectedSpeciesForPlacement = species;
            LastGroundInspectMessage = $"Mode Taruh [{species}]: Klik sel HIJAU di grid untuk menaruh.";
            OnSelectedSpeciesChanged?.Invoke(SelectedSpeciesForPlacement);
        }

        public void CancelSelection()
        {
            if (!SelectedSpeciesForPlacement.HasValue) return;
            SelectedSpeciesForPlacement = null;
            LastGroundInspectMessage = "Mode taruh dibatalkan.";
            OnSelectedSpeciesChanged?.Invoke(null);
        }

        private void Update()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null || CycleManager.Instance == null || CycleManager.Instance.IsGameOver) return;

            if (SimulationClock.Instance != null && SimulationClock.Instance.IsPaused)
            {
                HoveredGridCell = null;
                return;
            }

            if (Mouse.current == null) return;

            // Klik Kanan untuk membatalkan pilihan beli hewan/worm (sprite tombol akan nyala lagi)
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelSelection();
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                HoveredGridCell = null;
                return;
            }

            UpdateHoveredCell();

            if (Mouse.current.leftButton.wasPressedThisFrame && HoveredGridCell.HasValue)
            {
                Vector2Int gridPos = HoveredGridCell.Value;

                if (SelectedSpeciesForPlacement.HasValue)
                {
                    Species chosen = SelectedSpeciesForPlacement.Value;
                    bool bought = EconomyService.Instance.TryBuyAnimalAtCell(chosen, gridPos);
                    if (bought)
                    {
                        LastGroundInspectMessage = $"Berhasil menaruh {chosen} di sel {gridPos}!";
                    }
                    else
                    {
                        LastGroundInspectMessage = $"Gagal! Sel {gridPos} terhalang objek atau {chosen} tidak tersedia!";
                    }
                }
                else
                {
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