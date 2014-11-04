using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace M8.Editor {
    public class InputBinder : EditorWindow {
        public const string PrefKlassMapper = "InputMapper";
        public const string PrefFileMapper = "File";

        private static List<string> mActions = null;

        private static bool mActionFoldout;

        private static string[] mActionEditNames;
        private static int[] mActionEditVals;

        private TextAsset mTextFileMapper;
        private string mTextNameMapper = "";
        private string mTextFilePathMapper = "";

        private GUIStyle mTitleFoldoutStyle;
        private Vector2 mScroll;

        private uint mUnknownCount = 0;

        public static List<string> actions {
            get {
                if(mActions == null) {
                    //try to load it
                    string path = EditorPrefs.GetString(Utility.PreferenceKey(PrefKlassMapper, PrefFileMapper), "");
                    if(!string.IsNullOrEmpty(path))
                        GetInputActions(AssetDatabase.LoadAssetAtPath(path, typeof(TextAsset)) as TextAsset);
                }

                return mActions;
            }
        }
                
        public static int GUISelectInputAction(string label, int selectedValue) {
            if(actions != null && actions.Count > 0) {
                return EditorGUILayout.IntPopup(label, selectedValue, mActionEditNames, mActionEditVals);
            }
            else {
                GUILayout.BeginHorizontal();

                GUILayout.Label(label);

                //let user know they need to configure input actions
                if(GUILayout.Button("[Edit Input Actions]", GUILayout.Width(200))) {
                    mActionFoldout = true;
                    EditorWindow.GetWindow(typeof(InputBinder));
                }

                GUILayout.EndHorizontal();
            }

            return selectedValue;
        }

        private static void GetInputActions(TextAsset cfg) {
            if(cfg != null) {
                fastJSON.JSON.Parameters.UseExtensions = false;
                try {
                    mActions = fastJSON.JSON.ToObject<List<string>>(cfg.text);
                }
                catch {
                    mActions = new List<string>();
                }
                RefreshInputActionEdits();
            }
        }
        
        private static void RefreshInputActionEdits() {
            if(mActions != null) {
                mActionEditNames = new string[mActions.Count + 1];
                mActionEditVals = new int[mActions.Count + 1];

                mActionEditNames[0] = "Invalid";
                mActionEditVals[0] = InputManager.ActionInvalid;

                for(int i = 0; i < mActions.Count; i++) {
                    mActionEditNames[i + 1] = mActions[i];
                    mActionEditVals[i + 1] = i;
                }
            }
        }

        public const string PrefKlassBinder = "InputBinder";
        public const string PrefTextBinder = "Text";

        private static bool mBindingFoldout;

        private TextAsset mTextFileBinder;
        private string mTextNameBinder = "";
        private string mTextFilePathBinder = "";

        private enum InputType {
            Unity,
            KeyCode,
            InputMap
        }

        private struct BindData {
            public InputManager.Bind bind;
            public InputType[] keyTypes;
            public bool foldOut;

            public void DeleteKey(int ind) {
                bind.keys.RemoveAt(ind);
                keyTypes = M8.ArrayUtil.RemoveAt(keyTypes, ind);
            }

            public void ResizeKeys(int newLen) {
                if(keyTypes == null) {
                    if(bind.keys == null) {
                        bind.keys = new List<InputManager.Key>();
                        keyTypes = new InputType[0];
                    }
                    else if(bind.keys.Count == 0) {
                        keyTypes = new InputType[0];
                    }
                    else {
                        RefreshKeyTypes();
                    }
                }

                int prevLen = keyTypes.Length;

                if(prevLen != newLen) {
                    System.Array.Resize(ref keyTypes, newLen);

                    if(prevLen > keyTypes.Length) {
                        bind.keys.RemoveRange(newLen - 1, bind.keys.Count - newLen);
                    }
                    else {
                        for(int i = prevLen; i < keyTypes.Length; i++) {
                            keyTypes[i] = InputType.Unity;
                            bind.keys.Add(new InputManager.Key());
                        }
                    }
                }
            }

            public void RefreshKeyType(int ind) {
                if(bind.keys[ind].map != InputKeyMap.None)
                    keyTypes[ind] = InputType.InputMap;
                else if(bind.keys[ind].code != KeyCode.None)
                    keyTypes[ind] = InputType.KeyCode;
                else
                    keyTypes[ind] = InputType.Unity;
            }

            public void RefreshKeyTypes() {
                if(bind.keys != null && bind.keys.Count > 0) {
                    if(keyTypes == null || keyTypes.Length != bind.keys.Count) {
                        keyTypes = new InputType[bind.keys.Count];
                    }

                    for(int i = 0; i < keyTypes.Length; i++)
                        RefreshKeyType(i);
                }
            }

            //call before saving, basically clears out specific key binds based on key type
            public void ApplyKeyTypes() {
                if(keyTypes != null && bind.keys != null) {
                    for(int i = 0; i < keyTypes.Length; i++) {
                        switch(keyTypes[i]) {
                            case InputType.Unity:
                                bind.keys[i].code = KeyCode.None;
                                bind.keys[i].map = InputKeyMap.None;
                                break;

                            case InputType.KeyCode:
                                bind.keys[i].input = "";
                                bind.keys[i].map = InputKeyMap.None;
                                break;

                            case InputType.InputMap:
                                bind.keys[i].input = "";
                                bind.keys[i].code = KeyCode.None;
                                break;
                        }
                    }
                }
            }
        }

        private BindData[] mBinds;

        [MenuItem("M8/Input")]
        static void DoIt() {
            mActionFoldout = true;
            mBindingFoldout = true;
            EditorWindow.GetWindow(typeof(InputBinder));
        }

        void OnSelectionChange() {
            Repaint();
        }

        void OnEnable() {
            mTitleFoldoutStyle = new GUIStyle(EditorStyles.foldout);
            mTitleFoldoutStyle.richText = true;

            //mapper
            string path = EditorPrefs.GetString(Utility.PreferenceKey(PrefKlassMapper, PrefFileMapper), "");
            if(!string.IsNullOrEmpty(path)) {
                Object obj = AssetDatabase.LoadAssetAtPath(path, typeof(TextAsset));
                if(obj != null)
                    mTextFileMapper = (TextAsset)obj;
            }

            //binder
            path = EditorPrefs.GetString(Utility.PreferenceKey(PrefKlassBinder, PrefTextBinder), "");
            if(!string.IsNullOrEmpty(path)) {
                Object obj = AssetDatabase.LoadAssetAtPath(path, typeof(TextAsset));
                if(obj != null)
                    mTextFileBinder = (TextAsset)obj;
            }
        }

        void OnDisable() {
            if(mTextFileMapper != null)
                EditorPrefs.SetString(Utility.PreferenceKey(PrefKlassMapper, PrefFileMapper), AssetDatabase.GetAssetPath(mTextFileMapper));

            if(mTextFileBinder != null)
                EditorPrefs.SetString(Utility.PreferenceKey(PrefKlassBinder, PrefTextBinder), AssetDatabase.GetAssetPath(mTextFileBinder));
        }

        void OnGUI() {
            mScroll = GUILayout.BeginScrollView(mScroll);//, GUILayout.MinHeight(100));

            mActionFoldout = EditorGUILayout.Foldout(mActionFoldout, "<b><color=orange>ACTIONS</color></b>", mTitleFoldoutStyle);
            if(mActionFoldout)
                OnGUIMapper();

            Utility.DrawSeparator();

            mBindingFoldout = EditorGUILayout.Foldout(mBindingFoldout, "<b><color=orange>BINDINGS</color></b>", mTitleFoldoutStyle);
            if(mBindingFoldout)
                OnGUIBinder();

            GUILayout.EndScrollView();

            Utility.DrawSeparator();

            GUI.backgroundColor = Color.green;

            if(GUILayout.Button("Save")) {
                if(mActions != null) {
                    fastJSON.JSON.Parameters.UseExtensions = false;

                    //save mapping
                    string actionString = fastJSON.JSON.ToJSON(mActions);
                    File.WriteAllText(mTextFilePathMapper, actionString);

                    RefreshInputActionEdits();
                }
                //

                //save binding
                if(mBinds != null) {
                    List<InputManager.Bind> saveBinds = new List<InputManager.Bind>(mBinds.Length);

                    for(int i = 0; i < mBinds.Length; i++) {
                        mBinds[i].ApplyKeyTypes();

                        saveBinds.Add(mBinds[i].bind);
                    }

                    string bindString = fastJSON.JSON.ToJSON(saveBinds);
                    File.WriteAllText(mTextFilePathBinder, bindString);
                }
                //

                AssetDatabase.Refresh();
            }

            GUI.backgroundColor = Color.white;
        }

        void OnGUIMapper() {
            TextAsset prevTextFile = mTextFileMapper;

            EditorGUIUtility.labelWidth = 80.0f;

            GUILayout.BeginHorizontal();

            bool doCreate = false;

            if(mTextFileMapper == null) {
                GUI.backgroundColor = Color.green;
                doCreate = GUILayout.Button("Create", GUILayout.Width(76f));

                GUI.backgroundColor = Color.white;
                mTextNameMapper = GUILayout.TextField(mTextNameMapper);
            }

            GUILayout.EndHorizontal();

            if(mTextFileMapper != null) {
                mTextNameMapper = mTextFileMapper.name;
                mTextFilePathMapper = AssetDatabase.GetAssetPath(mTextFileMapper);
            }
            else if(!string.IsNullOrEmpty(mTextNameMapper)) {
                mTextFilePathMapper = Utility.GetSelectionFolder() + mTextNameMapper + ".txt";
            }

            if(doCreate && !string.IsNullOrEmpty(mTextNameMapper)) {
                File.WriteAllText(mTextFilePathMapper, "");

                AssetDatabase.Refresh();

                mTextFileMapper = (TextAsset)AssetDatabase.LoadAssetAtPath(mTextFilePathMapper, typeof(TextAsset));
            }

            GUILayout.BeginHorizontal();

            GUILayout.Label("Select: ");

            mTextFileMapper = (TextAsset)EditorGUILayout.ObjectField(mTextFileMapper, typeof(TextAsset), false);

            GUILayout.EndHorizontal();

            if(!string.IsNullOrEmpty(mTextFilePathMapper))
                GUILayout.Label("Path: " + mTextFilePathMapper);
            else {
                GUILayout.Label("Path: <none>" + mTextFilePathMapper);
            }

            GUILayout.Space(6f);

            GUILayout.BeginVertical(GUI.skin.box);

            if(mTextFileMapper != null) {
                if(prevTextFile != mTextFileMapper || mActions == null)
                    GetInputActions(mTextFileMapper);

                //list actions
                int removeInd = -1;

                Regex r = new Regex("^[a-zA-Z0-9]*$");

                for(int i = 0; i < mActions.Count; i++) {
                    GUILayout.BeginHorizontal();

                    GUILayout.Label(i.ToString(), GUILayout.MaxWidth(20));

                    string text = GUILayout.TextField(mActions[i], 255);

                    if(text.Length > 0 && (r.IsMatch(text) && !char.IsDigit(text[0])))
                        mActions[i] = text;

                    if(GUILayout.Button("DEL", GUILayout.MaxWidth(40))) {
                        removeInd = i;
                    }

                    GUILayout.EndHorizontal();
                }

                if(removeInd != -1)
                    mActions.RemoveAt(removeInd);

                if(GUILayout.Button("Add")) {
                    mActions.Add("Unknown" + (mUnknownCount++));
                }
            }

            GUILayout.EndVertical();

            EditorGUIUtility.labelWidth = 0.0f;
        }

        void OnGUIBinder() {
            TextAsset prevTextFile = mTextFileBinder;

            EditorGUIUtility.labelWidth = 80.0f;

            GUILayout.BeginHorizontal();

            bool doCreate = false;

            if(mTextFileBinder == null) {
                GUI.backgroundColor = Color.green;
                doCreate = GUILayout.Button("Create", GUILayout.Width(76f));

                GUI.backgroundColor = Color.white;
                mTextNameBinder = GUILayout.TextField(mTextNameBinder);
            }

            GUILayout.EndHorizontal();

            if(mTextFileBinder != null) {
                mTextNameBinder = mTextFileBinder.name;
                mTextFilePathBinder = AssetDatabase.GetAssetPath(mTextFileBinder);
            }
            else if(!string.IsNullOrEmpty(mTextNameBinder)) {
                mTextFilePathBinder = Utility.GetSelectionFolder() + mTextNameBinder + ".txt";
            }

            if(doCreate && !string.IsNullOrEmpty(mTextNameBinder)) {
                File.WriteAllText(mTextFilePathBinder, "");

                AssetDatabase.Refresh();

                mTextFileBinder = (TextAsset)AssetDatabase.LoadAssetAtPath(mTextFilePathBinder, typeof(TextAsset));
            }

            GUILayout.BeginHorizontal();

            GUILayout.Label("Select: ");

            mTextFileBinder = (TextAsset)EditorGUILayout.ObjectField(mTextFileBinder, typeof(TextAsset), false);

            GUILayout.EndHorizontal();

            if(!string.IsNullOrEmpty(mTextFilePathBinder))
                GUILayout.Label("Path: " + mTextFilePathBinder);
            else {
                GUILayout.Label("Path: <none>" + mTextFilePathBinder);
            }

            GUILayout.Space(6f);

            bool refreshBinds = mTextFileBinder != prevTextFile;

            if(mTextFileBinder != null && actions != null) {
                //initialize bind data
                if(mBinds == null || mBinds.Length != actions.Count) {
                    if(mBinds == null) {
                        mBinds = new BindData[actions.Count];
                    }
                    else {
                        System.Array.Resize<BindData>(ref mBinds, actions.Count);
                    }

                    refreshBinds = true;
                }

                //load from file
                if(refreshBinds && mTextFileBinder.text.Length > 0) {
                    //load data
                    fastJSON.JSON.Parameters.UseExtensions = false;
                    List<InputManager.Bind> loadBinds = fastJSON.JSON.ToObject<List<InputManager.Bind>>(mTextFileBinder.text);
                    foreach(InputManager.Bind bind in loadBinds) {
                        if(bind.action < mBinds.Length) {
                            mBinds[bind.action].bind = bind;
                            mBinds[bind.action].RefreshKeyTypes();
                        }
                    }
                }

                //display binds
                for(int i = 0; i < mBinds.Length; i++) {
                    if(mBinds[i].bind == null) {
                        mBinds[i].bind = new InputManager.Bind();
                    }

                    mBinds[i].bind.action = i;

                    GUILayout.BeginVertical(GUI.skin.box);

                    GUILayout.BeginHorizontal();

                    GUILayout.Label(actions[i]);

                    GUILayout.FlexibleSpace();

                    mBinds[i].bind.control = (InputManager.Control)EditorGUILayout.EnumPopup(mBinds[i].bind.control);

                    GUILayout.EndHorizontal();

                    if(mBinds[i].bind.control == InputManager.Control.Axis) {
                        mBinds[i].bind.deadZone = EditorGUILayout.FloatField("Deadzone", mBinds[i].bind.deadZone);
                        mBinds[i].bind.forceRaw = EditorGUILayout.Toggle("Force Raw", mBinds[i].bind.forceRaw);
                    }

                    int keyCount = mBinds[i].keyTypes != null ? mBinds[i].keyTypes.Length : 0;

                    mBinds[i].foldOut = EditorGUILayout.Foldout(mBinds[i].foldOut, string.Format("Binds [{0}]", keyCount));

                    if(mBinds[i].foldOut) {
                        int delKey = -1;

                        for(int key = 0; key < keyCount; key++) {
                            GUILayout.BeginVertical(GUI.skin.box);

                            GUILayout.BeginHorizontal();
                            mBinds[i].bind.keys[key].player = EditorGUILayout.IntField("Player", mBinds[i].bind.keys[key].player, GUILayout.MaxWidth(200));

                            GUILayout.FlexibleSpace();

                            if(GUILayout.Button("DEL", GUILayout.MaxWidth(40)))
                                delKey = key;

                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();

                            //key bind
                            mBinds[i].keyTypes[key] = (InputType)EditorGUILayout.EnumPopup(mBinds[i].keyTypes[key]);

                            switch(mBinds[i].keyTypes[key]) {
                                case InputType.Unity:
                                    mBinds[i].bind.keys[key].input = EditorGUILayout.TextField(mBinds[i].bind.keys[key].input, GUILayout.MinWidth(250));
                                    break;

                                case InputType.KeyCode:
                                    mBinds[i].bind.keys[key].code = (KeyCode)EditorGUILayout.EnumPopup(mBinds[i].bind.keys[key].code);
                                    break;

                                case InputType.InputMap:
                                    mBinds[i].bind.keys[key].map = (InputKeyMap)EditorGUILayout.EnumPopup(mBinds[i].bind.keys[key].map);
                                    break;
                            }

                            GUILayout.EndHorizontal();

                            //other configs
                            if(mBinds[i].bind.control == InputManager.Control.Axis) {
                                if(mBinds[i].keyTypes[key] != InputType.Unity)
                                    mBinds[i].bind.keys[key].axis = (InputManager.ButtonAxis)EditorGUILayout.EnumPopup("Axis", mBinds[i].bind.keys[key].axis, GUILayout.MaxWidth(200));

                                if(mBinds[i].keyTypes[key] == InputType.Unity || mBinds[i].bind.keys[key].axis == InputManager.ButtonAxis.Both)
                                    mBinds[i].bind.keys[key].invert = EditorGUILayout.Toggle("Invert", mBinds[i].bind.keys[key].invert);
                            }
                            else
                                mBinds[i].bind.keys[key].index = EditorGUILayout.IntField("Index", mBinds[i].bind.keys[key].index, GUILayout.MaxWidth(200));

                            GUILayout.EndVertical();
                        }

                        if(delKey != -1) {
                            mBinds[i].DeleteKey(delKey);
                        }

                        if(GUILayout.Button("Add")) {
                            mBinds[i].ResizeKeys(keyCount + 1);
                        }
                    }

                    GUILayout.EndVertical();
                }
            }

            EditorGUIUtility.labelWidth = 0.0f;
        }
    }
}