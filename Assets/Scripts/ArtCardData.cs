using UnityEngine;

[CreateAssetMenu(menuName = "Kemps/Art Card Data", fileName = "NewArtCardData")]
public class ArtCardData : ScriptableObject
{
    [Header("ID / Name")]
    public string cardId;           // Örn: "MonaLisa_0"
    public string displayName;      // Örn: "Mona Lisa Fragment 1"

    [Header("Artwork Info")]
    public string artworkName;      // Örn: "Mona Lisa"
    public string artistName;       // Örn: "Leonardo da Vinci"
    [TextArea(2, 4)]
    public string description;      // Kýsa açýklama

    [Header("Visuals")]
    public Sprite artworkSprite;    // UI'de göstermek istersen
    public Material cardMaterial;   // 3D kartýn material'i (opsiyonel)
}
