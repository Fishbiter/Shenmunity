using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shenmunity
{
    public abstract class ShenmueAssetRef : MonoBehaviour
    {
        [HideInInspector]
        public string m_path;

        public abstract void OnChange();

        public void DoInspectorGUI(TACReader.FileType type)
        {
            if (!string.IsNullOrEmpty(m_path))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(m_path);
                    var entry = TACReader.GetEntry(m_path);
                    if (entry != null)
                    {
                        var name = EditorGUILayout.DelayedTextField("TAG", entry.m_name);
                        if (name != entry.m_name)
                        {
                            entry.m_name = name;
                            TACReader.SaveNames();
                        }
                    }
                }
            }

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
            if (GUILayout.Button("Reload model"))
            {
                OnChange();
            }
        }
    }
}