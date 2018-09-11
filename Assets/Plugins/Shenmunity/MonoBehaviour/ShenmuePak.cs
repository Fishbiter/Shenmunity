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
    public class ShenmuePak : ShenmueAssetRef
    {
        [SerializeField]
        [HideInInspector]
        ShenmueModel[] m_models;

        [HideInInspector]
        public ShenmueAssetRef m_textureSet;

        public override void OnChange()
        {
            if (m_models != null)
            {
                foreach (var m in m_models)
                {
                    DestroyImmediate(m.gameObject);
                }
                m_models = null;
            }

            if (string.IsNullOrEmpty(m_path))
            {
                return;
            }

            var models = TACReader.GetFiles(TACReader.FileType.MODEL);
            var parent = TACReader.GetEntry(m_path);

            if(!string.IsNullOrEmpty(m_textureSet.m_path))
            {
                TACReader.SetTextureNamespace(m_textureSet.m_path);
            }

            var createdModels = new List<ShenmueModel>();

            foreach (var m in models)
            {
                if(m.m_parent == parent)
                {
                    var sm = new GameObject(m.m_path).AddComponent<ShenmueModel>();
                    sm.transform.parent = transform;
                    sm.m_path = m.m_path;
                    sm.OnChange();

                    createdModels.Add(sm);
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

            smar.DoInspectorGUI(TACReader.FileType.PAKS);

            if(!smar.m_textureSet)
            {
                smar.m_textureSet = smar.gameObject.AddComponent<ShenmueAssetRef>();
            }
            smar.m_textureSet.DoInspectorGUI(TACReader.FileType.PAKF);

            DrawDefaultInspector();
        }
    }
#endif
}