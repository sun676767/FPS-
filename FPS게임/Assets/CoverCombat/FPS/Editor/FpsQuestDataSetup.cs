using System.IO;
using LectureFPS;
using UnityEditor;
using UnityEngine;
public static class FpsQuestDataSetup
{
    [MenuItem("Tools/FPS Lecture/6_Create Upgrade Sample Data", false, 6)]
    static void CreateUpgrades()
    {
        const string path = "Assets/FPS/Resources/Data/JsonData/FPSUpgrades.json";
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(UpgradeTable.Defaults(), true));
            AssetDatabase.Refresh();
        }
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
    }
    [MenuItem("Tools/FPS Lecture/4_Create Quest Sample Data", false, 4)]
    static void Build()
    {
        const string path = "Assets/FPS/Resources/Data/JsonData/FPSQuests.json";
        if (File.Exists(path)) { Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(path); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var table = new QuestTable();
        table.quests.Add(new QuestData { id = "training", title = "Target training", objective = "Target", requiredCount = 5, rewardCredits = 100 });
        table.quests.Add(new QuestData { id = "combat", title = "Clear the area", prerequisiteId = "training", objective = "Enemy", requiredCount = 1, rewardCredits = 200 });
        File.WriteAllText(path, JsonUtility.ToJson(table, true));
        AssetDatabase.Refresh(); Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
    }
}

public sealed class FpsUpgradeDataWindow : EditorWindow
{
    const string Path = "Assets/FPS/Resources/Data/JsonData/FPSUpgrades.json";
    UpgradeTable table;
    Vector2 scroll;
    string error;
    [MenuItem("Tools/FPS Lecture/7_Upgrade Data Editor", false, 7)]
    static void Open() => GetWindow<FpsUpgradeDataWindow>("Upgrade Data");
    void OnEnable() => Load();
    void Load()
    {
        error = null;
        try
        {
            table = File.Exists(Path) ? JsonUtility.FromJson<UpgradeTable>(File.ReadAllText(Path)) : UpgradeTable.Defaults();
            if (table == null || !table.Validate()) { table = null; error = "Invalid upgrade data. Fix the JSON file before editing."; }
        }
        catch (System.Exception e) { table = null; error = e.Message; }
    }
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Values apply on the next Play session. Reload reduction is a fraction: 0.08 = 8% per level. Existing JSON is only replaced when Save is pressed.", MessageType.Info);
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        if (GUILayout.Button("Reload from disk")) Load();
        if (table == null) return;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var item in table.upgrades)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(item.id, EditorStyles.boldLabel);
            item.title = EditorGUILayout.TextField("Display name", item.title);
            item.valuePerLevel = EditorGUILayout.FloatField("Value per level", item.valuePerLevel);
            int count = EditorGUILayout.IntSlider("Max level", item.costs.Length, 1, 10);
            if (count != item.costs.Length)
            {
                int previous = item.costs.Length;
                System.Array.Resize(ref item.costs, count);
                for (int i = previous; i < count; i++) item.costs[i] = 100 * (i + 1);
            }
            for (int i = 0; i < item.costs.Length; i++) item.costs[i] = EditorGUILayout.IntField($"Level {i + 1} cost", item.costs[i]);
        }
        EditorGUILayout.EndScrollView();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !table.Validate()))
            if (GUILayout.Button("Validate and Save JSON", GUILayout.Height(32)))
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.WriteAllText(Path, JsonUtility.ToJson(table, true));
                AssetDatabase.Refresh();
                ShowNotification(new GUIContent("Upgrade data saved"));
            }
    }
}
