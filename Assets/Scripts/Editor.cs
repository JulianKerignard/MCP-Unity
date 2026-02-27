using UnityEngine;
using UnityEditor;
using System.IO;

public static class MaterialGenerator
{
    [MenuItem("Tools/TD Generate All Materials")]
    public static void GenerateAllMaterials()
    {
        // Find URP Lit shader
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("URP Lit shader not found!");
            return;
        }

        // Environment
        CreateMat(urpLit, "Assets/Materials/Environment", "M_Ground_Grass", HexColor("A8C686"), 0.2f, 0f);
        CreateMat(urpLit, "Assets/Materials/Environment", "M_Ground_Path", HexColor("E8D5B7"), 0.3f, 0f);
        CreateMat(urpLit, "Assets/Materials/Environment", "M_Zone_Base", HexColor("87CEEB"), 0.1f, 0f);
        CreateMat(urpLit, "Assets/Materials/Environment", "M_Zone_Spawn", HexColor("FFCBA4"), 0.1f, 0f);

        // Towers
        CreateMat(urpLit, "Assets/Materials/Towers", "M_Tower_Basic", HexColor("7EB8DA"), 0.4f, 0.2f);
        CreateMat(urpLit, "Assets/Materials/Towers", "M_Tower_Sniper", HexColor("C4A6E6"), 0.4f, 0.2f);
        CreateMat(urpLit, "Assets/Materials/Towers", "M_Tower_AoE", HexColor("F4A460"), 0.4f, 0.2f);
        CreateMat(urpLit, "Assets/Materials/Towers", "M_Tower_Slow", HexColor("87D9C4"), 0.4f, 0.2f);

        // Enemies
        CreateMat(urpLit, "Assets/Materials/Enemies", "M_Enemy_Basic", HexColor("E88B8B"), 0.3f, 0f);
        CreateMat(urpLit, "Assets/Materials/Enemies", "M_Enemy_Fast", HexColor("F0C674"), 0.3f, 0f);
        CreateMat(urpLit, "Assets/Materials/Enemies", "M_Enemy_Tank", HexColor("B07D62"), 0.2f, 0.1f);
        CreateMat(urpLit, "Assets/Materials/Enemies", "M_Enemy_Boss", HexColor("D45D79"), 0.3f, 0.1f);

        // FX
        CreateMat(urpLit, "Assets/Materials/FX", "M_Projectile", HexColor("FFFFFF"), 0.8f, 0f);
        CreateTransparentMat(urpLit, "Assets/Materials/FX", "M_Preview_Valid", HexColor("A8C686"), 0.5f);
        CreateTransparentMat(urpLit, "Assets/Materials/FX", "M_Preview_Invalid", HexColor("E88B8B"), 0.5f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TD] All 15 materials created successfully!");
    }

    static void CreateMat(Shader shader, string folder, string name, Color color, float smoothness, float metallic)
    {
        EnsureFolder(folder);
        string path = $"{folder}/{name}.mat";
        
        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        
        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"[TD] Created material: {path}");
    }

    static void CreateTransparentMat(Shader shader, string folder, string name, Color color, float alpha)
    {
        EnsureFolder(folder);
        string path = $"{folder}/{name}.mat";
        
        Material mat = new Material(shader);
        color.a = alpha;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0f);
        mat.SetFloat("_Metallic", 0f);
        
        // URP transparent surface
        mat.SetFloat("_Surface", 1); // 0=Opaque, 1=Transparent
        mat.SetFloat("_Blend", 0);   // Alpha blend
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        
        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"[TD] Created transparent material: {path}");
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }

    static void EnsureFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
