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
        string m_pathCreated;
        Transform m_mesh;
        const float SHENMUE_FLIP = -1.0f;

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
            if (m_pathCreated != m_path)
            {
                var children = new Transform[transform.childCount];
                for (int i = 0; i < children.Length; i++)
                {
                    children[i] = transform.GetChild(i);
                }

                foreach (var child in children)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
            m_pathCreated = m_path;

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
            Transform[] existingNodes = GetComponentsInChildren<Transform>();

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

                nodes[id] = CreateBone(id, node, parent, existingNodes);
                numberVerts += node.m_totalStripVerts;
                bones[index++] = nodes[id];
            }

            Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
            for(int i = 0; i < bones.Length; i++)
            {
                bindPoses[i] = Matrix4x4.identity;
            }
            
            var mesh = new Mesh();
            mesh.bindposes = bindPoses;
            var verts = new Vector3[numberVerts];
            var norms = new Vector3[numberVerts];
            var boneWeights = new BoneWeight[numberVerts];
            var uvs = new List<Vector2>();

            var vertLookup = new Dictionary<Vector3, List<int>>();
            var bounds = new Bounds();
            
            int numberVertsEmitted = 0;
            int nodeIndex = 0;
            int sourceBaseVert = 0;
            foreach (var node in model.m_nodeInLoadOrder)
            {
                foreach (var face in node.m_strips)
                {
                    foreach (var fv in face.m_faceVerts)
                    {
                        Vector3 pos = Vector3.zero;
                        Vector3 norm = Vector3.zero;
                        int boneIndex = nodeIndex;

                        if (fv.m_vertIndex < 0)
                        {
                            if (node.up != 0)
                            {
                                var parent = model.m_nodes[node.up];
                                int id = parent.m_pos.Count + fv.m_vertIndex;

                                if(id >= 0 && id < parent.m_pos.Count)
                                {
                                    pos = parent.m_pos[id];
                                    norm = parent.m_norm[id];
                                    boneIndex = Array.IndexOf(bones, nodes[node.up]);
                                }
                            }
                        }
                        else
                        {
                            pos = node.m_pos[fv.m_vertIndex];
                            norm = node.m_norm[fv.m_vertIndex];
                            boneIndex = nodeIndex;
                        }

                        pos.x *= SHENMUE_FLIP;
                        norm.x *= SHENMUE_FLIP;

                        int oldVertsEmitted = numberVertsEmitted;
                        fv.m_vertIndex = AddVert(vertLookup, pos, norm, fv.m_uv, boneIndex, verts, norms, uvs, boneWeights, ref numberVertsEmitted);
                        if(numberVertsEmitted > oldVertsEmitted)
                        {
                            var local = transform.InverseTransformPoint(bones[boneIndex].TransformPoint(pos));
                            bounds.Encapsulate(local);
                        }

                    }
                }
                nodeIndex++;
                sourceBaseVert += node.m_pos.Count;
            }

            Array.Resize(ref verts, numberVertsEmitted);
            Array.Resize(ref norms, numberVertsEmitted);
            Array.Resize(ref boneWeights, numberVertsEmitted);

            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.boneWeights = boneWeights;
            mesh.SetUVs(0, uvs);

            mesh.subMeshCount = model.m_textures.Count;

            List<int> inds = new List<int>();

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int baseVert = 0;
                inds.Clear();

                foreach (var node in model.m_nodeInLoadOrder)
                {
                    foreach (var strip in node.m_strips)
                    {
                        if (strip.m_texture == subMesh)
                        {
                            for (int i = 0; i < strip.m_faceVerts.Count - 2; i++)
                            {
                                inds.Add(strip.m_faceVerts[i].m_vertIndex);
                                if ((i & 1) != (strip.m_flipped ? 0 : 1))
                                {
                                    inds.Add(strip.m_faceVerts[i + 1].m_vertIndex);
                                    inds.Add(strip.m_faceVerts[i + 2].m_vertIndex);
                                }
                                else
                                {
                                    inds.Add(strip.m_faceVerts[i + 2].m_vertIndex);
                                    inds.Add(strip.m_faceVerts[i + 1].m_vertIndex);
                                }
                            }
                        }

                        baseVert += strip.m_faceVerts.Count;
                    }
                }

                mesh.SetIndices(inds.ToArray(), MeshTopology.Triangles, subMesh);
            }

            if(!m_mesh)
            {
                var meshNode = new GameObject("Mesh");
                m_mesh = meshNode.transform;
                m_mesh.parent = transform;
                m_mesh.localPosition = Vector3.zero;
                m_mesh.localRotation = Quaternion.identity;
                m_mesh.localScale = Vector3.one;
                meshNode.hideFlags = HideFlags.DontSave;
            }

            var mr = m_mesh.GetComponent<SkinnedMeshRenderer>();
            if (!mr)
            {
                mr = m_mesh.gameObject.AddComponent<SkinnedMeshRenderer>();
            }
            mr.sharedMesh = null;
            mr.sharedMesh = mesh;
            mr.rootBone = bones[0];
            mr.materials = mats;
            mr.bones = bones;
            mr.localBounds = bounds;
        }

        int AddVert(Dictionary<Vector3, List<int>> lookup, Vector3 pos, Vector3 norm, Vector2 uv, int boneIndex, Vector3[] poss, Vector3[] norms, List<Vector2> uvs, BoneWeight[] boneIndices, ref int count)
        {
            if (lookup.ContainsKey(pos))
            {
                foreach (int v in lookup[pos])
                {
                    if (norm == norms[v] && uv == uvs[v] && boneIndex == boneIndices[v].boneIndex0)
                    {
                        return v;
                    }
                }
            }

            if(!lookup.ContainsKey(pos))
            {
                lookup[pos] = new List<int>();
            }

            poss[count] = pos;
            norms[count] = norm;
            uvs.Add(uv);
            boneIndices[count].boneIndex0 = boneIndex;
            boneIndices[count].weight0 = 1.0f;

            lookup[pos].Add(count);

            count++;

            return count - 1;
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

        Transform CreateBone(uint id, Model.Node node, Transform parent, Transform[] existingBones)
        {
            string name = id.ToString();

            var bone = existingBones.FirstOrDefault(x => x.name == name);
            if(!bone)
            {
                var go = new GameObject(name);
                bone = go.transform;

                bone.parent = parent;
                bone.localPosition = new Vector3(node.x * SHENMUE_FLIP, node.y, node.z);
                bone.localScale = new Vector3(node.scaleX, node.scaleY, node.scaleZ);
                bone.localEulerAngles = new Vector3(0, 0, 0);
                bone.Rotate(Vector3.forward, node.rotZ * SHENMUE_FLIP);
                bone.Rotate(Vector3.up, node.rotY * SHENMUE_FLIP);
                bone.Rotate(Vector3.right, node.rotX);
            }
            
            return bone;
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