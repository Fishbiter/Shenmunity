using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shenmunity
{
    [ExecuteInEditMode][SelectionBase]
    public class ShenmueModel : ShenmueAssetRef
    {
        public bool m_allowEdit = false;
        
        private void Awake()
        {
            //LoadModel();
        }

        public override void OnChange()
        {
            LoadModel();
        }
        

        void LoadModel()
        {
            var children = new Transform[transform.childCount];
            for(int i = 0; i < children.Length; i++)
            {
                children[i] = transform.GetChild(i);
            }

            foreach (var child in children)
            {
                DestroyImmediate(child.gameObject);
            }

            if (string.IsNullOrEmpty(m_path))
                return;

            var model = new Model(m_path);

            var nodes = new Dictionary<uint, Transform>();

            Material[] mats = new Material[model.m_textures.Count];

            for (int i = 0; i < model.m_textures.Count; i++)
            {
                var srcTex = model.m_textures[i];
                var tex = new Texture2D((int)srcTex.m_width, (int)srcTex.m_height);
                tex.SetPixels(srcTex.m_texels);
                tex.Apply();

                var mat = new Material(Shader.Find("Standard"));
                switch (srcTex.m_type)
                {
                    case Model.PVRType.ARGB4444:
                        SetTransparent(mat);
                        break;
                    case Model.PVRType.ARGB1555:
                        SetCutout(mat);
                        break;
                }
                mat.SetFloat("_Glossiness", 0);

                mat.SetTexture("_MainTex", tex);
                mats[i] = mat;
            }

            foreach (var id in model.m_nodes.Keys)
            {
                Transform parent;
                uint parentId = model.m_nodes[id].up;
                if (parentId != 0)
                {
                    parent = nodes[parentId];
                }
                else
                {
                    parent = transform;
                }

                nodes[id] = CreateNode(model, model.m_nodes[id], mats, parent);
            }
        }

        void SetTransparent(Material material)
        {
            material.SetFloat("_Mode", 3);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }

        void SetCutout(Material material)
        {
            material.SetFloat("_Mode", 1);
            material.EnableKeyword("_ALPHATEST_ON");
        }

        Transform CreateNode(Model model, Model.Node node, Material[] mats, Transform parent)
        {
            var go = new GameObject("Shenmunity Model");

            go.hideFlags = HideFlags.DontSave;
            if (!m_allowEdit)
            {
                go.hideFlags |= HideFlags.NotEditable;
            }

            go.transform.parent = parent;
            go.transform.localPosition = new Vector3(node.x, node.y, node.z);
            go.transform.localScale = new Vector3(node.scaleX, node.scaleY, node.scaleZ);
            go.transform.localEulerAngles = new Vector3(node.rotX, node.rotY, node.rotZ);

            var mf = go.GetComponent<MeshFilter>();
            if (!mf)
            {
                mf = go.AddComponent<MeshFilter>();
            }
            var mr = go.GetComponent<MeshRenderer>();
            if (!mr)
            {
                mr = go.AddComponent<MeshRenderer>();
            }
            var mesh = new Mesh();
            var verts = new Vector3[node.m_totalFaceVerts];
            var norms = new Vector3[node.m_totalFaceVerts];
            var uv = new List<Vector2>();

            Dictionary<int, bool> textures = new Dictionary<int, bool>();

            int v = 0;
            foreach (var face in node.m_faces)
            {
                textures[face.m_texture] = true;
                foreach (var fv in face.m_faceVerts)
                {
                    if(fv.m_vertIndex < 0 || fv.m_vertIndex >= node.m_pos.Count)
                    {
                        Debug.LogWarningFormat("Invalid vert index {0}", fv.m_vertIndex);
                    }

                    verts[v] = node.m_pos[fv.m_vertIndex];
                    norms[v] = node.m_pos[fv.m_vertIndex];
                    uv.Add(fv.m_uv);
                    v++;
                }
            }

            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.SetUVs(0, uv);

            var texIds = textures.Keys.ToArray();

            mesh.subMeshCount = texIds.Length;

            List<int> inds = new List<int>();

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                inds.Clear();

                int baseVert = 0;
                int texId = texIds[subMesh];
                foreach (var face in node.m_faces)
                {
                    if (face.m_texture == texId)
                    {
                        for (int i = 0; i < face.m_faceVerts.Count - 2; i++)
                        {
                            inds.Add(baseVert + i);
                            if ((i & 1) != (face.m_flipped ? 1 : 0))
                            {
                                inds.Add(baseVert + i + 1);
                                inds.Add(baseVert + i + 2);
                            }
                            else
                            {
                                inds.Add(baseVert + i + 2);
                                inds.Add(baseVert + i + 1);
                            }
                        }
                    }
                    baseVert += face.m_faceVerts.Count;
                }

                mesh.SetIndices(inds.ToArray(), MeshTopology.Triangles, subMesh);
            }

            mf.mesh = null;
            mf.mesh = mesh;

            var myMats = new Material[texIds.Length];
            for(int i = 0; i < texIds.Length; i++)
            {
                if (texIds[i] < mats.Length)
                {
                    myMats[i] = mats[texIds[i]];
                }
            }

            mr.materials = myMats;

            return mr.transform;
        }
    }


#if UNITY_EDITOR
    [CustomEditor(typeof(ShenmueModel))]
    public class ShenmueModelEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var smar = (ShenmueModel)target;

            smar.DoInspectorGUI(TACReader.FileType.MODEL);

            DrawDefaultInspector();
        }
    }
#endif
}