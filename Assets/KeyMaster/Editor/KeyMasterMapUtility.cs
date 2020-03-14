using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using KeyMaster;


namespace KeyMaster.EditorUtilities
{
    static public partial class EditorUtilities
    {
        /// <summary>
        /// this is only used for generating a json file that can be read by external applications
        /// to send commands over a network. if only using key commands this is unnecessary
        /// </summary>
        [MenuItem("Utilities/KeyMaster/Build Network Command Map", priority = 2001)]
        static void BuildExternalControlFile()
        {
            var keyVault = GetKeyVault();

            // create json file for external apps to read 
            // allowing for network commands to be sent
            Dictionary<ControlPage, List<string>> commandMap = new Dictionary<ControlPage, List<string>>();

            foreach (var token in keyVault.tokens)
            {
                if (commandMap.ContainsKey(token.page)) commandMap[token.page].Add(token.label);
                else
                {
                    var list = new List<string>();
                    list.Add(token.label);
                    commandMap.Add(token.page, list);
                }
            }

            var fullFilePath = Path.Combine(Application.streamingAssetsPath, "keyCommandMap.json");
            if (!Directory.Exists(Application.streamingAssetsPath))
            {
                Directory.CreateDirectory(Application.streamingAssetsPath);
            }

            var storage = new KeyTokenListStorage(keyVault);
            var contents = JsonUtility.ToJson(storage, true);

            using (StreamWriter sw = new StreamWriter(fullFilePath))
            {
                sw.Write(contents);
            }
            Debug.LogFormat("#EDITOR# BuildExternalControlFile| updated network command map to file: {0}", fullFilePath);
            AssetDatabase.Refresh();
        }

        static public void PopulateKeyVault()
        {
            var methods = Utilities.GetDecoratedMethods();

            #region find or create serialized asset 
            var keyStorage = GetKeyVault();
            var keyTokenList = new List<KeyToken>();

            foreach (var meth in methods)
            {
                var k   = meth.GetCustomAttribute<KeyToken>();
                k.label = meth.Name.FriendlyFormat();
                k.type  = meth.DeclaringType.RemoveNamespaceFromType();
                keyTokenList.Add(k);
            }

            keyStorage.tokens = keyTokenList.ToArray();
            AssetDatabase.SaveAssets();
            #endregion
        }

        static public List<MethodInfo> GetDecoratedMethods()
        {
            return System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(asm =>
                    asm.GetTypes()
                    .SelectMany(t => t.GetRuntimeMethods())
                    .Where(m => m.GetCustomAttributes<KeyToken>(false).Count() > 0))
                .ToList();
        }

        static public KeyVault GetKeyVault()
        {
            KeyVault storageAsset = null;
            var assetPath = GetStorageLocation();
            //Debug.LogFormat("#EDITOR# attempting to load keystorage at path: {0}", assetPath);
            storageAsset = AssetDatabase.LoadAssetAtPath<KeyVault>(assetPath);
            if(storageAsset == null)
            {
                Debug.LogFormat("#EDITOR# keystorage was not found, creating new keystorage at path: {0}", assetPath);
                storageAsset    = ScriptableObject.CreateInstance<KeyVault>();

                AssetDatabase.CreateAsset(storageAsset, assetPath);
                AssetDatabase.Refresh();
            }
            else
            {
                //Debug.LogFormat("#EDITOR# found keystorage object at {0}", AssetDatabase.GetAssetPath(storageAsset));
            }
            return storageAsset;
        }

        static string GetStorageLocation()
        {
            var keyStorageClass = AssetDatabase.FindAssets("KeyVault t: TextAsset")
                    .Select(a => AssetDatabase.GUIDToAssetPath(a))
                    .FirstOrDefault();
            //Debug.LogFormat("#EDITOR# KeyStorage found at: {0}", keyStorageClass);

            // need system path to get full Assets/ project path
            var fullpath = Directory.GetParent(keyStorageClass).FullName;
            //Debug.LogFormat("#EDITOR# KeyStorage full path: {0}", fullpath);

            // convert back to assetdatabase-friendly path
            return Path.Combine(SystemToAssetPath(fullpath), "keyVault.asset");
        }

        /// <summary>
        /// takes a full system path and returns unity's assetdatabase-friendly path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static public string SystemToAssetPath(string path)
        {
            if(!Directory.Exists(path) && !File.Exists(path))
            {
                Debug.LogFormat("#EDITOR# couldn't convert to asset path: path {0} was invalid", path);
                return null;
            }
            var sub = path.Substring(path.IndexOf(@"Assets\"));
            //Debug.LogFormat("#EDITOR# converted {0} to asset path {1}", path, sub);
            return sub;
        }
        
        /// <summary>
        /// get full system path for project-relative asset path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static public string AssetToSystemPath(string path)
        {
            return Path.GetFullPath(path);
        }

        static public Texture2D BackgroundTexture(this Color color, int width, int height)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        /// <summary>
        /// used to create serializable list container
        /// JsonUtility cannot directly serialize a list, and must have a class object container
        /// in order to serialize a list of objects
        /// </summary>
        [System.Serializable]
        internal class KeyTokenListStorage
        {
            public List<NetworkKeyTokens> jsonTokens;
            public KeyTokenListStorage(KeyVault runtimeTokens)
            {
                var pages = runtimeTokens.tokens.Select(t => t.page)
                    .Distinct()
                    .ToList();

                jsonTokens = new List<NetworkKeyTokens>();


                foreach (var p in pages)
                {
                    jsonTokens.Add(new NetworkKeyTokens()
                    {
                        page = p.ToString(),
                        labels = runtimeTokens.tokens
                            .Where(t => t.page == p)
                            .Select(t => t.label)
                            .ToArray()
                    });
                };
            }
        }

        /// <summary>
        /// used to store tokens to json file that is not intended to be read by c#
        /// translates KeyMasterToken to json-friendly storage
        /// </summary>
        [System.Serializable]
        internal class NetworkKeyTokens
        {
            public string page;
            public string[] labels;
        }
    }



    public class CommandMapWindow : EditorWindow
    {

        static KeyVault keyVault;
        DisplayHelper[] assignments;

        enum ViewType
        {
            Page,
            Type,
            Key
        }

        ViewType viewType;
        Vector2 scrollPos;

        static GUIStyle style;
        Color buttonColor =  Color.white * .5f;
        Dictionary<ControlPage, bool> showPage  = new Dictionary<ControlPage, bool>();
        Dictionary<KeyCode, bool> showKey       = new Dictionary<KeyCode, bool>();
        Dictionary<string, bool> showType       = new Dictionary<string, bool>();

        [MenuItem("Utilities/KeyMaster/View Command Map", priority = 2000)]
        static public void OpenWindow()
        {
            EditorWindow.GetWindow<CommandMapWindow>().Show();
        }
        
        void OnFocus()
        {
            keyVault = EditorUtilities.GetKeyVault();
            BuildStyle();
            Init();
        }

        void Init()
        {
            if (keyVault == null || keyVault.tokens == null)
            {
                Debug.LogFormat("#EDITOR# couldn't find keyVault asset to read key command map");
                return;
            }
            
            
            assignments = new DisplayHelper[keyVault.tokens.Length];
            for (int i = 0; i < keyVault.tokens.Length; i++)
            {
                var attrib = keyVault.tokens[i];
                assignments[i] = new DisplayHelper()
                {
                    type    = attrib.type,
                    page    = attrib.page,
                    label   = attrib.label,
                    key     = attrib.key,
                    combo   = attrib.keyCombo == null || attrib.keyCombo.keys == null ? string.Empty : attrib.keyCombo.keys
                                .Select(k => string.Format(" + {0}", k.ToString()))
                                .Aggregate((a, b) => a + " " + b),
                };
            }

            showPage.Clear();
            // start at 1: skip "None" value
            for (int i = 1; i < System.Enum.GetValues(typeof(ControlPage)).Length; i++)
            {
                showPage.Add((ControlPage)i, true);
            }

            showKey.Clear();
            for (int i = 0; i < System.Enum.GetValues(typeof(KeyCode)).Length; i++)
            {
                showKey.Add((KeyCode)i, true);
            }

            showType.Clear();
            for (int i = 0; i < assignments.Length; i++)
            {
                if (!showType.ContainsKey(assignments[i].type))
                {
                    showType.Add(assignments[i].type, true);
                }
            }

            viewType = ViewType.Page;
        }


        void OnGUI()
        {
            Color c = GUI.backgroundColor;
            if(assignments == null || assignments.Length == 0)
            {
                style.normal.textColor = Color.white;
                EditorGUILayout.LabelField("no keys found", style);
                if (GUILayout.Button(new GUIContent("repopulate tokens from codebase")))
                {
                    EditorUtilities.PopulateKeyVault();
                    keyVault = EditorUtilities.GetKeyVault();
                    Init();
                }
                return;
            }

            ControlPage page = ControlPage.None;
            GUILayout.BeginVertical(style);
            GUI.backgroundColor = buttonColor;
            EditorGUILayout.LabelField("Select Layout Type", GUILayout.Width(150));
            viewType = (ViewType)EditorGUILayout.EnumPopup(viewType, GUILayout.Width(100));

            if (GUILayout.Button(new GUIContent("repopulate tokens from codebase")))
            {
                EditorUtilities.PopulateKeyVault();
                keyVault = EditorUtilities.GetKeyVault();
                Init();
            }

            GUI.backgroundColor = c;
            GUILayout.EndVertical();
            EditorGUILayout.Separator();
            EditorGUILayout.BeginVertical(style);
            

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, true);
            if (viewType == ViewType.Page)
            {
                assignments = assignments
                    .OrderBy(a => a.page)
                    .ThenBy(a => a.key)
                    .ToArray();
                for (int i = 0; i < assignments.Length; i++)
                {
                    page = ShowByPage(assignments[i], page);
                }
            }
            else if (viewType == ViewType.Key)
            {
                assignments = assignments
                    .OrderBy(a => a.key)
                    .ToArray();
                ShowByKey(assignments);
            }
            else if (viewType == ViewType.Type)
            {
                assignments = assignments
                    .OrderBy(a => a.type)
                    .ToArray();
                ShowByType(assignments);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void ShowByKey(DisplayHelper[] assignments)
        {
            List<DisplayHelper> asses = assignments.OrderBy(h => h.key).ToList();
            KeyCode curKey = KeyCode.None;
            foreach (var ass in asses)
            {
                /*
                    yeah...  i named variables asses and ass.
                    deal with it
                    (•_•)
                    ( •_•)>⌐■-■
                    (⌐■_■)
                    hashtag firstwoldanarchy
                 */
                if (curKey != ass.key)
                {
                    Color c = GUI.backgroundColor;
                    GUI.backgroundColor = buttonColor;
                    if (GUILayout.Button(string.Format("Key: \t{0} {1}", ass.key, ass.combo), GUILayout.Height(20)))
                    {
                        showKey[ass.key] = !showKey[ass.key];
                    }
                    if (!showKey[ass.key])
                    {
                        GUI.backgroundColor = c;
                        continue;
                    }
                    c = GUI.contentColor;
                    GUI.contentColor = Color.magenta;
                    curKey = ass.key;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Label", GUILayout.ExpandWidth(true), GUILayout.Width(300));
                    GUILayout.Label("Page", GUILayout.ExpandWidth(true), GUILayout.Width(300));
                    GUILayout.Label("Type", GUILayout.ExpandWidth(true), GUILayout.Width(300));
                    
                    GUILayout.EndHorizontal();
                    GUI.contentColor = c;
                }
                if (!showKey[ass.key]) continue;

                GUILayout.BeginHorizontal();
                GUILayout.Label(ass.label, GUILayout.ExpandWidth(true), GUILayout.Width(300));
                GUILayout.Label(ass.page.ToString(), GUILayout.ExpandWidth(true), GUILayout.Width(300));
                GUILayout.Label(ass.type, GUILayout.ExpandWidth(true), GUILayout.Width(300));
                //if (ass.combo != null)
                //{
                //    Color c = GUI.backgroundColor;
                //    GUI.contentColor = buttonColor;
                //    GUILayout.BeginVertical();
                //    GUILayout.Label("\t ***key combo ***", GUILayout.Width(200));
                //    for (int j = 0; j < ass.combo.keys.Length; j++)
                //    {
                //        GUILayout.Label(new GUIContent(string.Format("\tkey: {0}", ass.combo.keys[j])), GUILayout.ExpandWidth(true));
                //    }
                //    GUI.contentColor = c;
                //    GUILayout.EndVertical();
                //}
                GUILayout.EndHorizontal();
            }
        }

        ControlPage ShowByPage(DisplayHelper dh, ControlPage page)
        {
            if (page != dh.page)
            {
                page = dh.page;
                Color c = GUI.backgroundColor;
                GUI.backgroundColor = buttonColor;
                if (GUILayout.Button(string.Format("Page: \t{0}", page), GUILayout.Height(20)))
                {
                    showPage[page] = !showPage[page];
                }
                if (!showPage[page])
                {
                    GUI.backgroundColor = c;
                    return page;
                }
                c = GUI.contentColor;
                GUI.contentColor = Color.magenta;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Label", GUILayout.ExpandWidth(true), GUILayout.Width(300));
                GUILayout.Label("Key", GUILayout.ExpandWidth(true), GUILayout.Width(300));
                GUILayout.Label("Type", GUILayout.ExpandWidth(true), GUILayout.Width(300));
                
                GUILayout.EndHorizontal();
                GUI.contentColor = c;
            }
            if (!showPage[page]) return page;


            GUILayout.BeginHorizontal();
            GUILayout.Label(dh.label, GUILayout.ExpandWidth(true), GUILayout.Width(300));
            GUILayout.Label(string.Format("{0} {1}", dh.key.ToString(), dh.combo), GUILayout.ExpandWidth(true), GUILayout.Width(300));
            GUILayout.Label(dh.type, GUILayout.ExpandWidth(true), GUILayout.Width(300));
            GUILayout.EndHorizontal();
            return page;
        }

        void ShowByType(DisplayHelper[] assignments)
        {
            List<DisplayHelper> asses = assignments
                .OrderBy(h => h.type)
                .ThenBy(h => h.key)
                .ToList();
            string type = string.Empty;
            foreach (var ass in asses)
            {
                if (type != ass.type)
                {
                    type = ass.type;
                    Color c = GUI.backgroundColor;
                    GUI.backgroundColor = buttonColor;
                    if (GUILayout.Button(string.Format("Type: \t{0}", ass.type), GUILayout.Height(20)))
                    {
                        showType[ass.type] = !showType[ass.type];
                    }
                    if (!showType[ass.type])
                    {
                        GUI.backgroundColor = c;
                        continue;
                    }
                    GUI.backgroundColor = c;
                    c = GUI.contentColor;
                    GUI.contentColor = Color.magenta;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Label", GUILayout.ExpandWidth(true), GUILayout.Width(300));
                    GUILayout.Label("Key", GUILayout.ExpandWidth(true), GUILayout.Width(300));
                    GUILayout.Label("Page", GUILayout.ExpandWidth(true), GUILayout.Width(300));
                    
                    GUILayout.EndHorizontal();
                    GUI.contentColor = c;
                }
                if (!showType[ass.type]) continue;

                GUILayout.BeginHorizontal();
                GUILayout.Label(ass.label, GUILayout.ExpandWidth(true), GUILayout.Width(300));
                GUILayout.Label(string.Format("{0} {1}", ass.key.ToString(), ass.combo), GUILayout.ExpandWidth(true), GUILayout.Width(300));
                GUILayout.Label(ass.page.ToString(), GUILayout.ExpandWidth(true), GUILayout.Width(300));
                GUILayout.EndHorizontal();
            }
        }

        void BuildStyle()
        {
            if (style != null) return;
            style                   = new GUIStyle();
            style.normal.background = EditorUtilities.BackgroundTexture(Color.black, 1, 1);
            style.alignment         = TextAnchor.MiddleLeft;
        }

        class DisplayHelper
        {
            public string type;
            public string label;
            public ControlPage page;
            public KeyCode key;
            public string combo;
        }
    }

    [CustomEditor(typeof(KeyVault))]
    public class KeyVaultEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if(GUILayout.Button("view command map"))
            {
                CommandMapWindow.OpenWindow();
            }
        }
    }
}
