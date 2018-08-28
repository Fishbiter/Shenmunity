using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Shenmunity
{
    public class TACFileSelector : EditorWindow
    {
        List<TACReader.TACEntry> m_list;
        ShenmueAssetRef m_ref;
        static Vector2 m_scroll;
        static bool m_showNamed;
        static string m_search;

        static public void SelectFile(TACReader.FileType type, ShenmueAssetRef outRef)
        {
            // Get existing open window or if none, make a new one:
            TACFileSelector window = (TACFileSelector)EditorWindow.GetWindow(typeof(TACFileSelector));
            window.ListFiles(type);
            window.m_ref = outRef;
            window.Show();
        }

        static public void SwitchAsset(TACReader.FileType type, ShenmueAssetRef outRef, int dir)
        {
            var files = TACReader.GetFiles(type);
            for(int i = 0; i < files.Count; i++)
            {
                if(files[i].m_path == outRef.m_path)
                {
                    outRef.m_path = files[(i + dir + files.Count) % files.Count].m_path;
                    outRef.OnChange();
                    return;
                }
            }

            if(dir > 0)
            {
                outRef.m_path = files[0].m_path;
            }
            else
            {
                outRef.m_path = files[files.Count-1].m_path;
            }
            outRef.OnChange();
        }

        void ListFiles(TACReader.FileType type)
        {
            m_list = TACReader.GetFiles(type);
        }

        void OnGUI()
        {
            m_showNamed = GUILayout.Toggle(m_showNamed, "Only show named");
            m_search = GUILayout.TextField(m_search);

            m_scroll = GUILayout.BeginScrollView(m_scroll);
            bool none = true;

            foreach(var r in m_list)
            {
                if (m_showNamed && string.IsNullOrEmpty(r.m_name))
                    continue;

                if (!string.IsNullOrEmpty(m_search) && (string.IsNullOrEmpty(r.m_name) || r.m_name.IndexOf(m_search) == -1))
                    continue;

                if (GUILayout.Button(string.Format("{0} {1} ({2}kb)", r.m_path, r.m_name, r.m_length/1000)))
                {
                    m_ref.m_path = r.m_path;
                    m_ref.OnChange();
                    Close();
                }
                none = false;
            }
            if (none)
                GUILayout.Label(string.Format("'{0}' not found", m_search));

            GUILayout.EndScrollView();
        }
    }
}