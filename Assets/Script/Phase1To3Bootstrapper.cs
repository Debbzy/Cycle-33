using System.Collections.Generic;
using UnityEngine;
using Cycle33.Data;
using Cycle33.Systems;

namespace Cycle33.Testing
{
    public class Phase1To3Bootstrapper : MonoBehaviour
    {
        [Header("Required ScriptableObjects")]
        public GlobalRulesSO globalRules;
        public StageConfigSO stageConfig;
        public ObjectDataSO grassObjectData;

        [Header("Databases (Masukkan semua SO Hewan & Objek di sini)")]
        public List<AnimalDataSO> allAnimalData = new List<AnimalDataSO>();
        public List<ObjectDataSO> allObjectData = new List<ObjectDataSO>();

        [Header("Optional Initial Spawn (Untuk Testing Cepat)")]
        public AnimalDataSO sheepData;
        public AnimalDataSO wolfData;
        public int initialSheepCount = 2;
        public int initialWolfCount = 0;

        private void Start()
        {
            if (globalRules == null || stageConfig == null || grassObjectData == null)
            {
                Debug.LogError("[Bootstrapper] GlobalRules, StageConfig, atau GrassObjectData belum diisi di Inspector!");
                return;
            }

            // Pastikan sheepData & wolfData otomatis masuk ke allAnimalData bila belum dimasukkan
            if (sheepData != null && !allAnimalData.Contains(sheepData)) allAnimalData.Add(sheepData);
            if (wolfData != null && !allAnimalData.Contains(wolfData)) allAnimalData.Add(wolfData);
            if (grassObjectData != null && !allObjectData.Contains(grassObjectData)) allObjectData.Add(grassObjectData);

            // 1. Inisialisasi seluruh Service P1 - P9
            SimulationClock.Instance.Initialize(globalRules, seed: 33);
            GridService.Instance.InitializeGrid(globalRules, stageConfig, allObjectData);
            if (GridVisualizer.Instance != null) GridVisualizer.Instance.GenerateVisualGrid(globalRules);
            HabitatManager.Instance.Initialize(globalRules, stageConfig, grassObjectData);
            if (CaveManager.Instance != null) CaveManager.Instance.Initialize(stageConfig);
            if (WeatherManager.Instance != null) WeatherManager.Instance.Initialize(globalRules, stageConfig, allObjectData);
            EconomyService.Instance.Initialize(stageConfig, allAnimalData);

            // Pasang konfigurasi ke HUD bila ada
            var hud = FindFirstObjectByType<GameHUDController>();
            if (hud != null)
            {
                hud.globalRules = globalRules;
                hud.stageConfig = stageConfig;
            }

            // 2. Setup Config di CycleManager sebelum spawn hewan awal
            CycleManager.Instance.SetupConfig(globalRules, stageConfig);

            // 3. Spawn hewan awal opsional
            if (sheepData != null)
            {
                for (int i = 0; i < initialSheepCount; i++)
                {
                    Vector2Int pos = new Vector2Int(i, 0);
                    if (GridService.Instance.IsWalkable(pos))
                        CycleManager.Instance.SpawnCreature(sheepData, pos, isPurchased: true);
                }
            }

            if (wolfData != null)
            {
                for (int i = 0; i < initialWolfCount; i++)
                {
                    Vector2Int pos = new Vector2Int(i, globalRules.gridSize.y - 1);
                    if (GridService.Instance.IsWalkable(pos))
                        CycleManager.Instance.SpawnCreature(wolfData, pos, isPurchased: true);
                }
            }

            // 4. Mulai Siklus 1
            CycleManager.Instance.StartStage(globalRules, stageConfig);
        }
    }
}