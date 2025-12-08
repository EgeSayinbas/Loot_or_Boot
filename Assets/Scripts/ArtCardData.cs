using UnityEngine;

[CreateAssetMenu(menuName = "Kemps/Art Card Data")]
public class ArtCardData : ScriptableObject
{
    [Header("ID Bilgileri")]
    public string cardId;   // Örn: "MonaLisa_Piece0"
    public string setId;    // Örn: "MonaLisa"
    [Range(0, 3)]
    public int pieceIndex;  // 0–3 arasý parça indexi

    [Header("Görsel")]
    public Texture2D artwork;   // Sprite yapýlabilir// 3D kartýn ön yüzünde kullanacaðýz
}
