using UnityEngine;
using System.Collections.Generic;

public enum TimeState
{
    Daytime,
    Nighttime
}

public class GameTimeManager : MonoBehaviour
{
    [Header("Time Settings")]
    public float dayDuration = 60f;   // Durasi Siang (detik)
    public float nightDuration = 60f;  // Durasi Malam (detik)
    
    [Header("Current Status")]
    public int currentCycle = 1;       // Hari / Cycle ke-
    public TimeState currentTimeState = TimeState.Daytime;
    private float timer = 0f;

    [Header("References")]
    public GridManager gridManager;
    public DisasterManager disasterManager;
    public GameObject grassPrefab;     // Masukkan prefab rumput low-poly di sini

    [Header("Stage Disaster List (Contoh Stage 1)")]
    public List<DisasterType> stageDisasters = new List<DisasterType> { DisasterType.Rain, DisasterType.Thunderstorm };

    void Start()
    {
        // Mulai hari pertama dengan spawn rumput pagi
        SpawnRandomGrass(5);
    }

    void Update()
    {
        // Menghitung waktu berjalan berdasarkan kondisi Siang / Malam
        float targetDuration = (currentTimeState == TimeState.Daytime) ? dayDuration : nightDuration;
        timer += Time.deltaTime;

        if (timer >= targetDuration)
        {
            timer = 0f;
            SwitchTimeState();
        }
    }

    void SwitchTimeState()
    {
        if (currentTimeState == TimeState.Daytime)
        {
            // Berubah ke Malam
            currentTimeState = TimeState.Nighttime;
            Debug.Log($"Cycle {currentCycle}: Masuk Waktu Malam (Nighttime)");
        }
        else
        {
            // Berubah kembali ke Pagi/Siang (Berganti Cycle / Hari Baru)
            currentTimeState = TimeState.Daytime;
            currentCycle++;
            Debug.Log($"--- Siklus Baru Dimulai: Hari ke-{currentCycle} ---");

            // 1. Cek Disaster terlebih dahulu untuk hari ini (Setiap 3 cycle)
            if (disasterManager != null)
            {
                disasterManager.CheckAndTriggerDisaster(currentCycle, stageDisasters);
            }

            // 2. Tentukan jumlah spawn rumput berdasarkan apakah sedang Rain atau bukan
            int grassToSpawn = 5; // Standar harian

            // Jika disaster aktif saat ini adalah Rain, tambahkan +10 rumput (total 15)
            if (disasterManager != null && disasterManager.currentDisaster == DisasterType.Rain)
            {
                grassToSpawn += 10;
                Debug.Log("Disaster Rain aktif! Pagi ini rumput bertambah 15 (5 standar + 10 bonus).");
            }

            // 3. Spawn rumput ke grid sesuai jumlah yang ditentukan
            SpawnRandomGrass(grassToSpawn);
        }
    }

    // Fungsi untuk men-spawn sejumlah rumput secara random di koordinat grid yang kosong
    void SpawnRandomGrass(int amount)
    {
        if (gridManager == null || grassPrefab == null)
        {
            Debug.LogWarning("GridManager atau GrassPrefab belum di-assign di Inspector!");
            return;
        }

        // 1. Kumpulkan semua koordinat grid yang saat ini masih KOSONG (null)
        List<Vector2Int> emptyTiles = new List<Vector2Int>();

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int z = 0; z < gridManager.height; z++)
            {
                if (gridManager.GetObjectAtGrid(x, z) == null)
                {
                    emptyTiles.Add(new Vector2Int(x, z));
                }
            }
        }

        // 2. Jika tidak ada tile kosong sama sekali, batalkan
        if (emptyTiles.Count == 0)
        {
            Debug.Log("Grid sudah penuh! Tidak bisa spawn rumput lagi.");
            return;
        }

        // 3. Acak urutan list tile kosong (Fisher-Yates Shuffle sederhana)
        for (int i = 0; i < emptyTiles.Count; i++)
        {
            Vector2Int temp = emptyTiles[i];
            int randomIndex = Random.Range(i, emptyTiles.Count);
            emptyTiles[i] = emptyTiles[randomIndex];
            emptyTiles[randomIndex] = temp;
        }

        // 4. Tentukan berapa banyak yang akan di-spawn (minimal antara target atau sisa tile kosong)
        int spawnCount = Mathf.Min(amount, emptyTiles.Count);

        // 5. Eksekusi spawn berdasarkan tile kosong yang sudah diacak pasti
        for (int i = 0; i < spawnCount; i++)
        {
            Vector2Int tileCoord = emptyTiles[i];

            // Ambil posisi dunia 3D dari grid
            Vector3 spawnPos = gridManager.GetWorldPosition(tileCoord.x, tileCoord.y);

            // Instantiate prefab rumput
            GameObject newGrass = Instantiate(grassPrefab, spawnPos, Quaternion.identity, gridManager.transform);

            // Catat ke dalam GridManager
            gridManager.SetObjectAtGrid(tileCoord.x, tileCoord.y, newGrass);
        }

        Debug.Log(spawnCount < amount 
            ? $"Grid hampir penuh! Hanya berhasil men-spawn {spawnCount} dari target {amount} rumput." 
            : $"Berhasil men-spawn {spawnCount} rumput baru di pagi hari.");
    }
}