using UnityEngine;
using UnityEngine.InputSystem; // Menggunakan New Input System

public class WorldRotateDrag : MonoBehaviour
{
    [Header("Drag Rotation Settings")]
    public float rotationSpeed = 0.2f; // Kecepatan rotasi saat mouse digeser

    void Update()
    {
        // Pastikan Mouse terdeteksi
        if (Mouse.current == null) return;

        // Cek apakah klik kiri mouse sedang ditekan dan digeser
        if (Mouse.current.leftButton.isPressed)
        {
            // Ambil nilai pergerakan delta mouse
            Vector2 delta = Mouse.current.delta.ReadValue();

            // Jika mouse digeser secara horizontal (sumbu X), putar world pada sumbu Y
            if (Mathf.Abs(delta.x) > 0.01f)
            {
                transform.Rotate(Vector3.up, -delta.x * rotationSpeed, Space.World);
            }
        }
    }
}