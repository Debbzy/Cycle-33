using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 10;
    public int height = 10;
    public float tileSize = 1f; // Ukuran 1 tile di dunia nyata
    
    [Header("Position Adjustment")]
    public float surfaceHeight = 0.5f;
    public float offsetX = -0.5f;
    public float offsetZ = -0.5f;

    private GameObject[,] gridArray;

    void Awake()
    {
        gridArray = new GameObject[width, height];
    }

    // Mengubah koordinat grid menjadi posisi dunia 3D dengan aman dari distorsi scale parent
    public Vector3 GetWorldPosition(int x, int z)
    {
        // Hitung posisi awal dari sudut kiri-depan grid agar centered
        float startX = -(width * tileSize) / 2f + offsetX;
        float startZ = -(height * tileSize) / 2f + offsetX;

        // Posisi lokal relatif terhadap world
        Vector3 localPos = new Vector3(startX + (x * tileSize), surfaceHeight, startZ + (z * tileSize));
        
        // TransformPoint memperhitungkan posisi dan rotasi World, 
        // namun untuk menghindari masalah scale parent yang besar, 
        // kita bisa pastikan scale world bernilai 1 dengan cara manual atau menggunakan matrix rotasi saja:
        return transform.position + (transform.rotation * localPos);
    }

    public bool IsValidPosition(int x, int z)
    {
        return x >= 0 && z >= 0 && x < width && z < height;
    }

    public void SetObjectAtGrid(int x, int z, GameObject obj)
    {
        if (IsValidPosition(x, z))
        {
            gridArray[x, z] = obj;
        }
    }

    public GameObject GetObjectAtGrid(int x, int z)
    {
        if (IsValidPosition(x, z))
        {
            return gridArray[x, z];
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 0.3f, 0.6f);
        
        float startX = -(width * tileSize) / 2f;
        float startZ = -(height * tileSize) / 2f;

        // Menggambar garis gizmos dengan memperhitungkan rotasi dan posisi world secara akurat
        for (int x = 0; x <= width; x++)
        {
            float xPos = startX + (x * tileSize);
            Vector3 startLocal = new Vector3(xPos, surfaceHeight, startZ);
            Vector3 endLocal = new Vector3(xPos, surfaceHeight, startZ + (height * tileSize));
            
            Gizmos.DrawLine(transform.position + (transform.rotation * startLocal), 
                            transform.position + (transform.rotation * endLocal));
        }
        for (int z = 0; z <= height; z++)
        {
            float zPos = startZ + (z * tileSize);
            Vector3 startLocal = new Vector3(startX, surfaceHeight, zPos);
            Vector3 endLocal = new Vector3(startX + (width * tileSize), surfaceHeight, zPos);
            
            Gizmos.DrawLine(transform.position + (transform.rotation * startLocal), 
                            transform.position + (transform.rotation * endLocal));
        }
    }
}