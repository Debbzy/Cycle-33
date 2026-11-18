using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro; // Menggunakan TextMeshPro bawaan Unity 6
using Cycle33.Core;
using Cycle33.Data;

namespace Cycle33.Systems
{
    public class GameHUDController : MonoBehaviour
    {
        public static GameHUDController Instance { get; private set; }

        [Header("Configurations")]
        public GlobalRulesSO globalRules;
        public StageConfigSO stageConfig;

        [Header("UI Panels (Tarik GameObject Panel Canvas ke Sini)")]
        public GameObject winPanel;
        public GameObject losePanel;
        public GameObject pausePanel;

        [Header("Stage Info UI Texts (Opsional: Kosongkan jika tidak dipakai)")]
        public TMP_Text stageText;
        public TMP_Text cycleText;
        public TMP_Text phaseAndTimerText;
        public TMP_Text weatherText;
        public TMP_Text moneyText;
        public TMP_Text targetMoneyText;
        public TMP_Text speedStatusText;
        public TMP_Text inspectInfoText;

        [Header("Population UI Texts (Opsional)")]
        public TMP_Text grassCountText;
        public TMP_Text wormCountText;
        public TMP_Text sheepCountText;
        public TMP_Text chickenCountText;
        public TMP_Text wolfCountText;
        public TMP_Text civetCountText;

        [Header("Win/Lose Final Score Texts (Opsional)")]
        public TMP_Text winFinalMoneyText;
        public TMP_Text loseFinalMoneyText;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Pastikan panel Win, Lose, dan Pause mati saat stage baru dimulai
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.OnGameEvaluated += HandleGameEvaluated;
        }

        private void OnDisable()
        {
            EventBus.OnGameEvaluated -= HandleGameEvaluated;
        }

        private void Update()
        {
            // Deteksi tombol ESC untuk membuka/menutup Pause Menu
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (CycleManager.Instance != null && !CycleManager.Instance.IsGameOver)
                {
                    TogglePauseMenu();
                }
            }

            RefreshAllUITexts();
        }

        // ====================================================================
        // 1. SISTEM WIN / LOSE PANEL (Otomatis menyala saat Cycle == MaxCycle)
        // ====================================================================
        private void HandleGameEvaluated(bool isWin)
        {
            // Hentikan simulasi saat panel Win/Lose muncul
            if (SimulationClock.Instance != null)
            {
                SimulationClock.Instance.SetPaused(true);
            }

            if (pausePanel != null) pausePanel.SetActive(false);

            if (isWin)
            {
                if (winPanel != null) winPanel.SetActive(true);
                if (winFinalMoneyText != null)
                    winFinalMoneyText.text = $"Money: {GetCurrentMoney()} / Target: {GetTargetMoney()}";
            }
            else
            {
                if (losePanel != null) losePanel.SetActive(true);
                if (loseFinalMoneyText != null)
                    loseFinalMoneyText.text = $"Money: {GetCurrentMoney()} / Target: {GetTargetMoney()}";
            }
        }

        // ====================================================================
        // 2. SISTEM PAUSE MENU (Dipanggil via ESC atau Button OnClick)
        // ====================================================================
        public void TogglePauseMenu()
        {
            if (SimulationClock.Instance == null) return;
            bool willPause = !SimulationClock.Instance.IsPaused;
            SetPauseState(willPause);
        }

        public void PauseGame() => SetPauseState(true);
        public void ResumeGame() => SetPauseState(false);

        private void SetPauseState(bool pause)
        {
            if (SimulationClock.Instance != null)
            {
                SimulationClock.Instance.SetPaused(pause);
            }
            if (pausePanel != null)
            {
                pausePanel.SetActive(pause);
            }
        }

        public void RestartStage()
        {
            Time.timeScale = 1f;
            Scene currentScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(currentScene.buildIndex);
        }

        public void LoadSceneByName(string sceneName)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        // ====================================================================
        // 3. FUNGSI BUTTON MEMBELI HEWAN (Hubungkan ke Button OnClick)
        // ====================================================================
        public void BuyWorm()
        {
            if (PlayerPlacementController.Instance != null)
                PlayerPlacementController.Instance.SelectSpeciesToBuy(Species.Worm);
        }

        public void BuySheep()
        {
            if (PlayerPlacementController.Instance != null)
                PlayerPlacementController.Instance.SelectSpeciesToBuy(Species.Sheep);
        }

        public void BuyChicken()
        {
            if (PlayerPlacementController.Instance != null)
                PlayerPlacementController.Instance.SelectSpeciesToBuy(Species.Chicken);
        }

        public void BuyWolf()
        {
            if (PlayerPlacementController.Instance != null)
                PlayerPlacementController.Instance.SelectSpeciesToBuy(Species.Wolf);
        }

        public void BuyCivet()
        {
            if (PlayerPlacementController.Instance != null)
                PlayerPlacementController.Instance.SelectSpeciesToBuy(Species.Civet);
        }

        public void CancelBuySelection()
        {
            if (PlayerPlacementController.Instance != null)
                PlayerPlacementController.Instance.CancelSelection();
        }

        // ====================================================================
        // 4. FUNGSI BUTTON FAST-FORWARD (Hubungkan ke Button OnClick)
        // ====================================================================
        public void SetSpeed1x() => SimulationClock.Instance?.SetSpeedByMultiplierValue(1);
        public void SetSpeed2x() => SimulationClock.Instance?.SetSpeedByMultiplierValue(2);
        public void SetSpeed3x() => SimulationClock.Instance?.SetSpeedByMultiplierValue(3);
        public void SetSpeed5x() => SimulationClock.Instance?.SetSpeedByMultiplierValue(5);
        public void SetSpeed6x() => SimulationClock.Instance?.SetSpeedByMultiplierValue(6);

        // Bisa juga dipanggil dari Button dengan parameter int index (0, 1, 2, 3, 4)
        public void SetSpeedByIndex(int index) => SimulationClock.Instance?.SetSpeedIndex(index);

        // ====================================================================
        // 5. FUNGSI GETTER INFO STAGE (Bisa diambil dari script UI lain)
        // ====================================================================
        public int GetCurrentStageIndex() => stageConfig != null ? stageConfig.stageIndex : 1;
        public int GetCurrentCycle() => CycleManager.Instance != null ? CycleManager.Instance.CurrentCycle : 0;
        public int GetMaxCycles() => stageConfig != null ? stageConfig.totalCycles : 0;
        public string GetCurrentPhaseName() => CycleManager.Instance != null ? CycleManager.Instance.CurrentPhase.ToString() : "-";
        public float GetPhaseRemainingSeconds() => CycleManager.Instance != null ? CycleManager.Instance.PhaseRemainingSec : 0f;
        public string GetCurrentWeatherName() => CycleManager.Instance != null ? CycleManager.Instance.CurrentEvent.ToString() : "None";
        public int GetCurrentMoney() => EconomyService.Instance != null ? EconomyService.Instance.Money : 0;
        public int GetTargetMoney() => stageConfig != null ? stageConfig.targetMoney : 0;
        public int GetWormCount() => HabitatManager.Instance != null ? HabitatManager.Instance.WormCount : 0;
        public int GetGrassCount() => HabitatManager.Instance != null ? HabitatManager.Instance.ActiveGrass.Count : 0;

        public int GetSpeciesCount(Species species)
        {
            if (CycleManager.Instance == null) return 0;
            return CycleManager.Instance.ActiveCreatures.Count(c => c != null && c.Data.species == species && c.State != CreatureState.Dead);
        }

        // Update otomatis seluruh TMP_Text yang dipasang di Inspector
        private void RefreshAllUITexts()
        {
            if (CycleManager.Instance == null || EconomyService.Instance == null || HabitatManager.Instance == null)
                return;

            if (stageText != null) stageText.text = $"Stage {GetCurrentStageIndex()}";
            if (cycleText != null) cycleText.text = $"Cycle: {GetCurrentCycle()} / {GetMaxCycles()}";
            if (phaseAndTimerText != null) phaseAndTimerText.text = $"{GetCurrentPhaseName()} ({GetPhaseRemainingSeconds():F0}s)";
            if (weatherText != null) weatherText.text = $"Weather: {GetCurrentWeatherName()}";
            if (moneyText != null) moneyText.text = $"${GetCurrentMoney()}";
            if (targetMoneyText != null) targetMoneyText.text = $"Target: ${GetTargetMoney()}";

            if (speedStatusText != null && SimulationClock.Instance != null)
            {
                speedStatusText.text = SimulationClock.Instance.IsPaused
                    ? "PAUSED"
                    : $"{SimulationClock.Instance.CurrentSpeedMultiplier}x";
            }

            if (inspectInfoText != null && PlayerPlacementController.Instance != null)
            {
                inspectInfoText.text = PlayerPlacementController.Instance.LastGroundInspectMessage;
            }

            if (grassCountText != null) grassCountText.text = GetGrassCount().ToString();
            if (wormCountText != null) wormCountText.text = GetWormCount().ToString();
            if (sheepCountText != null) sheepCountText.text = GetSpeciesCount(Species.Sheep).ToString();
            if (chickenCountText != null) chickenCountText.text = GetSpeciesCount(Species.Chicken).ToString();
            if (wolfCountText != null) wolfCountText.text = GetSpeciesCount(Species.Wolf).ToString();
            if (civetCountText != null) civetCountText.text = GetSpeciesCount(Species.Civet).ToString();
        }
    }
}