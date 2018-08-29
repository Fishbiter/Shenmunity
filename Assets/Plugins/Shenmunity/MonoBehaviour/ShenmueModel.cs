using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
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

            Transform[] bones = new Transform[model.m_nodes.Count];

            int numberVerts = 0;
            int index = 0;
            foreach (var id in model.m_nodes.Keys)
            {
                var node = model.m_nodes[id];
                Transform parent;
                uint parentId = node.up;
                if (parentId != 0)
                {
                    parent = nodes[parentId];
                }
                else
                {
                    parent = transform;
                }

                nodes[id] = CreateNode(node, parent);
                nodes[id].name = id.ToString();
                numberVerts += node.m_totalFaceVerts;
                bones[index++] = nodes[id];
            }

            Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
            for(int i = 0; i < bones.Length; i++)
            {
                bindPoses[i] = Matrix4x4.identity;// bones[i].worldToLocalMatrix * transform.localToWorldMatrix;
            }
            
            var mr = GetComponent<SkinnedMeshRenderer>();
            if (!mr)
            {
                mr = gameObject.AddComponent<SkinnedMeshRenderer>();
            }
            var mesh = new Mesh();
            mesh.bindposes = bindPoses;
            var verts = new Vector3[numberVerts];
            var norms = new Vector3[numberVerts];
            var boneWeights = new BoneWeight[numberVerts];
            var uv = new List<Vector2>();

            int v = 0;
            int nodeIndex = 0;
            int sourceBaseVert = 0;
            foreach (var node in model.m_nodeInLoadOrder)
            {
                foreach (var face in node.m_faces)
                {
                    foreach (var fv in face.m_faceVerts)
                    {
                        if (fv.m_vertIndex < 0)
                        {
                            if (node.up != 0)
                            {
                                var parent = model.m_nodes[node.up];
                                int id = parent.m_pos.Count + fv.m_vertIndex;

                                if(id >= 0 && id < parent.m_pos.Count)
                                {
                                    verts[v] = parent.m_pos[id];
                                    norms[v] = parent.m_norm[id];
                                    boneWeights[v].boneIndex0 = Array.IndexOf(bones, nodes[node.up]);
                                }
                            }


                            //int boneIndex = 0;
                            //GetOtherNodeVert(fv.m_vertIndex + sourceBaseVert, model, out verts[v], out norms[v], out boneIndex);
                            //boneWeights[v].boneIndex0 = boneIndex;
                        }
                        else
                        {
                            verts[v] = node.m_pos[fv.m_vertIndex];
                            norms[v] = node.m_norm[fv.m_vertIndex];
                            boneWeights[v].boneIndex0 = nodeIndex;
                        }
                        boneWeights[v].weight0 = 1.0f;
                        uv.Add(fv.m_uv);
                        v++;
                    }
                }
                nodeIndex++;
                sourceBaseVert += node.m_pos.Count;
            }

            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.boneWeights = boneWeights;
            mesh.SetUVs(0, uv);

            mesh.subMeshCount = model.m_textures.Count;

            List<int> inds = new List<int>();

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int baseVert = 0;
                inds.Clear();

                foreach (var node in model.m_nodeInLoadOrder)
                {
                    foreach (var face in node.m_faces)
                    {
                        if (face.m_texture == subMesh)
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
                }

                mesh.SetIndices(inds.ToArray(), MeshTopology.Triangles, subMesh);
            }

            mr.sharedMesh = null;
            mr.sharedMesh = mesh;
            mr.rootBone = bones[0];
            mr.materials = mats;
            mr.bones = bones;
        }

        void GetOtherNodeVert(int absVertId, Model model, out Vector3 pos, out Vector3 norm, out int boneIndex)
        {
            int nodeIndex = 0;
            int baseVert = 0;
            foreach (var node in model.m_nodeInLoadOrder)
            {
                int vertId = absVertId - baseVert;
                if (vertId >= 0 && vertId < node.m_pos.Count)
                {
                    pos = node.m_pos[vertId];
                    norm = node.m_norm[vertId];
                    boneIndex = nodeIndex;
                    return;
                }
                baseVert += node.m_pos.Count;
                
                nodeIndex++;
            }

            pos = Vector3.zero ;
            norm = Vector3.one;
            boneIndex = 0;
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

        Transform CreateNode(Model.Node node, Transform parent)
        {
            var go = new GameObject("Shenmunity Bone");

            go.hideFlags = HideFlags.DontSave;
            if (!m_allowEdit)
            {
                go.hideFlags |= HideFlags.NotEditable;
            }

            go.transform.parent = parent;
            go.transform.localPosition = new Vector3(node.x, node.y, node.z);
            go.transform.localScale = new Vector3(node.scaleX, node.scaleY, node.scaleZ);
            go.transform.localEulerAngles = new Vector3(0, 0, 0);
            go.transform.Rotate(Vector3.forward, node.rotZ);
            go.transform.Rotate(Vector3.up, node.rotY);
            go.transform.Rotate(Vector3.right, node.rotX);
            
            return go.transform;
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