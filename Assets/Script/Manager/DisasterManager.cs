using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum DisasterType
{
    None,
    Rain,
    Thunderstorm,
    FullMoon,
    BirthSeason
}

public class DisasterManager : MonoBehaviour
{
    [Header("Current Active Disaster")]
    public DisasterType currentDisaster = DisasterType.None;

    [Header("Thunderstorm Settings")]
    public float lightningInterval = 15f; // Menyambar setiap 15 detik
    private Coroutine thunderstormCoroutine;

    [Header("References")]
    public GridManager gridManager; // <-- Pastikan baris ini ada!

    void Awake()
    {
        // Mencari GridManager secara otomatis di GameObject yang sama jika belum di-drag
        if (gridManager == null)
        {
            gridManager = GetComponent<GridManager>();
        }
    }

    // Dipanggil setiap kali siklus/hari berganti (misal setiap 3 hari)
    public void CheckAndTriggerDisaster(int currentCycle, List<DisasterType> availableDisastersInStage)
    {
        // Sesuai aturan: Setiap 3 Cycle akan randomize disaster yang tersedia di stage tersebut
        if (currentCycle % 3 == 0 && availableDisastersInStage.Count > 0)
        {
            int randomIndex = Random.Range(0, availableDisastersInStage.Count);
            currentDisaster = availableDisastersInStage[randomIndex];
            
            ApplyDisasterEffect();
        }
        else
        {
            currentDisaster = DisasterType.None;
        }
    }

    void ApplyDisasterEffect()
    {
        // Hentikan coroutine petir sebelumnya jika ada
        if (thunderstormCoroutine != null)
        {
            StopCoroutine(thunderstormCoroutine);
        }

        switch (currentDisaster)
        {
            case DisasterType.Rain:
                TriggerRainEffect();
                break;
            case DisasterType.FullMoon:
                TriggerFullMoonEffect();
                break;
            case DisasterType.BirthSeason:
                TriggerBirthSeasonEffect();
                break;
            case DisasterType.Thunderstorm:
                thunderstormCoroutine = StartCoroutine(ThunderstormRoutine());
                break;
            default:
                Debug.Log("Normal Day, no disaster.");
                break;
        }
    }

    void TriggerRainEffect()
    {
        Debug.Log("Disaster Active: Rain (Rn) - Rumput bertambah, Wolf & Civet tidur.");
        // Logika tambahan: Tambah 10 rumput secara random di grid, set status tidur wolf/civet = true
    }

    void TriggerFullMoonEffect()
    {
        Debug.Log("Disaster Active: Full Moon (Fm) - Wolf menjadi Nocturn & Hungry.");
        // Logika tambahan: Set wolf.nocturn = true dan wolf.hungry = true di malam hari
    }

    void TriggerBirthSeasonEffect()
    {
        Debug.Log("Disaster Active: Birth Season (Bs) - Musim berkembang biak!");
        // Logika tambahan: Set Status_Mating worm, sheep, chicken = true, birth_bar wolf & civet = full
    }

    IEnumerator ThunderstormRoutine()
    {
        Debug.Log("Disaster Active: Thunderstorm (Ts) - Badai petir dimulai!");
        
        // Tunggu 15 detik untuk strike pertama (sesuai diagram flow)
        yield return new WaitForSeconds(lightningInterval);

        while (currentDisaster == DisasterType.Thunderstorm)
        {
            ExecuteLightningStrike();
            // Lanjutkan sambaran setiap 15 detik berikutnya
            yield return new WaitForSeconds(lightningInterval);
        }
    }

    void ExecuteLightningStrike()
    {
        Debug.Log("Petir menyambar! Mencari target di grid...");

        if (gridManager == null) return;

        // 1. Pilih random tile pada grid 10x10
        int randomX = Random.Range(0, gridManager.width);
        int randomZ = Random.Range(0, gridManager.height);

        // Ambil objek yang ada di tile tersebut
        GameObject targetObj = gridManager.GetObjectAtGrid(randomX, randomZ);

        if (targetObj != null)
        {
            // Cek apakah objek tersebut memiliki komponen yang menyimpan data status (misalnya script Creature / Plant / Structure)
            // Di sini kita asumsikan ada script pengelola data objek bernama "EntityData" atau kamu bisa sesuaikan dengan nama class-mu.
            
            // Contoh pengecekan komponen Undercover atau prioritas pembakaran/penghancuran
            // Jika objek memiliki beberapa entitas/komponen tertumpuk di satu tile:
            
            // Mengambil semua collider / komponen target di tile tersebut (jika lebih dari 1)
            // Atau kita cek berdasarkan nilai prioritas / lightning_weight dari data objek.
            
            // Contoh implementasi logika penghancuran:
            Debug.Log($"Petir menyambar tile ({randomX}, {randomZ}) mengenai objek: {targetObj.name}");

            // Cek apakah objek memiliki status Undercover (jika ada script-nya)
            // EntityData entity = targetObj.GetComponent<EntityData>();
            // if (entity != null && entity.undercover) { Debug.Log("Objek terlindung, petir tidak berpengaruh."); return; }

            // Hancurkan / bakar objek (Misal: hancurkan rumput atau kill creature)
            Destroy(targetObj);
            gridManager.SetObjectAtGrid(randomX, randomZ, null); // Kosongkan kembali grid
        }
        else
        {
            Debug.Log($"Petir menyambar tile ({randomX}, {randomZ}), tetapi tile kosong.");
        }
    }
}