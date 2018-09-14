#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Shenmunity
{
    public class TACFileSelector : EditorWindow
    {
        static List<TACReader.TACEntry> m_list;
        static ShenmueAssetRef m_ref;
        static Vector2 m_scroll;
        static bool m_showPaths;
        static string m_search;
        static int m_offset;

        enum SortBy
        {
            Path,
            Name,
            Largest,
            Smallest,

            Count
        };
        static SortBy m_sortBy = SortBy.Path;

        static public void SelectFile(TACReader.FileType type, ShenmueAssetRef outRef)
        {
            ShowList(outRef, TACReader.GetFiles(type));
        }

        static public void ShowList(ShenmueAssetRef outRef, List<TACReader.TACEntry> list)
        {
            m_list = list;
            m_offset = 0;
            SortList();

            TACFileSelector window = (TACFileSelector)EditorWindow.GetWindow(typeof(TACFileSelector));
            m_ref = outRef;
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

        void OnGUI()
        {
            if (m_list == null)
            {
                Close();
                return;
            }

            m_showPaths = GUILayout.Toggle(m_showPaths, "Show paths");
            m_search = GUILayout.TextField(m_search);

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("SortBy:");
                SortBy oldSortBy = m_sortBy;
                for (int i = 0; i < (int)SortBy.Count; i++)
                {
                    if (GUILayout.Toggle(m_sortBy == (SortBy)i, ((SortBy)i).ToString()))
                    {
                        m_sortBy = (SortBy)i;
                    }
                }
                if(m_sortBy != oldSortBy)
                {
                    SortList();
                    m_offset = 0;
                }
            }

            m_scroll = GUILayout.BeginScrollView(m_scroll);
            bool none = true;

            int index = 0;

            if (m_offset > 0)
            {
                if (GUILayout.Button("Prev..."))
                {
                    m_offset -= 1000;
                }
            }

            foreach (var r in m_list)
            {
                string name = (r.m_parent != null ? r.m_parent.m_name + "/" : "") + r.m_name;

                if (!string.IsNullOrEmpty(m_search) && (string.IsNullOrEmpty(name) || name.IndexOf(m_search, System.StringComparison.OrdinalIgnoreCase) == -1))
                    continue;

                none = false;

                if (index > m_offset + 1000)
                {
                    if (GUILayout.Button("Next..."))
                    {
                        m_offset += 1000;
                    }
                    break;
                }

                if (index++ < m_offset)
                    continue;

                if (m_showPaths)
                    name = (m_showPaths ? r.m_path + " " : "") + name;

                if (GUILayout.Button(string.Format("{0} ({1}kb)", name, r.m_length/1000)))
                {
                    m_ref.m_path = r.m_path;
                    Close();
                    m_ref.OnChange();
                }
                
            }
            if (none)
            {
                if (m_offset > 0)
                    m_offset = 0;
                else
                    GUILayout.Label(string.Format("'{0}' not found", m_search));
            }

            GUILayout.EndScrollView();
        }

        static void SortList()
        {
            switch (m_sortBy)
            {
                case SortBy.Path:
                    m_list.Sort((x, y) => string.Compare(x.m_path, y.m_path));
                    break;
                case SortBy.Name:
                    m_list.Sort((x, y) =>
                    {
                        int leftIsName = !string.IsNullOrEmpty(x.m_name) ? 1 : 0;
                        int rightIsName = !string.IsNullOrEmpty(y.m_name) ? 1 : 0;
                        if (leftIsName == 1 && rightIsName == 1)
                        {
                            return string.Compare(x.m_name, y.m_name, System.StringComparison.OrdinalIgnoreCase);
                        }
                        return rightIsName - leftIsName;
                    });
                    break;
                case SortBy.Smallest:
                    m_list.Sort((x, y) => (int)(x.m_length - y.m_length));
                    break;
                case SortBy.Largest:
                    m_list.Sort((x, y) => (int)(y.m_length - x.m_length));
                    break;
            }
        }
    }
}

#endif