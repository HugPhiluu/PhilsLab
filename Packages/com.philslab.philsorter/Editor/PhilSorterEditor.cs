using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using PhilSorter.Localization;

// For referencing TargetFolder type
using TargetFolder = SorterConfig.TargetFolder;

public class PhilSorterWindow : EditorWindow
{
    public SorterConfig config;
    private Vector2 scroll;
    private string newDisplayName = "";
    private string search = "";
    private int selectedTab = 0;
    private int selectedCategoryIndex = 0;
    private static readonly string[] tabs = { "Targets", "History", "Settings" };
    public List<string> categories = new List<string> { "Default" };
    private Object lastSelection = null;

    [MenuItem("Window/Phil's Sorter (Config)")]
    public static void ShowWindow()
    {
        GetWindow<PhilSorterWindow>("Phil's Sorter");
    }

    private void OnEnable()
    {
        LoadConfig();
        if (config != null && config.showDebugLogs) Debug.Log("[Phil's Sorter] PhilSorterWindow enabled");
        LoadCategoriesFromTargets();
    }

    private void OnGUI()
    {
        if (config == null)
        {
            EditorGUILayout.HelpBox("SorterConfig not found. Click to create one.", MessageType.Warning);
            if (GUILayout.Button("Create SorterConfig"))
            {
                if (config != null && config.showDebugLogs) Debug.Log("[Phil's Sorter] Creating new SorterConfig from OnGUI");
                LoadConfig();
            }
            return;
        }

        // Prefill Display Name if selection changed and user hasn't typed a custom name
        Object currentSelection = Selection.activeObject;
        if (currentSelection != lastSelection)
        {
            string path = currentSelection != null ? AssetDatabase.GetAssetPath(currentSelection) : null;
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
            {
                // Only prefill if newDisplayName is empty or matches the previous folder name
                string folderName = Path.GetFileName(path);
                bool isEmptyName = string.IsNullOrEmpty(newDisplayName);
                bool matchesPrevious = lastSelection != null && 
                                     newDisplayName == Path.GetFileName(AssetDatabase.GetAssetPath(lastSelection));
                
                if (isEmptyName || matchesPrevious)
                {
                    newDisplayName = folderName;
                }
            }
            lastSelection = currentSelection;
        }

        // Always keep categories in sync
        LoadCategoriesFromTargets();

        selectedTab = GUILayout.Toolbar(selectedTab, tabs);
        EditorGUILayout.Space();

        if (selectedTab == 0) // Targets
        {
            DrawTargetFoldersUI();
        }
        else if (selectedTab == 1) // History
        {
            DrawHistoryUI();
        }
        else if (selectedTab == 2) // Settings
        {
            DrawSettingsUI();
        }
    }

    private void DrawTargetFoldersUI()
    {
        EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("target_folders_configurable"), EditorStyles.boldLabel);
        search = EditorGUILayout.TextField(PhilSorter.L10n.TrStr("search"), search);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        var filtered = config.targetFolders
            .Where(f => string.IsNullOrEmpty(search) || 
                       (f.displayName != null && f.displayName.ToLower().Contains(search.ToLower())) || 
                       (f.path != null && f.path.ToLower().Contains(search.ToLower())))
            .ToList();

        // Show folders grouped and ordered by user category order
        foreach (var cat in categories)
        {
            var catFolders = filtered
                .Where(f => (string.IsNullOrEmpty(f.category) ? "Default" : f.category) == cat)
                .OrderBy(f => f.displayName)
                .ToList();
            if (catFolders.Count == 0) continue;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(cat, EditorStyles.miniBoldLabel);
            foreach (var folder in catFolders)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(EditorGUIUtility.IconContent("Folder Icon"), GUILayout.Width(24), GUILayout.Height(18)))
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(folder.path);
                }
                // Editable Display Name
                string newName = EditorGUILayout.TextField(folder.displayName, GUILayout.Width(120));
                if (newName != folder.displayName)
                {
                    folder.displayName = newName;
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssets();
                }
                EditorGUILayout.LabelField(folder.path, EditorStyles.miniLabel);
                if (GUILayout.Button(PhilSorter.L10n.TrStr("remove"), GUILayout.Width(60)))
                {
                    config.targetFolders.Remove(folder);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("add_new_target_folder"), EditorStyles.boldLabel);
        newDisplayName = EditorGUILayout.TextField(PhilSorter.L10n.TrStr("display_name"), newDisplayName);
        // Category dropdown
        if (categories.Count == 0) categories.Add("Default");
        selectedCategoryIndex = Mathf.Clamp(selectedCategoryIndex, 0, categories.Count - 1);
        selectedCategoryIndex = EditorGUILayout.Popup("Category", selectedCategoryIndex, categories.ToArray());
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(PhilSorter.L10n.TrStr("add_selected_folder")))
        {
            AddSelectedFolder();
        }
        if (GUILayout.Button(PhilSorter.L10n.TrStr("manage_categories"), GUILayout.Width(140)))
        {
            CategoryManagerWindow.ShowWindow(this);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(PhilSorter.L10n.TrStr("tip_add_selected_folder"), MessageType.Info);
        if (GUILayout.Button("Save Config"))
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    // --- History Tab ---
    private void DrawHistoryUI()
    {
        EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("action_history"), EditorStyles.boldLabel);
        if (config.history == null || config.history.Count == 0)
        {
            EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("no_history_yet"));
            return;
        }
        EditorGUILayout.Space();
        foreach (var entry in config.history.OrderByDescending(e => e.timestamp))
        {
            EditorGUILayout.BeginHorizontal();
            string label = $"[{entry.timestamp}] {entry.action}: {entry.path}";
            if (!string.IsNullOrEmpty(entry.extra))
                label += $" → {entry.extra}";
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            if (GUILayout.Button(PhilSorter.L10n.TrStr("jump"), GUILayout.Width(50)))
            {
                string jumpPath = entry.action == "Move" && !string.IsNullOrEmpty(entry.extra) 
                    ? entry.extra 
                    : entry.path;
                    
                if (!string.IsNullOrEmpty(jumpPath))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(jumpPath);
                    if (obj != null)
                        Selection.activeObject = obj;
                    else
                        EditorUtility.DisplayDialog(
                            PhilSorter.L10n.TrStr("jump_failed_title"), 
                            $"Could not find path: {jumpPath}", 
                            PhilSorter.L10n.TrStr("ok"));
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    // Favorites feature removed

    private void DrawSettingsUI()
    {
        // --- Sleek Modern Settings UI ---
        GUILayout.Space(10);
        
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) 
        { 
            fontSize = 18, 
            alignment = TextAnchor.MiddleCenter 
        };
        
        GUIStyle sectionStyle = new GUIStyle(EditorStyles.helpBox) 
        { 
            padding = new RectOffset(16, 16, 12, 12), 
            margin = new RectOffset(0, 0, 0, 8) 
        };
        
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        
        GUIStyle descStyle = new GUIStyle(EditorStyles.miniLabel) 
        { 
            fontSize = 11, 
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } 
        };
        
        Color origColor = GUI.backgroundColor;

        // Title
        EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("settings_title"), titleStyle, GUILayout.Height(32));
        GUILayout.Space(8);
        // Language selection
        PhilSorter.L10n.DrawLanguagePicker();

        // General Section
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("⚙️  General", headerStyle);
        EditorGUI.BeginChangeCheck();
        float oldLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 260f;

        config.jumpToNewFolder = EditorGUILayout.ToggleLeft(PhilSorter.L10n.TrStr("jump_to_new_folder"), config.jumpToNewFolder);
        EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("auto_select_after_move"), descStyle);
        config.confirmBeforeMove = EditorGUILayout.ToggleLeft(PhilSorter.L10n.TrStr("always_confirm_before_move"), config.confirmBeforeMove);
        EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("show_confirm_dialog"), descStyle);
        config.showScriptWarnings = EditorGUILayout.ToggleLeft(PhilSorter.L10n.TrStr("warn_if_scripts"), config.showScriptWarnings);
        EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("extra_warning_scripts"), descStyle);
        config.showPatchDialog = EditorGUILayout.ToggleLeft(PhilSorter.L10n.TrStr("show_patch_dialog"), config.showPatchDialog);
        EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("offer_patch_hardcoded"), descStyle);
        config.showRecentTargets = EditorGUILayout.ToggleLeft(PhilSorter.L10n.TrStr("show_recent_targets"), config.showRecentTargets);
        EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("display_recent_targets"), descStyle);
        EditorGUIUtility.labelWidth = oldLabelWidth;
        EditorGUILayout.EndVertical();

        // Advanced Section
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("🛠️  Advanced", headerStyle);
        EditorGUIUtility.labelWidth = 260f;
        config.showDebugLogs = EditorGUILayout.ToggleLeft(PhilSorter.L10n.TrStr("show_debug_logs"), config.showDebugLogs);
        EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("enable_extra_debug"), descStyle);
        config.enableExperimental = EditorGUILayout.ToggleLeft(PhilSorter.L10n.TrStr("enable_experimental"), config.enableExperimental);
        EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("try_new_features"), descStyle);
        EditorGUIUtility.labelWidth = oldLabelWidth;
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = origColor;

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        // Credits Section
        GUILayout.Space(8);
        GUI.backgroundColor = new Color(0.13f,0.18f,0.22f,0.10f);
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("♥  Credits", headerStyle);
        GUILayout.Space(2);
        EditorGUILayout.LabelField("Phil's Asset Sorter", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 13 });
        EditorGUILayout.LabelField("by Philuu", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
        EditorGUILayout.LabelField("Special thanks to the VRChat and Unity communities!", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 11 });
        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Support Me on Ko-fi♥", GUILayout.Width(180), GUILayout.Height(32)))
        {
            Application.OpenURL("https://ko-fi.com/philuu");
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = origColor;
    }

    private void LoadConfig()
    {
        // Look for existing config anywhere in the project
        string[] guids = AssetDatabase.FindAssets("t:SorterConfig");
        if (guids != null && guids.Length > 0)
        {
            string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            config = AssetDatabase.LoadAssetAtPath<SorterConfig>(foundPath);
        }
        else
        {
            // Create new config next to this script (not hardcoded)
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string dir = Path.GetDirectoryName(scriptPath).Replace("\\", "/");
            string configPath = dir + "/PhilSorterConfig.asset";
            config = ScriptableObject.CreateInstance<SorterConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private void AddSelectedFolder()
    {
        Object[] selectedObjects = Selection.objects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select at least one folder in the Project window.", PhilSorter.L10n.TrStr("ok"));
            if (config != null && config.showDebugLogs) Debug.LogWarning("[Phil's Sorter] AddSelectedFolder: No selection");
            return;
        }

        List<string> added = new List<string>();
        List<string> already = new List<string>();
        List<string> invalid = new List<string>();
        string category = (categories.Count > selectedCategoryIndex && selectedCategoryIndex >= 0) ? categories[selectedCategoryIndex] : "Default";

        foreach (var obj in selectedObjects)
        {
            if (obj == null) continue;
            string path = AssetDatabase.GetAssetPath(obj);
            if (!AssetDatabase.IsValidFolder(path))
            {
                invalid.Add(path);
                if (config != null && config.showDebugLogs) Debug.LogWarning($"[Phil's Sorter] AddSelectedFolder: Not a valid folder: {path}");
                continue;
            }
            if (config.targetFolders.Any(f => f.path == path))
            {
                already.Add(path);
                if (config != null && config.showDebugLogs) Debug.LogWarning($"[Phil's Sorter] AddSelectedFolder: Already in targets: {path}");
                continue;
            }
            string displayName = string.IsNullOrEmpty(newDisplayName) || selectedObjects.Length > 1 ? Path.GetFileName(path) : newDisplayName;
            config.targetFolders.Add(new TargetFolder
            {
                path = path,
                displayName = displayName,
                category = category
            });
            added.Add(path);
            if (config != null && config.showDebugLogs) Debug.Log($"[Phil's Sorter] Added to targets: {path} (category: {category})");

            // Log to history
            if (config.history == null) config.history = new List<SorterConfig.HistoryEntry>();
            config.history.Add(new SorterConfig.HistoryEntry {
                action = "SetTarget",
                path = path,
                extra = "",
                timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        if (added.Count > 0)
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }


        newDisplayName = "";
    }

    // Loads categories in user order: Default, then customCategories, then any from targetFolders not present
    public void LoadCategoriesFromTargets()
    {
        categories = new List<string>();
        categories.Add("Default");
        if (config.customCategories != null)
        {
            foreach (var cat in config.customCategories)
                if (!string.IsNullOrWhiteSpace(cat) && cat != "Default" && !categories.Contains(cat))
                    categories.Add(cat);
        }
        // Add any categories from targetFolders not already present
        foreach (var cat in config.targetFolders.Select(f => string.IsNullOrEmpty(f.category) ? "Default" : f.category))
        {
            if (!categories.Contains(cat))
                categories.Add(cat);
        }
        if (config != null && config.showDebugLogs) Debug.Log($"[Phil's Sorter] Loaded categories: {string.Join(", ", categories)}");
    }

    // Category manager window with robust drag-and-drop using ReorderableList
    public class CategoryManagerWindow : EditorWindow
    {
        private static PhilSorterWindow parentWindow;
        private string newCat = "";
        private ReorderableList reorderableList;
        public static void ShowWindow(PhilSorterWindow parent)
        {
            parentWindow = parent;
            var win = GetWindow<CategoryManagerWindow>(true, "Manage Categories");
            win.minSize = new Vector2(300, 350);
        }
        // For editing category names in-place
        private int editingIndex = -1;
        private string editingValue = "";
        private void OnEnable()
        {
            reorderableList = new ReorderableList(parentWindow.config.customCategories, typeof(string), true, false, false, false);
            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                float textWidth = rect.width - 80;
                // Inline editing
                if (editingIndex == index)
                {
                    GUI.SetNextControlName("CategoryEditField");
                    editingValue = EditorGUI.TextField(new Rect(rect.x + 8, rect.y + 2, textWidth, EditorGUIUtility.singleLineHeight), editingValue);
                    // Commit on enter or focus loss
                    Event e = Event.current;
                    bool commit = (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return) || (e.type == EventType.MouseDown && !new Rect(rect.x + 8, rect.y + 2, textWidth, EditorGUIUtility.singleLineHeight).Contains(e.mousePosition));
                    if (commit)
                    {
                        string oldName = parentWindow.config.customCategories[index];
                        string newName = editingValue.Trim();
                        if (!string.IsNullOrEmpty(newName) && newName != "Default" && !parentWindow.categories.Contains(newName))
                        {
                            // Update all targetFolders using this category
                            foreach (var tf in parentWindow.config.targetFolders)
                                if (tf.category == oldName) tf.category = newName;
                            parentWindow.config.customCategories[index] = newName;
                            EditorUtility.SetDirty(parentWindow.config);
                            AssetDatabase.SaveAssets();
                            parentWindow.LoadCategoriesFromTargets();
                        }
                        editingIndex = -1;
                        editingValue = "";
                        GUI.FocusControl(null);
                        Event.current.Use();
                    }
                }
                else
                {
                    // Draw as label, click to edit
                    if (GUI.Button(new Rect(rect.x + 8, rect.y + 2, textWidth, EditorGUIUtility.singleLineHeight), parentWindow.config.customCategories[index], EditorStyles.label))
                    {
                        editingIndex = index;
                        editingValue = parentWindow.config.customCategories[index];
                        GUI.FocusControl("CategoryEditField");
                    }
                }
                if (GUI.Button(new Rect(rect.x + rect.width - 65, rect.y + 2, 56, EditorGUIUtility.singleLineHeight), "Remove"))
                {
                    string cat = parentWindow.config.customCategories[index];
                    foreach (var tf in parentWindow.config.targetFolders)
                    {
                        if (tf.category == cat)
                            tf.category = "Default";
                    }
                    parentWindow.config.customCategories.RemoveAt(index);
                    EditorUtility.SetDirty(parentWindow.config);
                    AssetDatabase.SaveAssets();
                    parentWindow.LoadCategoriesFromTargets();
                    if (editingIndex == index) { editingIndex = -1; editingValue = ""; }
                }
            };
            reorderableList.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
            reorderableList.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, "Custom Categories", EditorStyles.boldLabel);
            };
            reorderableList.onReorderCallback = (ReorderableList list) =>
            {
                EditorUtility.SetDirty(parentWindow.config);
                AssetDatabase.SaveAssets();
                parentWindow.LoadCategoriesFromTargets();
            };
        }
        private void OnGUI()
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("category_manager"), EditorStyles.boldLabel, GUILayout.Height(22));
            EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("drag_to_reorder"), EditorStyles.miniLabel);
            GUILayout.Space(8);
            // Default category (label, not text field)
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("default_category"), EditorStyles.boldLabel, GUILayout.Width(180));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
            // ReorderableList, constrained width
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical(GUILayout.Width(320));
            if (reorderableList == null)
                OnEnable();
            reorderableList.DoLayoutList();
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            // Add new category
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.LabelField(PhilSorter.L10n.TrStr("new_category"), GUILayout.Width(100));
            newCat = EditorGUILayout.TextField(newCat, GUILayout.Width(140));
            if (GUILayout.Button(PhilSorter.L10n.TrStr("add"), GUILayout.Width(60)))
            {
                if (!string.IsNullOrWhiteSpace(newCat) && !parentWindow.categories.Contains(newCat))
                {
                    if (parentWindow.config.customCategories == null)
                        parentWindow.config.customCategories = new List<string>();
                    parentWindow.config.customCategories.Add(newCat);
                    EditorUtility.SetDirty(parentWindow.config);
                    AssetDatabase.SaveAssets();
                    parentWindow.LoadCategoriesFromTargets();
                    newCat = "";
                    
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            // Close button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(PhilSorter.L10n.TrStr("close"), GUILayout.Width(80), GUILayout.Height(26)))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
