using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shenmunity
{
    [SelectionBase]
    public class ShenmueTransform : MonoBehaviour
    {
        public enum CollisionType
        {
            None,
            Box,
            Mesh,
        };

        public CollisionType m_collisionType;
        [HideInInspector]
        public string m_humanBone = "";

        [SerializeField]
        Collider m_collider;

        public void GenerateCollider(Model.Node node)
        {
            if (m_collider)
            {
                DestroyImmediate(m_collider);
                m_collider = null;
            }

            if (m_collisionType == CollisionType.None)
            {
                return;
            }
            else if (m_collisionType == CollisionType.Box)
            {
                GenerateBoxCollider(node);
            }
            else if (m_collisionType == CollisionType.Mesh)
            {
                GenerateMeshCollider(node);
            }
        }

        void GenerateBoxCollider(Model.Node node)
        {
            Bounds bounds = new Bounds();
            foreach (var strip in node.m_strips)
            {
                foreach(var vert in strip.m_stripVerts)
                {
                    if (vert.m_vertIndex >= 0)
                    {
                        var vertPos = node.m_pos[vert.m_vertIndex];
                        vertPos.x *= ShenmueModel.SHENMUE_FLIP;
                        bounds.Encapsulate(vertPos);
                    }
                }
            }
            var bc = gameObject.AddComponent<BoxCollider>();
            bc.center = bounds.center;
            bc.size = bounds.extents * 2;

            m_collider = bc;
        }

        void GenerateMeshCollider(Model.Node node)
        {
            var mesh = new Mesh();
            Vector3[] pos = new Vector3[node.m_pos.Count];
            var inds = new List<int>();

            for (int v = 0; v < node.m_pos.Count; v++)
            {
                pos[v] = node.m_pos[v];
                pos[v].x *= -1;
            }

            foreach (var strip in node.m_strips)
            {
                for (int i = 0; i < strip.m_stripVerts.Count - 2; i++)
                {
                    inds.Add(strip.m_stripVerts[i].m_vertIndex);
                    if ((i & 1) != 1)
                    {
                        inds.Add(strip.m_stripVerts[i + 1].m_vertIndex);
                        inds.Add(strip.m_stripVerts[i + 2].m_vertIndex);
                    }
                    else
                    {
                        inds.Add(strip.m_stripVerts[i + 2].m_vertIndex);
                        inds.Add(strip.m_stripVerts[i + 1].m_vertIndex);
                    }
                }
            }

            mesh.vertices = pos;
            mesh.SetIndices(inds.ToArray(), MeshTopology.Triangles, 0);

            var mc = gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            m_collider = mc;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ShenmueTransform))]
    public class ShenmueTransformEditor : Editor
    {
        Vector2 m_boneScroll;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var st = (ShenmueTransform)target;

            m_boneScroll = GUILayout.BeginScrollView(m_boneScroll);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            DoBoneName(st, "");
            int index = 0;
            int count = HumanTrait.BoneName.Length;
            foreach (var boneName in HumanTrait.BoneName)
            {
                DoBoneName(st, boneName);

                if ((index++ % (count / 3 + 1)) == count / 3)
                {
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
        }

        void DoBoneName(ShenmueTransform st, string boneName)
        {
            if (st.m_humanBone == boneName)
            {
                GUILayout.Label(boneName);
            }
            else if (GUILayout.Button(boneName))
            {
                st.m_humanBone = boneName;
            }
        }
    }
#endif
}