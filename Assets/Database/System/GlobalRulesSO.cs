using UnityEngine;

namespace Cycle33.Data
{
    [CreateAssetMenu(fileName = "GlobalRules", menuName = "Cycle33/GlobalRules")]
    public class GlobalRulesSO : ScriptableObject
    {
        [Header("Cycle & Time (TIME-*)")]
        public float cycleDurationSec = 120f;
        public float dayDurationSec = 60f;
        public float nightDurationSec = 60f;
        public int[] speedMultipliers = { 1, 2, 3, 5, 6 };

        [Header("Grid")]
        public Vector2Int gridSize = new Vector2Int(10, 10);

        [Header("Weather & Sickness")]
        public float lightningIntervalSec = 15f;
        [Range(0, 100)] public int sickEarnReductionPercent = 50;
        public int sickDurationCycles = 3;
        public int rainGrassBonus = 10;

        [Header("Worm & Parasite (HAB-*)")]
        public int wormsPerGrassBonus = 5;
        public float wormOverpopRatio = 0.5f;
        [Range(0f, 1f)] public float parasiteChancePerNewGrass = 0.3f;

        [Header("Idle & Drinking")]
        [Range(0f, 1f)] public float drinkChancePerIdleCheck = 0.1f;
        public float drinkDurationSec = 3f;
    }
}