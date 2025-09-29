using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PhilSorter.Localization;

namespace PhilSorter
{
    public static class L10n
    {
        public const string PreferenceKey = "com.philslab.philsorter.lang";
        
        private static string _localizationPath;
        public static string LocalizationPath
        {
            get
            {
                if (string.IsNullOrEmpty(_localizationPath))
                {
                    // Method 1: Find the package path using PackageManager API
                    var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(L10n).Assembly);
                    if (packageInfo != null)
                    {
                        _localizationPath = Path.Combine(packageInfo.assetPath, "Localization").Replace("\\", "/");
                        Debug.Log($"[PhilSorter] Found localization path via PackageInfo: {_localizationPath}");
                    }
                    else
                    {
                        // Method 2: Find this script file and derive the package path from it
                        string[] scriptGuids = AssetDatabase.FindAssets("L10n t:MonoScript");
                        string scriptPath = null;
                        
                        // Find the correct L10n script (ours, not some other L10n in the project)
                        foreach (var guid in scriptGuids)
                        {
                            var testPath = AssetDatabase.GUIDToAssetPath(guid);
                            if (testPath.Contains("com.philslab.philsorter") && testPath.EndsWith("Editor/L10n/L10n.cs"))
                            {
                                scriptPath = testPath;
                                break;
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(scriptPath))
                        {
                            // Extract package root from script path
                            var packageRoot = scriptPath.Substring(0, scriptPath.LastIndexOf("/Editor/L10n/L10n.cs"));
                            _localizationPath = Path.Combine(packageRoot, "Localization").Replace("\\", "/");
                            Debug.Log($"[PhilSorter] Found localization path via script search: {_localizationPath}");
                        }
                        else
                        {
                            // Method 3: Last resort fallback - try common package locations
                            string[] possiblePaths = {
                                "Packages/com.philslab.philsorter/Localization",
                                "Assets/Packages/com.philslab.philsorter/Localization",
                                "Packages/com.philslab.philsort/Localization" // Legacy name fallback
                            };
                            
                            foreach (var testPath in possiblePaths)
                            {
                                if (Directory.Exists(testPath))
                                {
                                    _localizationPath = testPath;
                                    Debug.Log($"[PhilSorter] Found localization path via fallback: {_localizationPath}");
                                    break;
                                }
                            }
                            
                            if (string.IsNullOrEmpty(_localizationPath))
                            {
                                // Absolute last resort
                                _localizationPath = "Packages/com.philslab.philsorter/Localization";
                                Debug.LogWarning($"[PhilSorter] Could not find localization path, using fallback: {_localizationPath}");
                            }
                        }
                    }
                }
                return _localizationPath;
            }
        }

        public static PhilSorterL10n Localization { get; } = new PhilSorterL10n(() => LocalizationPath, "en", PreferenceKey);

        private static GUIContent tempContent;

        public static GUIContent Tr(string localizationKey)
            => TempContent(Localization.Tr(localizationKey));

        public static GUIContent Tr(string localizationKey, string fallback)
            => TempContent(Localization.TryTr(localizationKey) ?? fallback);

        public static string TrStr(string localizationKey) 
            => Localization.Tr(localizationKey);

        public static string TrStr(string localizationKey, string fallback)
            => Localization.TryTr(localizationKey) ?? fallback;

        public static GUIContent TempContent(string text)
        {
            if (tempContent == null)
            {
                tempContent = new GUIContent(text);
            }
            else
            {
                tempContent.text = text;
            }
            tempContent.image = null;
            tempContent.tooltip = null;
            return tempContent;
        }

        public static void DrawLanguagePicker()
        {
            Localization.DrawLanguagePicker();
        }
    }
}
