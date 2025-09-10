#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Outernet.Client
{
    public class EditorSettings : ScriptableObject
    {
        private static EditorSettings _instance;

        // Development: http://52.21.202.250;
        // Development2: http://13.219.25.192;
        // Production: http://34.206.6.20;
        // Beta: http://52.200.81.198;
        public string environmentURL = "http://52.200.81.198";

        // Development: public
        // Production: public
        // Beta: dev2
        // Development2: dev2
        public string environmentSchema = "dev2";

        public LogGroup enabledLogGroups = ~LogGroup.None; // Enable all log groups
        public LogLevel logLevel = LogLevel.Info;
        public LogLevel stackTraceLevel = LogLevel.Warn;

        public bool overridePlerionBaseUrl = false;

        [ConditionalProperty(nameof(overridePlerionBaseUrl), true)]
        public string plerionAPIBaseUrl;

        public static EditorSettings GetOrCreateInstance()
        {
            if (_instance != null)
                return _instance;

            var instances = AssetDatabase.FindAssets($"t:{nameof(EditorSettings)}", new string[] { "Assets/_LocalWorkspace" });

            if (instances.Length == 0)
            {
                _instance = CreateInstance<EditorSettings>();

                if (!System.IO.Directory.Exists($"{Application.dataPath}/_LocalWorkspace"))
                    AssetDatabase.CreateFolder("Assets", "_LocalWorkspace");

                string name = AssetDatabase.GenerateUniqueAssetPath("Assets/_LocalWorkspace/EditorSettings.asset");
                AssetDatabase.CreateAsset(_instance, name);
                AssetDatabase.SaveAssets();
                return _instance;
            }

            _instance = AssetDatabase.LoadAssetAtPath<EditorSettings>(AssetDatabase.GUIDToAssetPath(instances[0]));
            return _instance;
        }
    }
}
#endif