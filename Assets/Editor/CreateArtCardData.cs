#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateArtCardData
{
    private const string CardsRoot = "Assets/Resources/Cards";
    private const string DataRoot = "Assets/Resources/Cards/Data";
    private const string BackFolder = "CardBack_PNG"; // senin klasör adı

    [MenuItem("Kemps/Cards/Generate ArtCardData From PNG Folders")]
    public static void GenerateAll()
    {
        if (!AssetDatabase.IsValidFolder(DataRoot))
        {
            Directory.CreateDirectory(DataRoot);
            AssetDatabase.Refresh();
        }

        Texture2D backTexture = LoadBackTexture();
        if (backTexture == null)
        {
            Debug.LogError($"Back texture bulunamadı. Klasör: {CardsRoot}/{BackFolder}");
            return;
        }

        int cardId = 0;

        GenerateForGroup("Blue_CardPNG", CardGroup.Blue, backTexture, ref cardId);
        GenerateForGroup("Red_CardPNG", CardGroup.Red, backTexture, ref cardId);
        GenerateForGroup("Green_CardPNG", CardGroup.Green, backTexture, ref cardId);
        GenerateForGroup("Yellow_CardPNG", CardGroup.Yellow, backTexture, ref cardId);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"ArtCardData üretildi. Toplam kart: {cardId}");
    }

    private static void GenerateForGroup(string folderName, CardGroup group, Texture2D backTexture, ref int cardId)
    {
        string fullPath = $"{CardsRoot}/{folderName}";
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { fullPath });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D front = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (front == null) continue;

            string fileName = Path.GetFileNameWithoutExtension(path);

            ArtCardData data = ScriptableObject.CreateInstance<ArtCardData>();
            data.cardId = cardId++;
            data.group = group;
            data.itemName = CleanItemName(fileName);
            data.frontTexture = front;
            data.backTexture = backTexture;

            string assetPath = $"{DataRoot}/{group}_{data.itemName}.asset";
            AssetDatabase.CreateAsset(data, assetPath);
        }
    }

    private static Texture2D LoadBackTexture()
    {
        string backPath = $"{CardsRoot}/{BackFolder}";
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { backPath });
        if (guids.Length == 0) return null;

        return AssetDatabase.LoadAssetAtPath<Texture2D>(
            AssetDatabase.GUIDToAssetPath(guids[0])
        );
    }

    private static string CleanItemName(string rawName)
    {
        // Blue_Agate -> Agate
        int underscore = rawName.IndexOf('_');
        if (underscore >= 0 && underscore < rawName.Length - 1)
            return rawName.Substring(underscore + 1);

        return rawName;
    }
}

#endif
