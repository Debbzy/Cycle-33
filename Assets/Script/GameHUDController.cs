using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using Cycle33.Core;
using Cycle33.Data;
using Cycle33.Audio;

namespace Cycle33.Systems
{
    public class GameHUDController : MonoBehaviour
    {
        public static GameHUDController Instance { get; private set; }

        [Serializable]
        public struct EventSpriteEntry
        {
            public Cycle33.Core.EventType eventType;
            public Sprite iconSprite;
            public GameObject iconObject; // Opsional: jika tiap event punya GameObject sprite terpisah
        }

        [Header("Configurations")]
        public GlobalRulesSO globalRules;
        public StageConfigSO stageConfig;

        [Header("UI Panels (Tarik GameObject Panel Canvas ke Sini)")]
        public GameObject winPanel;
        public GameObject losePanel;
        public GameObject pausePanel;

        [Header("Event / Disaster Sprite Icons")]
        public Image eventIconImage;                  // Opsional: 1 Image yang mengganti Sprite sesuai event
        public List<EventSpriteEntry> eventIcons = new List<EventSpriteEntry>(); // Isi Rain, Thunderstorm, FullMoon, BirthSeason

        [Header("Clock Phase & Hand UI (Atas = Siang, Bawah = Malam)")]
        public RectTransform clockHandRect;          // Jarum jam yang dirotasi
        public float dayStartAngleZ = 0f;            // Sudut jarum saat awal siang (mis. 0° = ke atas)
        public float nightStartAngleZ = 180f;        // Sudut jarum saat awal malam (mis. 180° = ke bawah)
        public float cycleEndAngleZ = 360f;          // Sudut jarum saat akhir malam / kembali ke siang

        [Header("Buy Button Sprites (Mati saat dipilih, nyala lagi saat klik kanan/ganti pilihan)")]
        public GameObject wormButtonSprite;
        public GameObject sheepButtonSprite;
        public GameObject chickenButtonSprite;
        public GameObject wolfButtonSprite;
        public GameObject civetButtonSprite;

        [Header("Stage Info UI Texts (Opsional)")]
        public TMP_Text stageText;
        public TMP_Text cycleText;
        public TMP_Text maxCycleText;
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
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);

            RefreshEventIcons(Cycle33.Core.EventType.None);
            RefreshBuyButtonSprites(null);
        }

        private void OnEnable()
        {
            EventBus.OnGameEvaluated += HandleGameEvaluated;
            EventBus.OnEventStarted += RefreshEventIcons;
            if (PlayerPlacementController.Instance != null)
            {
                PlayerPlacementController.Instance.OnSelectedSpeciesChanged += RefreshBuyButtonSprites;
            }
        }

        private void OnDisable()
        {
            EventBus.OnGameEvaluated -= HandleGameEvaluated;
            EventBus.OnEventStarted -= RefreshEventIcons;
            if (PlayerPlacementController.Instance != null)
            {
                PlayerPlacementController.Instance.OnSelectedSpeciesChanged -= RefreshBuyButtonSprites;
            }
        }

        private void Update()
        {
            // Pastikan subscription event terpasang jika PlayerPlacementController baru aktif setelah OnEnable
            if (PlayerPlacementController.Instance != null)
            {
                PlayerPlacementController.Instance.OnSelectedSpeciesChanged -= RefreshBuyButtonSprites;
                PlayerPlacementController.Instance.OnSelectedSpeciesChanged += RefreshBuyButtonSprites;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (CycleManager.Instance != null && !CycleManager.Instance.IsGameOver)
                {
                    TogglePauseMenu();
                }
            }

            RefreshClockHand();
            RefreshAllUITexts();
        }

        // ====================================================================
        // 1. EVENT / DISASTER SPRITE ICON
        // ====================================================================
        private void RefreshEventIcons(Cycle33.Core.EventType activeEvent)
        {
            bool hasSpriteAssigned = false;

            foreach (var entry in eventIcons)
            {
                bool isMatch = (activeEvent != Cycle33.Core.EventType.None && entry.eventType == activeEvent);

                if (entry.iconObject != null)
                {
                    entry.iconObject.SetActive(isMatch);
                }

                if (isMatch && entry.iconSprite != null && eventIconImage != null)
                {
                    eventIconImage.sprite = entry.iconSprite;
                    eventIconImage.enabled = true;
                    hasSpriteAssigned = true;
                }
            }

            if (eventIconImage != null && !hasSpriteAssigned)
            {
                eventIconImage.sprite = null;
                eventIconImage.enabled = false;
            }
        }

        // ====================================================================
        // 2. CLOCK HAND (SIANG DI ATAS, MALAM DI BAWAH, MENDEKATI WAKTU BERIKUTNYA)
        // ====================================================================
        private void RefreshClockHand()
        {
            if (clockHandRect == null || CycleManager.Instance == null) return;

            float angleZ = dayStartAngleZ;
            PhaseType phase = CycleManager.Instance.CurrentPhase;
            float p = CycleManager.Instance.PhaseProgress01; // 0 = awal fase, 1 = hampir ke fase berikutnya

            if (phase == PhaseType.DayPhase)
            {
                // Bergerak dari posisi atas (siang) menuju posisi bawah (mendekati malam)
                angleZ = Mathf.Lerp(dayStartAngleZ, nightStartAngleZ, p);
            }
            else if (phase == PhaseType.NightPhase)
            {
                // Bergerak dari posisi bawah (malam) menuju posisi atas/siklus berikutnya (mendekati siang)
                angleZ = Mathf.Lerp(nightStartAngleZ, cycleEndAngleZ, p);
            }

            // Rotasi negatif pada sumbu Z agar jarum berputar searah jarum jam
            clockHandRect.localEulerAngles = new Vector3(0f, 0f, -angleZ);
        }

        // ====================================================================
        // 3. BUY BUTTON SPRITE TOGGLE (MATI SAAT DIPILIH, NYALA SAAT BATAL/GANTI)
        // ====================================================================
        private void RefreshBuyButtonSprites(Species? selectedSpecies)
        {
            SetSpriteActive(wormButtonSprite, selectedSpecies != Species.Worm);
            SetSpriteActive(sheepButtonSprite, selectedSpecies != Species.Sheep);
            SetSpriteActive(chickenButtonSprite, selectedSpecies != Species.Chicken);
            SetSpriteActive(wolfButtonSprite, selectedSpecies != Species.Wolf);
            SetSpriteActive(civetButtonSprite, selectedSpecies != Species.Civet);
        }

        private void SetSpriteActive(GameObject spriteObj, bool active)
        {
            if (spriteObj != null && spriteObj.activeSelf != active)
            {
                spriteObj.SetActive(active);
            }
        }

        // ====================================================================
        // 4. WIN / LOSE & PAUSE MENU
        // ====================================================================
        private void HandleGameEvaluated(bool isWin)
        {
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

        public void TogglePauseMenu()
        {
            if (SimulationClock.Instance == null) return;
            SetPauseState(!SimulationClock.Instance.IsPaused);
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
            int index = SceneManager.GetActiveScene().buildIndex;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.LoadSceneWithFade(index, () => Time.timeScale = 1f);
                return;
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(index);
        }

        public void LoadSceneByName(string sceneName)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.LoadSceneWithFade(sceneName, () => Time.timeScale = 1f);
                return;
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        // ====================================================================
        // 5. FUNGSI BUTTON MEMBELI HEWAN & WORM (TARUH DI GRID)
        // ====================================================================
        public void BuyWorm() => PlayerPlacementController.Instance?.SelectSpeciesToBuy(Species.Worm);
        public void BuySheep() => PlayerPlacementController.Instance?.SelectSpeciesToBuy(Species.Sheep);
        public void BuyChicken() => PlayerPlacementController.Instance?.SelectSpeciesToBuy(Species.Chicken);
        public void BuyWolf() => PlayerPlacementController.Instance?.SelectSpeciesToBuy(Species.Wolf);
        public void BuyCivet() => PlayerPlacementController.Instance?.SelectSpeciesToBuy(Species.Civet);
        public void CancelBuySelection() => PlayerPlacementController.Instance?.CancelSelection();

        // ====================================================================
        // 6. FUNGSI BUTTON FAST-FORWARD (1x, 2x, 4x, 6x)
        // ====================================================================
        public void SetSpeed1x() => SimulationClock.Instance?.SetSpeedByMultiplierValue(1);
        public void SetSpeed2x() => SimulationClock.Instance?.SetSpeedByMultiplierValue(2);
        public void SetSpeed4x() => SimulationClock.Instance?.SetSpeedByMultiplierValue(4);
        public void SetSpeed6x() => SimulationClock.Instance?.SetSpeedByMultiplierValue(6);
        public void SetSpeedByIndex(int index) => SimulationClock.Instance?.SetSpeedIndex(index);

        // ====================================================================
        // 7. GETTER & TEXT REFRESH
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

        private void RefreshAllUITexts()
        {
            if (CycleManager.Instance == null || EconomyService.Instance == null || HabitatManager.Instance == null)
                return;

            if (stageText != null) stageText.text = $"Stage {GetCurrentStageIndex()}";
            if (cycleText != null) cycleText.text = $"{GetCurrentCycle()}";
            if (maxCycleText != null) maxCycleText.text = $"{GetMaxCycles()}";
            if (phaseAndTimerText != null) phaseAndTimerText.text = $"{GetCurrentPhaseName()} ({GetPhaseRemainingSeconds():F0}s)";
            if (weatherText != null) weatherText.text = $"Weather: {GetCurrentWeatherName()}";
            if (moneyText != null) moneyText.text = $"{GetCurrentMoney()}";
            if (targetMoneyText != null) targetMoneyText.text = $"{GetCurrentMoney()} / {GetTargetMoney()}";

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