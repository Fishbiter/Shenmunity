using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shenmunity
{
    [ExecuteInEditMode]
    [SelectionBase]
    public class ShenmueCHRT : MonoBehaviour
    {
        [HideInInspector]
        public ShenmueAssetRef m_chrt = new ShenmueAssetRef();
        public ShenmueAssetRef m_paks = new ShenmueAssetRef();

#if UNITY_EDITOR
        private void Awake()
        {
            m_chrt.OnChange = () => ShenmueCHRTEditor.ShowCandidates(this);
        }

        [MenuItem("GameObject/Shenmunity/Scene (CHRT)", priority = 10)]
        public static void Create()
        {
            var sm = new GameObject("Shenmue scene");
            TACFileSelector.SelectFile(TACReader.FileType.CHRT, sm.AddComponent<ShenmueCHRT>().m_chrt);
        }
#endif

        public void OnChange()
        {
            if (string.IsNullOrEmpty(m_chrt.m_path) && string.IsNullOrEmpty(m_paks.m_path))
            {
                return;
            }

            CHRT chrt;
            uint len;
            using (var br = TACReader.GetBytes(m_chrt.m_path, out len))
            {
                chrt = new CHRT(br);
            }

            TACReader.SetTextureNamespace(TACReader.GetEntry(m_chrt.m_path).m_parent.m_path);

            var paks = TACReader.GetEntry(m_paks.m_path);
            foreach (var file in paks.m_children)
            {
                if(file.m_type == "MAPM")
                {
                    ShenmueModel.Create(file.m_path, transform);
                }
            }

            foreach (var node in chrt.m_nodes)
            {
                var fileName = paks.m_path + "_" + node.m_model;
                foreach (var file in paks.m_children)
                {
                    if (file.m_path == fileName)
                    {
                        var model = ShenmueModel.Create(file.m_path, transform);
                        model.name = node.m_id + " (" + node.m_image + " : " + model.name + ")";
                        model.transform.localPosition = node.m_position;
                        model.transform.localEulerAngles = new Vector3(0, 0, 0);
                        model.transform.Rotate(Vector3.forward, node.m_eulerAngles.z);
                        model.transform.Rotate(Vector3.up, node.m_eulerAngles.y);
                        model.transform.Rotate(Vector3.right, node.m_eulerAngles.x);
                    }
                }
            }

            TACReader.SetTextureNamespace("");
        }
    }


#if UNITY_EDITOR
    [CustomEditor(typeof(ShenmueCHRT))]
    public class ShenmueCHRTEditor : Editor
    {
        ShenmueCHRT m_showCandidates;

        void OnCHRTChange(ShenmueCHRT target)
        {
            target.m_paks.m_path = "";
            m_showCandidates = target;
        }

        IEnumerator ShowCandidatesNext(ShenmueCHRT target)
        {
            yield return new WaitForEndOfFrame();
            ShowCandidates(target);
        }

        public static void ShowCandidates(ShenmueCHRT smar)
        {
            smar.m_paks.OnChange = smar.OnChange;
            CHRT chrt;
            uint len;
            using (var br = TACReader.GetBytes(smar.m_chrt.m_path, out len))
            {
                chrt = new CHRT(br);
            }
            TACFileSelector.ShowList(smar.m_paks, TACReader.GetPAKSCandidates(chrt.GetModelNames()));
        }

        public override void OnInspectorGUI()
        {
            if(m_showCandidates != null)
            {
                ShowCandidates(m_showCandidates);
                return;
            }

            var smar = (ShenmueCHRT)target;

            smar.m_chrt.DoInspectorGUI(TACReader.FileType.CHRT, smar.OnChange, () => OnCHRTChange(smar));

            if(!string.IsNullOrEmpty(smar.m_chrt.m_path))
            {
                smar.m_paks.DoHeader();
                if (GUILayout.Button("Select PAKS"))
                {
                    ShowCandidates(smar);
                }
            }

            DrawDefaultInspector();
        }
    }
#endif
}