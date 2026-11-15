using System.Linq;
using UnityEngine;
using Cycle33.Core;
using Cycle33.Data;

namespace Cycle33.Systems
{
    public class GameHUDController : MonoBehaviour
    {
        public GlobalRulesSO globalRules;
        public StageConfigSO stageConfig;

        private bool? _gameResult = null;
        private float _uiPulseTimer = 0f;

        private void OnEnable()
        {
            EventBus.OnGameEvaluated += HandleGameEvaluated;
        }

        private void OnDisable()
        {
            EventBus.OnGameEvaluated -= HandleGameEvaluated;
        }

        private void HandleGameEvaluated(bool isWin)
        {
            _gameResult = isWin;
        }

        private void Update()
        {
            // TIME-01 & TIME-02: Animasi UI wajib memakai UnscaledDeltaTime agar tetap normal saat Fast-Forward
            if (SimulationClock.Instance != null)
            {
                _uiPulseTimer += SimulationClock.Instance.UnscaledDeltaTime;
            }
        }

        private void OnGUI()
        {
            if (CycleManager.Instance == null || EconomyService.Instance == null || HabitatManager.Instance == null)
                return;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft };
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };

            // 1. Panel Status Atas (Cycle, Phase, Event, Money, Populasi)
            GUILayout.BeginArea(new Rect(15, 15, 460, 210), GUI.skin.box);
            GUILayout.Label($"STAGE {stageConfig.stageIndex}  |  CYCLE: {CycleManager.Instance.CurrentCycle} / {stageConfig.totalCycles}", headerStyle);
            GUILayout.Label($"Phase: {CycleManager.Instance.CurrentPhase} ({CycleManager.Instance.PhaseRemainingSec:F0}s)  |  Weather: {CycleManager.Instance.CurrentEvent}");
            GUILayout.Label($"💰 Money: {EconomyService.Instance.Money}   (Target Akhir: {stageConfig.targetMoney})", headerStyle);

            int sheepCount = CycleManager.Instance.ActiveCreatures.Count(c => c != null && c.Data.species == Species.Sheep);
            int chickenCount = CycleManager.Instance.ActiveCreatures.Count(c => c != null && c.Data.species == Species.Chicken);
            int wolfCount = CycleManager.Instance.ActiveCreatures.Count(c => c != null && c.Data.species == Species.Wolf);
            int civetCount = CycleManager.Instance.ActiveCreatures.Count(c => c != null && c.Data.species == Species.Civet);

            GUILayout.Label($"🌱 Grass: {HabitatManager.Instance.ActiveGrass.Count}  |  🪱 Worms: {HabitatManager.Instance.WormCount}");
            GUILayout.Label($"🐑 Sheep: {sheepCount}  |  🐔 Chicken: {chickenCount}  |  🐺 Wolf: {wolfCount}  |  🦊 Civet: {civetCount}");

            if (PlayerPlacementController.Instance != null)
            {
                GUILayout.Space(4);
                GUILayout.Label($"ℹ️ {PlayerPlacementController.Instance.LastGroundInspectMessage}");
            }
            GUILayout.EndArea();

            // 2. Panel Tombol Fast-Forward (TIME-01: 1x, 2x, 3x, 5x, 6x)
            if (globalRules != null && globalRules.speedMultipliers != null)
            {
                GUILayout.BeginArea(new Rect(490, 15, 320, 70), GUI.skin.box);
                GUILayout.Label("⏩ Fast-Forward Speed (TIME-01):", headerStyle);
                GUILayout.BeginHorizontal();
                for (int i = 0; i < globalRules.speedMultipliers.Length; i++)
                {
                    int mult = globalRules.speedMultipliers[i];
                    if (GUILayout.Button($"{mult}x", btnStyle, GUILayout.Height(30)))
                    {
                        SimulationClock.Instance.SetSpeedIndex(i);
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }

            // 3. Panel Pembelian Hewan (ECON-03 & ECON-04)
            GUILayout.BeginArea(new Rect(15, 235, 460, 95), GUI.skin.box);
            GUILayout.Label("🛒 Toko Hewan (Klik Kanan untuk batal pilih):", headerStyle);
            GUILayout.BeginHorizontal();

            Species[] allSpecies = { Species.Worm, Species.Sheep, Species.Chicken, Species.Wolf, Species.Civet };
            foreach (var sp in allSpecies)
            {
                bool available = EconomyService.Instance.IsSpeciesAvailableInStage(sp);
                GUI.enabled = available && !CycleManager.Instance.IsGameOver;

                var data = EconomyService.Instance.GetAnimalData(sp);
                string priceText = data != null ? $"${data.price}" : "N/A";

                if (GUILayout.Button($"{sp}\n({priceText})", btnStyle, GUILayout.Height(48)))
                {
                    PlayerPlacementController.Instance.SelectSpeciesToBuy(sp);
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // 4. Layar Evaluasi Menang / Kalah di Akhir Siklus Terakhir (ECON-05)
            if (_gameResult.HasValue)
            {
                Rect centerRect = new Rect(Screen.width * 0.5f - 200, Screen.height * 0.5f - 100, 400, 200);
                GUI.Box(centerRect, "");
                GUILayout.BeginArea(centerRect);
                GUILayout.FlexibleSpace();
                string title = _gameResult.Value ? "🎉 STAGE CLEARED (MENANG)!" : "💀 STAGE FAILED (KALAH)!";
                GUILayout.Label(title, new GUIStyle(headerStyle) { fontSize = 22, alignment = TextAnchor.MiddleCenter });
                GUILayout.Space(10);
                GUILayout.Label($"Saldo Akhir: {EconomyService.Instance.Money} / Target: {stageConfig.targetMoney}", new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter });
                GUILayout.FlexibleSpace();
                GUILayout.EndArea();
            }
        }
    }
}