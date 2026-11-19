using System.Collections.Generic;
using UnityEngine;
using Cycle33.Core;

namespace Cycle33.Data
{
    [CreateAssetMenu(fileName = "AnimalData", menuName = "Cycle33/AnimalData")]
    public class AnimalDataSO : ScriptableObject
    {
        public Species species;
        public int price = 100;
        public int earn = 10;
        public int ageLimit = 6;
        public int birthBarMax = 2;
        public int adultGrowCycles = 1;
        public int hungerInterval = 1;
        public int hungryTolerance = 1;
        public int lightningWeight = 1;

        [Header("Diet & Activity")]
        public List<Species> preyDiet = new List<Species>();
        public bool eatsWorm = false;
        public bool eatsGrass = true;
        public bool isPredator = false;
        public bool activeInDay = true;
        public bool activeInNight = false;
        public bool activeInFullMoonNight = false;

        [Header("Movement & Animation (OQ-6)")]
        public float moveSpeedCellsPerSec = 2.5f;
        public float eatAnimDurationSec = 1.5f;
        [Range(0.1f, 1f)] public float eatHitFrameNormalized = 0.5f;
        public float mateAnimDurationSec = 1.5f;

        [Header("3D Prefabs & Orientation")]
        public GameObject prefab;          // Prefab Adult (atau default)
        public GameObject babyPrefab;      // Prefab Baby (opsional; jika kosong pakai prefab)
        public float modelForwardOffsetY = 0f; // Isi 180 jika model menghadap belakang

        [Header("Kenyat-Kenyot (Squash & Stretch) Saat Jalan")]
        [Range(0f, 0.5f)] public float squashAmount = 0.14f;
        public float squashSpeed = 14f;

        public GameObject GetPrefabForAge(int currentAge)
        {
            bool adult = currentAge >= adultGrowCycles;
            if (!adult && babyPrefab != null)
            {
                return babyPrefab;
            }
            return prefab;
        }
    }
}