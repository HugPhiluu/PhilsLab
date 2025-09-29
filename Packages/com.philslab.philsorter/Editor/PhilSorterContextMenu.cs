using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PhilSorter.Localization;

public static class PhilSorterContextMenu
{
    private static SorterConfig config;

    private static void LoadConfig()
    {
        // Find config next to this script, not in root, and never hardcode path
        string[] scriptGuids = AssetDatabase.FindAssets("PhilSorterEditor t:MonoScript");
        string scriptPath = scriptGuids.Length > 0 ? AssetDatabase.GUIDToAssetPath(scriptGuids[0]) : null;
        string dir = scriptPath != null ? Path.GetDirectoryName(scriptPath).Replace("\\", "/") : "Assets";
        string configPath = dir + "/PhilSorterConfig.asset";
        config = AssetDatabase.LoadAssetAtPath<SorterConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<SorterConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (config != null && config.showDebugLogs) Debug.Log($"[Phil's Sorter] Created new SorterConfig at: {configPath}");
        }
        if (config != null && config.showDebugLogs) Debug.Log($"[Phil's Sorter] Loaded config from: {configPath}");
    }

    [MenuItem("Assets/Phil's Sorter/Move To...", true)]
    private static bool ValidateMoveMenu()
    {
        bool valid = Selection.activeObject != null && 
                    AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
        if (config != null && config.showDebugLogs) 
            Debug.Log($"[Phil's Sorter] ValidateMoveMenu: {valid}");
        return valid;
    }

    [MenuItem("Assets/Phil's Sorter/Move To...")]
    private static void ShowMoveMenu()
    {
        LoadConfig();
        if (config != null && config.showDebugLogs) Debug.Log("[Phil's Sorter] ShowMoveMenu called");
        if (config == null || config.targetFolders.Count == 0)
        {
            if (config != null && config.showDebugLogs) 
                Debug.LogWarning("[Phil's Sorter] No target folders configured.");
            
            EditorUtility.DisplayDialog(
                "Phil's Sorter", 
                "No target folders configured. Open Window/Phil's Sorter (Config) to add targets.", 
                "OK");
            return;
        }
        PhilSorterMoveToWindow.ShowWindow(config);
    }

    // Modern popup window for Move To
    private class PhilSorterMoveToWindow : EditorWindow
    {
        private static SorterConfig config;
        private static string selectedFolderPath;
        private Vector2 scroll;
        private string search = "";
        private GUIStyle categoryStyle;
        private GUIStyle folderStyle;
        private GUIStyle recentStyle;

        public static void ShowWindow(SorterConfig cfg)
        {
            config = cfg;
            selectedFolderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            var win = GetWindow<PhilSorterMoveToWindow>(true, "Move Folder To...");
            win.minSize = new Vector2(400, 500);
            win.position = new Rect(Screen.width / 2, Screen.height / 2, 400, 500);
        }

        private void OnEnable()
        {
            categoryStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            folderStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0)
            };
            recentStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic };
            if (config != null && config.showDebugLogs) Debug.Log("[Phil's Sorter] PhilSorterMoveToWindow enabled");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("move_to") + " '" + Path.GetFileName(selectedFolderPath) + "' ", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            search = EditorGUILayout.TextField(PhilSorter.L10n.TrStr("search"), search);
            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            // Use the same order as categories list (user order)
            List<string> categories;
            if (config == null)
            {
                categories = new List<string>();
            }
            else
            {
                categories = new List<string>(new[] { "Default" })
                    .Concat(config.customCategories ?? new List<string>())
                    .Concat(config.targetFolders.Select(f => 
                        string.IsNullOrEmpty(f.category) ? "Default" : f.category))
                    .Distinct()
                    .ToList();
            }
            
            foreach (var category in categories)
            {
                EditorGUILayout.LabelField(category, categoryStyle);
                
                var folders = config.targetFolders.Where(f => 
                    (string.IsNullOrEmpty(search) || 
                     (f.displayName != null && f.displayName.ToLower().Contains(search.ToLower())) || 
                     (f.path != null && f.path.ToLower().Contains(search.ToLower()))) && 
                    (f.category == category)).ToList();
                foreach (var folder in folders)
                {
                    Rect rowRect = EditorGUILayout.GetControlRect(false, 32);
                    bool isHover = rowRect.Contains(Event.current.mousePosition);
                    Color origColor = GUI.backgroundColor;
                    if (isHover)
                        GUI.backgroundColor = new Color(0.25f, 0.5f, 1f, 0.18f);
                    // Draw background with rounded corners
                    EditorGUI.DrawRect(rowRect, isHover ? new Color(0.25f, 0.5f, 1f, 0.10f) : new Color(0.18f, 0.18f, 0.18f, 0.04f));
                    // Draw border
                    Handles.BeginGUI();
                    Handles.color = isHover ? new Color(0.25f, 0.5f, 1f, 0.25f) : new Color(0.2f, 0.2f, 0.2f, 0.10f);
                    Vector3[] borderPoints = {
                        new Vector3(rowRect.x, rowRect.y),
                        new Vector3(rowRect.x + rowRect.width, rowRect.y),
                        new Vector3(rowRect.x + rowRect.width, rowRect.y + rowRect.height),
                        new Vector3(rowRect.x, rowRect.y + rowRect.height),
                        new Vector3(rowRect.x, rowRect.y)
                    };
                    Handles.DrawAAPolyLine(2f, borderPoints);
                    Handles.EndGUI();
                    // Button overlay
                    if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                    {
                        MoveSelectedFolderStatic(folder.path);
                        Close();
                        return;
                    }
                    // Icon
                    Rect iconRect = new Rect(rowRect.x + 8, rowRect.y + 6, 20, 20);
                    GUI.Label(iconRect, EditorGUIUtility.IconContent("Folder Icon"));
                    // Name and path
                    float textX = iconRect.xMax + 8;
                    float textWidth = rowRect.width - (textX - rowRect.x) - 8;
                    Rect nameRect = new Rect(textX, rowRect.y + 4, textWidth, 16);
                    Rect pathRect = new Rect(textX, rowRect.y + 18, textWidth, 14);
                    GUI.Label(nameRect, new GUIContent(folder.displayName, folder.path), folderStyle);
                    GUI.Label(pathRect, new GUIContent(ShortenPath(folder.path, (int)textWidth, folderStyle)), EditorStyles.miniLabel);
                    GUI.backgroundColor = origColor;
                }
                EditorGUILayout.Space();
            }

            if (config.recentTargets != null && config.recentTargets.Count > 0)
            {
                EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("recent"), recentStyle);
                foreach (var recent in config.recentTargets)
                {
                    Rect rowRect = EditorGUILayout.GetControlRect(false, 32);
                    bool isHover = rowRect.Contains(Event.current.mousePosition);
                    Color origColor = GUI.backgroundColor;
                    if (isHover)
                        GUI.backgroundColor = new Color(0.25f, 0.5f, 1f, 0.13f);
                    EditorGUI.DrawRect(rowRect, isHover ? new Color(0.25f, 0.5f, 1f, 0.08f) : new Color(0.18f, 0.18f, 0.18f, 0.03f));
                    Handles.BeginGUI();
                    Handles.color = isHover ? new Color(0.25f, 0.5f, 1f, 0.18f) : new Color(0.2f, 0.2f, 0.2f, 0.08f);
                    Vector3[] points = {
                        new Vector3(rowRect.x, rowRect.y),
                        new Vector3(rowRect.x + rowRect.width, rowRect.y),
                        new Vector3(rowRect.x + rowRect.width, rowRect.y + rowRect.height),
                        new Vector3(rowRect.x, rowRect.y + rowRect.height),
                        new Vector3(rowRect.x, rowRect.y)
                    };
                    Handles.DrawAAPolyLine(2f, points);
                    Handles.EndGUI();
                    if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                    {
                        MoveSelectedFolderStatic(recent);
                        Close();
                        return;
                    }
                    Rect iconRect = new Rect(rowRect.x + 8, rowRect.y + 6, 20, 20);
                    GUI.Label(iconRect, EditorGUIUtility.IconContent("Folder Icon"));
                    float textX = iconRect.xMax + 8;
                    float textWidth = rowRect.width - (textX - rowRect.x) - 8;
                    Rect nameRect = new Rect(textX, rowRect.y + 4, textWidth, 16);
                    Rect pathRect = new Rect(textX, rowRect.y + 18, textWidth, 14);
                    GUI.Label(nameRect, new GUIContent(Path.GetFileName(recent), recent), folderStyle);
                    GUI.Label(pathRect, new GUIContent(ShortenPath(recent, (int)textWidth, folderStyle)), EditorStyles.miniLabel);
                    GUI.backgroundColor = origColor;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();
            if (GUILayout.Button(PhilSorter.L10n.TrStr("cancel")))
            {
                Close();
            }
        }

        // Helper to shorten long paths with ellipsis
        private static string ShortenPath(string path, int maxWidth, GUIStyle style)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string ellipsis = "...";
            if (style.CalcSize(new GUIContent(path)).x <= maxWidth) return path;
            string[] parts = path.Split('/');
            if (parts.Length < 2) return path;
            string result = parts[0] + "/" + ellipsis + "/" + parts[parts.Length - 1];
            while (style.CalcSize(new GUIContent(result)).x > maxWidth && parts.Length > 2)
            {
                parts = parts.Skip(1).ToArray();
                result = ellipsis + "/" + string.Join("/", parts);
            }
            return result;
        }

        private static void MoveSelectedFolderStatic(string targetPath)
        {
            string folderPath = selectedFolderPath;
            string folderName = Path.GetFileName(folderPath);
            string destination = Path.Combine(targetPath, folderName).Replace("\\", "/");

            // --- Confirm before move ---
            if (config.confirmBeforeMove)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Phil's Sorter",
                    string.Format(PhilSorter.L10n.TrStr("confirm_move"), folderName, destination),
                    PhilSorter.L10n.TrStr("move"),
                    PhilSorter.L10n.TrStr("cancel"),
                    PhilSorter.L10n.TrStr("move_and_dont_ask")
                );
                if (choice == 1) return; // Cancel
                if (choice == 2)
                {
                    config.confirmBeforeMove = false;
                    UnityEditor.EditorUtility.SetDirty(config);
                    UnityEditor.AssetDatabase.SaveAssets();
                }
                // else (0) Move: continue
            }

            // --- Script/hardcoded path warning ---
            List<string> hardcodedPaths;
            bool hasHardcoded = FolderContainsHardcodedPaths(folderPath, out hardcodedPaths);
            var csFiles = Directory.Exists(folderPath) ? Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories) : new string[0];

            if (hasHardcoded && config.showPatchDialog)
            {
                string msg = "Warning: The following scripts contain hardcoded asset paths or Application.dataPath references:\n\n" +
                             string.Join("\n", hardcodedPaths.Take(10)) +
                             (hardcodedPaths.Count > 10 ? $"\n...and {hardcodedPaths.Count - 10} more." : "") +
                             "\n\nMoving this folder may break these scripts. Alternatively, Phil's Sorter can try to patch the hardcoded paths for you.\n\nWhat would you like to do?";
                int choice = EditorUtility.DisplayDialogComplex(
                    "Phil's Sorter - Hardcoded Paths Detected",
                    msg,
                    "Patch and Move", // 0
                    "Cancel",         // 1
                    "Move Anyway"     // 2
                );
                if (choice == 1) return; // Cancel
                if (choice == 0) {
                    PatchHardcodedPaths(folderPath, destination);
                }
                // else (2) Move Anyway: continue
            }
            else if (csFiles.Length > 0 && config.showScriptWarnings)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Phil's Sorter - Script Warning",
                    "This folder contains C# scripts. Moving it may break references in your project or third-party tools. Proceed anyway?",
                    "Move Anyway", "Cancel"
                );
                if (!proceed) return;
            }

            // --- Move logic ---

            if (!AssetDatabase.IsValidFolder(targetPath))
            {
                Debug.LogError("Target folder does not exist: " + targetPath);
                EditorUtility.DisplayDialog("Phil's Sorter", "Target folder does not exist: " + targetPath, "OK");
                return;
            }
            if (AssetDatabase.IsValidFolder(destination))
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Phil's Sorter - Duplicate Folder",
                    $"A folder named '{folderName}' already exists in the target. What would you like to do?\n\n- Cancel: Do nothing.\n- Merge: Move contents into the existing folder (overwrites files with same name).\n- Rename: Enter a new name for the folder.",
                    "Merge", // 0
                    "Cancel", // 1
                    "Rename" // 2
                );
                if (choice == 1) return; // Cancel
                if (choice == 0) // Merge
                {
                    // Move all contents from folderPath into destination, overwriting files with same name
                    MergeFolders(folderPath, destination);
                    AssetDatabase.DeleteAsset(folderPath); // Remove the old folder
                    Debug.Log($"Merged folder '{folderPath}' into '{destination}'");
                    // Log to history
                    if (config.history == null) config.history = new List<SorterConfig.HistoryEntry>();
                    config.history.Add(new SorterConfig.HistoryEntry {
                        action = "Move",
                        path = folderPath,
                        extra = destination,
                        timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                    EditorUtility.SetDirty(config);
                    AssetDatabase.Refresh();
                    if (config.jumpToNewFolder)
                        JumpToFolderInProjectWindow(destination);
                    if (config.showDebugLogs)
                        Debug.Log($"[Phil's Sorter] Jumped to folder: {destination}");
                    return;
                }
                if (choice == 2) // Rename
                {
                    string newName = folderName;
                    bool valid = false;
                    while (!valid)
                    {
                        newName = EditorUtility.SaveFolderPanel("Rename Folder", Path.GetDirectoryName(destination), folderName);
                        if (string.IsNullOrEmpty(newName)) return; // Cancelled
                        newName = newName.Replace("\\", "/");
                        if (newName.StartsWith(Application.dataPath))
                        {
                            newName = "Assets" + newName.Substring(Application.dataPath.Length);
                        }
                        if (!AssetDatabase.IsValidFolder(newName))
                        {
                            valid = true;
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Phil's Sorter", $"A folder named '{Path.GetFileName(newName)}' already exists. Please choose another name.", "OK");
                        }
                    }
                    string error = AssetDatabase.MoveAsset(folderPath, newName);
                    if (!string.IsNullOrEmpty(error))
                        Debug.LogError("Failed to move folder: " + error);
                    else
                    {
                        Debug.Log($"Moved folder to: {newName}");
                        // Log to history
                        if (config.history == null) config.history = new List<SorterConfig.HistoryEntry>();
                        config.history.Add(new SorterConfig.HistoryEntry {
                            action = "Move",
                            path = folderPath,
                            extra = newName,
                            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                        EditorUtility.SetDirty(config);
                        AssetDatabase.Refresh();
                        if (config.jumpToNewFolder)
                            JumpToFolderInProjectWindow(newName);
                        if (config.showDebugLogs)
                            Debug.Log($"[Phil's Sorter] Jumped to folder: {newName}");
                    }
                    return;
                }
            }
            // Default: no duplicate, just move
            string moveError = AssetDatabase.MoveAsset(folderPath, destination);
            if (!string.IsNullOrEmpty(moveError))
                Debug.LogError("Failed to move folder: " + moveError);
            else
            {
                Debug.Log($"Moved folder to: {destination}");
                // Log to history
                if (config.history == null) config.history = new List<SorterConfig.HistoryEntry>();
                config.history.Add(new SorterConfig.HistoryEntry {
                    action = "Move",
                    path = folderPath,
                    extra = destination,
                    timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
                EditorUtility.SetDirty(config);
                AssetDatabase.Refresh();
                if (config.jumpToNewFolder)
                    JumpToFolderInProjectWindow(destination);
                if (config.showDebugLogs)
                    Debug.Log($"[Phil's Sorter] Jumped to folder: {destination}");
            }
            // No extra AssetDatabase.Refresh() here
        }

        private static void AddRecentTargetStatic(string path)
        {
            LoadConfig();
            if (config.recentTargets == null)
                config.recentTargets = new List<string>();
            if (!config.recentTargets.Contains(path))
            {
                config.recentTargets.Insert(0, path);
                if (config.recentTargets.Count > 10)
                    config.recentTargets.RemoveAt(config.recentTargets.Count - 1);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }
    }

    private static void MoveSelectedFolder(string targetPath)
    {
        string folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        string folderName = Path.GetFileName(folderPath);
        string destination = Path.Combine(targetPath, folderName).Replace("\\", "/");

        // --- Confirm before move ---
        if (config.confirmBeforeMove)
        {
            if (!EditorUtility.DisplayDialog("Phil's Sorter", string.Format(PhilSorter.L10n.TrStr("confirm_move"), folderName, destination), PhilSorter.L10n.TrStr("move"), PhilSorter.L10n.TrStr("cancel")))
                return;
        }

        // --- Script/hardcoded path warning ---
        List<string> hardcodedPaths;
        bool hasHardcoded = FolderContainsHardcodedPaths(folderPath, out hardcodedPaths);
        var csFiles = Directory.Exists(folderPath) ? Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories) : new string[0];

        if (hasHardcoded && config.showPatchDialog)
        {
            string msg = "Warning: The following scripts contain hardcoded asset paths or Application.dataPath references:\n\n" +
                         string.Join("\n", hardcodedPaths.Take(10)) +
                         (hardcodedPaths.Count > 10 ? $"\n...and {hardcodedPaths.Count - 10} more." : "") +
                         "\n\nMoving this folder may break these scripts. Alternatively, Phil's Sorter can try to patch the hardcoded paths for you.\n\nWhat would you like to do?";
            int choice = EditorUtility.DisplayDialogComplex(
                "Phil's Sorter - Hardcoded Paths Detected",
                msg,
                "Patch and Move", // 0
                "Cancel",         // 1
                "Move Anyway"     // 2
            );
            if (choice == 1) return; // Cancel
            if (choice == 0)
            {
                PatchHardcodedPaths(folderPath, destination);
            }
            // else (2) Move Anyway: continue
        }
        else if (csFiles.Length > 0 && config.showScriptWarnings)
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Phil's Sorter - Script Warning",
                "This folder contains C# scripts. Moving it may break references in your project or third-party tools. Proceed anyway?",
                "Move Anyway", "Cancel"
            );
            if (!proceed) return;
        }

        // --- Move logic ---

        if (!AssetDatabase.IsValidFolder(targetPath))
        {
            Debug.LogError("Target folder does not exist: " + targetPath);
            return;
        }
        if (AssetDatabase.IsValidFolder(destination))
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Phil's Sorter - Duplicate Folder",
                $"A folder named '{folderName}' already exists in the target. What would you like to do?\n\n- Cancel: Do nothing.\n- Merge: Move contents into the existing folder (overwrites files with same name).\n- Rename: Enter a new name for the folder.",
                "Merge", // 0
                "Cancel", // 1
                "Rename" // 2
            );
            if (choice == 1) return; // Cancel
            if (choice == 0) // Merge
            {
                MergeFolders(folderPath, destination);
                AssetDatabase.DeleteAsset(folderPath);
                Debug.Log($"Merged folder '{folderPath}' into '{destination}'");
                AddRecentTarget(targetPath);
                AssetDatabase.Refresh();
                if (config.jumpToNewFolder)
                    JumpToFolderInProjectWindow(destination);
                if (config.showDebugLogs)
                    Debug.Log($"[Phil's Sorter] Jumped to folder: {destination}");
                return;
            }
            if (choice == 2) // Rename
            {
                string newName = folderName;
                bool valid = false;
                while (!valid)
                {
                    newName = EditorUtility.SaveFolderPanel("Rename Folder", Path.GetDirectoryName(destination), folderName);
                    if (string.IsNullOrEmpty(newName)) return; // Cancelled
                    newName = newName.Replace("\\", "/");
                    if (newName.StartsWith(Application.dataPath))
                    {
                        newName = "Assets" + newName.Substring(Application.dataPath.Length);
                    }
                    if (!AssetDatabase.IsValidFolder(newName))
                    {
                        valid = true;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Phil's Sorter", $"A folder named '{Path.GetFileName(newName)}' already exists. Please choose another name.", "OK");
                    }
                }
                string error = AssetDatabase.MoveAsset(folderPath, newName);
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError("Failed to move folder: " + error);
                else
                {
                    Debug.Log($"Moved folder to: {newName}");
                    AddRecentTarget(targetPath);
                    AssetDatabase.Refresh();
                    if (config.jumpToNewFolder)
                        JumpToFolderInProjectWindow(newName);
                    if (config.showDebugLogs)
                        Debug.Log($"[Phil's Sorter] Jumped to folder: {newName}");
                }
                return;
            }
        }
        // Default: no duplicate, just move
        string moveError = AssetDatabase.MoveAsset(folderPath, destination);
        if (!string.IsNullOrEmpty(moveError))
            Debug.LogError("Failed to move folder: " + moveError);
        else
        {
            Debug.Log($"Moved folder to: {destination}");
            AddRecentTarget(targetPath);
            AssetDatabase.Refresh();
            if (config.jumpToNewFolder)
                JumpToFolderInProjectWindow(destination);
            if (config.showDebugLogs)
                Debug.Log($"[Phil's Sorter] Jumped to folder: {destination}");
        }
    }

    // Helper to merge contents of two folders (source into destination, overwriting files with same name)
    private static void MergeFolders(string source, string destination)
    {
        source = source.Replace("\\", "/");
        destination = destination.Replace("\\", "/");
        if (config != null && config.showDebugLogs) Debug.Log($"[Phil's Sorter] Merging folders: {source} -> {destination}");
        if (!Directory.Exists(source) || !Directory.Exists(destination)) {
            if (config != null && config.showDebugLogs) Debug.LogWarning($"[Phil's Sorter] MergeFolders: Source or destination does not exist");
            return;
        }
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relPath = file.Substring(source.Length).TrimStart('/', '\\');
            string destFile = Path.Combine(destination, relPath).Replace("\\", "/");
            string destDir = Path.GetDirectoryName(destFile);
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
            File.Copy(file, destFile, true);
            if (config != null && config.showDebugLogs) Debug.Log($"[Phil's Sorter] Copied file: {file} -> {destFile}");
        }
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            string relPath = dir.Substring(source.Length).TrimStart('/', '\\');
            string destDir = Path.Combine(destination, relPath).Replace("\\", "/");
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
            if (config != null && config.showDebugLogs) Debug.Log($"[Phil's Sorter] Ensured directory: {destDir}");
        }
    }

    // --- Helper: Reliably select a folder in the Project window after move/refresh ---
    private static void JumpToFolderInProjectWindow(string folderPath)
    {
        // Use delayCall to ensure selection happens after refresh and UI update
        EditorApplication.delayCall += () =>
        {
            var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
            if (folderAsset != null)
            {
                Selection.activeObject = folderAsset;
                // Optionally, also ping the folder
                EditorGUIUtility.PingObject(folderAsset);
            }
        };
    }

    private static void AddRecentTarget(string path)
    {
        LoadConfig();
        if (config.recentTargets == null)
            config.recentTargets = new List<string>();
        if (!config.recentTargets.Contains(path))
        {
            config.recentTargets.Insert(0, path);
            if (config.recentTargets.Count > 10)
                config.recentTargets.RemoveAt(config.recentTargets.Count - 1);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }

    // --- Helper: Check for hardcoded asset paths in .cs files ---
    private static bool FolderContainsHardcodedPaths(string folderPath, out List<string> foundPaths)
    {
        foundPaths = new List<string>();
        if (!Directory.Exists(folderPath)) {
            if (config != null && config.showDebugLogs) Debug.LogWarning($"[Phil's Sorter] FolderContainsHardcodedPaths: Directory does not exist: {folderPath}");
            return false;
        }
        var csFiles = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);
        var pathPattern = new System.Text.RegularExpressions.Regex(@"""Assets/[^""]+""|Application\.dataPath", System.Text.RegularExpressions.RegexOptions.Compiled);
        foreach (var file in csFiles)
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var match = pathPattern.Match(lines[i]);
                if (match.Success)
                {
                    foundPaths.Add($"{Path.GetFileName(file)} (line {i + 1}): {match.Value}");
                    if (config != null && config.showDebugLogs) Debug.Log($"[Phil's Sorter] Hardcoded path found in: {file} (line {i + 1}): {match.Value}");
                }
            }
        }
        if (config != null && config.showDebugLogs) Debug.Log($"[Phil's Sorter] FolderContainsHardcodedPaths: Found {foundPaths.Count} hardcoded paths");
        return foundPaths.Count > 0;
    }

    // --- Helper: Patch hardcoded asset paths in .cs files from old folder to new destination ---
    private static void PatchHardcodedPaths(string oldRoot, string newRoot)
    {
        var csFiles = Directory.GetFiles(oldRoot, "*.cs", SearchOption.AllDirectories);
        oldRoot = oldRoot.Replace("\\", "/");
        newRoot = newRoot.Replace("\\", "/");
        int patchCount = 0;
        if (config != null && config.showDebugLogs) Debug.Log($"[Phil's Sorter] Patching hardcoded paths from {oldRoot} to {newRoot} in {csFiles.Length} files");
        foreach (var file in csFiles)
        {
            string text = File.ReadAllText(file);
            string patched = text.Replace(oldRoot, newRoot);
            if (text != patched)
            {
                File.WriteAllText(file, patched);
                patchCount++;
                if (config != null && config.showDebugLogs) Debug.Log($"[Phil's Sorter] Patched file: {file}");
            }
        }
        if (config != null && config.showDebugLogs) Debug.Log($"[Phil's Sorter] Patched {patchCount} scripts for hardcoded paths");
        EditorUtility.DisplayDialog("Phil's Sorter - Patch Complete",
            string.Format(PhilSorter.L10n.TrStr("patch_complete"), patchCount), PhilSorter.L10n.TrStr("ok"));
    }
}

