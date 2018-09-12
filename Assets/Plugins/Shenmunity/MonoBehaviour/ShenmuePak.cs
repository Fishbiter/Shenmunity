using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shenmunity
{
    [ExecuteInEditMode]
    [SelectionBase]
    public class ShenmuePak : MonoBehaviour
    {
        [SerializeField]
        [HideInInspector]
        ShenmueModel[] m_models;

        [HideInInspector]
        public ShenmueAssetRef m_pakRef = new ShenmueAssetRef();
        [HideInInspector]
        public ShenmueAssetRef m_textureSet = new ShenmueAssetRef();

#if UNITY_EDITOR
        [MenuItem("GameObject/Shenmunity/Pack (PAKS)", priority = 10)]
        public static void Create()
        {
            var sm = new GameObject("Shenmue pak");
            TACFileSelector.SelectFile(TACReader.FileType.PAKS, sm.AddComponent<ShenmuePak>().m_pakRef);
        }
#endif

        public void OnChange()
        {
            if (m_models != null)
            {
                foreach (var m in m_models)
                {
                    DestroyImmediate(m.gameObject);
                }
                m_models = null;
            }

            if (string.IsNullOrEmpty(m_pakRef.m_path))
            {
                return;
            }

            var models = TACReader.GetFiles(TACReader.FileType.MODEL);
            var parent = TACReader.GetEntry(m_pakRef.m_path);

            if(!string.IsNullOrEmpty(m_textureSet.m_path))
            {
                TACReader.SetTextureNamespace(m_textureSet.m_path);
            }

            var createdModels = new List<ShenmueModel>();

            foreach (var m in models)
            {
                if(m.m_parent == parent)
                {
                    createdModels.Add(ShenmueModel.Create(m.m_path, transform));
                }
            }

            TACReader.SetTextureNamespace("");

            m_models = createdModels.ToArray();
        }
    }


#if UNITY_EDITOR
    [CustomEditor(typeof(ShenmuePak))]
    public class ShenmuePakEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var smar = (ShenmuePak)target;

            GUILayout.Label("PAKS");
            smar.m_pakRef.DoInspectorGUI(TACReader.FileType.PAKS, smar.OnChange);

            GUILayout.Label("PAKF");
            smar.m_textureSet.DoInspectorGUI(TACReader.FileType.PAKF, smar.OnChange);

            DrawDefaultInspector();
        }
    }
#endif
}