using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shenmunity
{
    [System.Serializable]
    public class ShenmueAssetRef
    {
        [HideInInspector]
        public string m_path;

        public System.Action OnChange;

        public ShenmueAssetRef()
        {
            OnChange = DoNothing;
        }

        void DoNothing()
        {

        }

#if UNITY_EDITOR
        public void DoHeader()
        {
            if (!string.IsNullOrEmpty(m_path))
            {
                GUILayout.Label(m_path);
                using (new GUILayout.HorizontalScope())
                {
                    var entry = TACReader.GetEntry(m_path);
                    if (entry != null)
                    {
                        if(entry.m_parent != null)
                        {
                            GUILayout.Label(entry.m_parent.m_name+"/");
                        }

                        var name = EditorGUILayout.DelayedTextField("", entry.m_name);
                        if (name != entry.m_name)
                        {
                            entry.m_name = name;
                            TACReader.SaveNames();
                        }
                    }
                }
            }
        }

        public void DoInspectorGUI(TACReader.FileType type, System.Action onchange, System.Action onchangevalue = null)
        {
            if (onchangevalue == null)
                onchangevalue = onchange;

            OnChange = onchangevalue;

            DoHeader();

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("<"))
                {
                    TACFileSelector.SwitchAsset(type, this, -1);
                }
                if (GUILayout.Button("Choose asset"))
                {
                    TACFileSelector.SelectFile(type, this);
                }
                if (GUILayout.Button(">"))
                {
                    TACFileSelector.SwitchAsset(type, this, 1);
                }
            }
            
            if (onchange != DoNothing && GUILayout.Button("Reload model"))
            {
                onchange();
            }
        }
#endif
    }
}