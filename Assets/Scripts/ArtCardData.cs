using UnityEngine;

public enum CardGroup
{
    Blue,
    Red,
    Green,
    Yellow
}

[CreateAssetMenu(menuName = "Kemps/Card Data", fileName = "ArtCardData")]
public class ArtCardData : ScriptableObject
{
    [Header("Identity")]
    public int cardId;                 // 0–51 arasý unique
    public string itemName;            // Agate, Crown, Lapis vb.
    public CardGroup group;            // Blue / Red / Green / Yellow

    [Header("Visuals")]
    public Texture2D frontTexture;     // Ön yüz PNG
    public Texture2D backTexture;      // Ortak arka yüz PNG
}
