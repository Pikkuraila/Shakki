using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class LoadoutSlot : MonoBehaviour
{
    public int index;                 // 0..15
    public bool locked;               // esim. lukitse kuninkaan ruutu
    [HideInInspector] public string currentPieceId = "";  // vain n�ytt�� varten

    // halutessasi lis�� highlight-efekti OnPointerEnter/Exit (Physics2D raycast + oma manageri)
}
