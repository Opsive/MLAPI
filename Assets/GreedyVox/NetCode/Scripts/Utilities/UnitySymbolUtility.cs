using UnityEditor;

namespace GreedyVox.NetCode.Utilities
{
    public static class UnitySymbolUtility
    {
        public const string UccAiBD = "ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER_BD_AI";
#if UNITY_EDITOR
        public static string[] GetSymbols() =>
        PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup).Split(';');
        public static void SetSymbols(string[] symbols) =>
        PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, string.Join(";", symbols));
        public static bool HasSymbol(string symbol) =>
        PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup).Contains(symbol);
        public static void AddSymbol(string symbol)
        {
            var current = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            // Add the new symbol to the existing symbols
            if (!current.Contains(symbol))
                PlayerSettings.SetScriptingDefineSymbolsForGroup(
                    EditorUserBuildSettings.selectedBuildTargetGroup, $"{current};{symbol}"
                );
        }
        public static void RemoveSymbol(string symbol)
        {
            var current = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            // Remove the specified symbol and remove any duplicate semicolons
            if (current.Contains(symbol))
                PlayerSettings.SetScriptingDefineSymbolsForGroup(
                    EditorUserBuildSettings.selectedBuildTargetGroup, current.Replace(symbol, "").Replace(";;", ";")
                );
        }
#endif
    }
}