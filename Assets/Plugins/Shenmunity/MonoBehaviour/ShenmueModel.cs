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
        public enum MeshMode
        {
            Skinned,
            Static,
            Individual,
        };

        public MeshMode m_meshMode;

        [SerializeField]
        string m_pathCreated;

        public const float SHENMUE_FLIP = -1.0f;
        
        Transform[] m_bones;

        [MenuItem("GameObject/Shenmunity/Model", priority = 10)]
        public static void CreateShenmueModel()
        {
            var sm = new GameObject("Shenmue model");
            TACFileSelector.SelectFile(TACReader.FileType.MODEL, sm.AddComponent<ShenmueModel>());
        }

        private void Awake()
        {
            LoadModel();
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

            var model = new MT5(m_path);

            var nodes = new Dictionary<uint, Transform>();

            Material[] mats = new Material[model.m_textures.Count * 2];

            for (int mirror = 0; mirror < 2; mirror++)
            {
                for (int i = 0; i < model.m_textures.Count; i++)
                {
                    var srcTex = model.m_textures[i];
                    var tex = new Texture2D((int)srcTex.m_width, (int)srcTex.m_height);
                    tex.SetPixels(srcTex.m_texels);
                    tex.Apply();

                    if(mirror != 0)
                    {
                        tex.wrapModeU = TextureWrapMode.Mirror;
                        tex.wrapModeV = TextureWrapMode.Mirror;
                    }

                    var mat = new Material(Shader.Find("Standard"));
                    switch (srcTex.m_type)
                    {
                        case MT5.PVRType.ARGB4444:
                            SetTransparent(mat);
                            break;
                        case MT5.PVRType.ARGB1555:
                            SetCutout(mat);
                            break;
                    }
                    mat.SetFloat("_Glossiness", 0);

                    mat.SetTexture("_MainTex", tex);
                    mats[i + mirror * model.m_textures.Count] = mat;
                }
            }

            m_bones = new Transform[model.m_nodes.Count];
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
                m_bones[index++] = nodes[id];

                nodes[id].GetComponent<ShenmueTransform>().GenerateCollider(node);

                foreach (var strip in node.m_strips)
                {
                    if(strip.m_mirrorUVs)
                    {
                        strip.m_texture += model.m_textures.Count;
                    }
                }
            }

            if (m_meshMode == MeshMode.Individual)
            {
                int boneIndex = 0;
                foreach (var node in model.m_nodeInLoadOrder)
                {
                    Bounds bounds;
                    Material[] meshMats;
                    var mesh = CreateMeshForNodes(transform, model, new MT5.Node[] { node }, new Transform[] { m_bones[boneIndex] }, nodes, out bounds, mats, out meshMats);

                    SetMeshToGameObject(m_bones[boneIndex], mesh, meshMats, bounds);
                    boneIndex++;
                }
                PurgeMesh(transform);
            }
            else
            {
                foreach(var bone in m_bones)
                {
                    PurgeMesh(bone);
                }

                Bounds bounds;
                Material[] meshMats;
                var mesh = CreateMeshForNodes(transform, model, model.m_nodeInLoadOrder.ToArray(), m_bones, nodes, out bounds, mats, out meshMats);

                SetMeshToGameObject(transform, mesh, meshMats, bounds);
            }
        }

        void PurgeMesh(Transform root)
        {
            var destroy = new List<GameObject>();
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.GetComponent<MeshRenderer>() || child.GetComponent<SkinnedMeshRenderer>())
                    destroy.Add(child.gameObject);
            }
            foreach (var go in destroy)
            {
                DestroyImmediate(go);
            }
        }

        void SetMeshToGameObject(Transform root, Mesh mesh, Material[] mats, Bounds bounds)
        {
            PurgeMesh(root);

            var meshNode = new GameObject("Mesh");
            var meshT = meshNode.transform;
            meshT.parent = root;
            meshT.localPosition = Vector3.zero;
            meshT.localRotation = Quaternion.identity;
            meshT.localScale = Vector3.one;
            meshNode.hideFlags = HideFlags.DontSave;

            if (m_meshMode == MeshMode.Skinned)
            {
                var mr = meshT.gameObject.AddComponent<SkinnedMeshRenderer>();
                mr.sharedMesh = mesh;
                mr.rootBone = m_bones[0];
                mr.materials = mats;
                mr.bones = m_bones;
                mr.localBounds = bounds;
            }
            else
            {
                var mr = meshT.gameObject.AddComponent<MeshRenderer>();
                mr.materials = mats;
                var mf = meshT.gameObject.AddComponent<MeshFilter>();
                mf.mesh = mesh;
            }
        }

        Mesh CreateMeshForNodes(Transform root, MT5 model, MT5.Node[] nodes, Transform[] bones, Dictionary<uint, Transform> allNodes, out Bounds bounds, Material[] allMats, out Material[] mats)
        {
            var usedMaterials = new Dictionary<int, bool>();

            int numberVerts = 0;
            foreach (var node in nodes)
            {
                numberVerts += node.m_totalStripVerts;
            }

            Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
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
            bounds = new Bounds();

            int numberVertsEmitted = 0;
            int nodeIndex = 0;
            int sourceBaseVert = 0;
            foreach (var node in nodes)
            {
                foreach (var strip in node.m_strips)
                {
                    if (strip.m_texture < allMats.Length) //materials out side this bound appear to be volumes of some kind... maybe PVS blockers?
                    {
                        usedMaterials[strip.m_texture] = true;
                    }

                    foreach (var fv in strip.m_stripVerts)
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

                                if (id >= 0 && id < parent.m_pos.Count)
                                {
                                    pos = parent.m_pos[id];
                                    norm = parent.m_norm[id];
                                    boneIndex = Array.IndexOf(bones, allNodes[node.up]);
                                    if (boneIndex == -1)
                                    {
                                        Debug.LogWarning("Parent bone doesn't exist... are you building a skinned model as individual parts??");
                                        boneIndex = 0;
                                    }
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

                        var modelLocal = root.InverseTransformPoint(bones[boneIndex].TransformPoint(pos));
                        if (m_meshMode == MeshMode.Static)
                        {
                            pos = modelLocal;
                            norm = root.InverseTransformDirection(bones[boneIndex].TransformDirection(norm));
                        }

                        int oldVertsEmitted = numberVertsEmitted;
                        fv.m_vertIndex = AddVert(vertLookup, pos, norm, fv.m_uv, boneIndex, verts, norms, uvs, boneWeights, ref numberVertsEmitted);
                        if (numberVertsEmitted > oldVertsEmitted)
                        {
                            bounds.Encapsulate(modelLocal);
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
            if (m_meshMode == MeshMode.Skinned)
            {
                mesh.boneWeights = boneWeights;
            }
            mesh.SetUVs(0, uvs);

            mesh.subMeshCount = usedMaterials.Count;
            mats = new Material[usedMaterials.Count];

            int matId = 0;
            foreach (var mat in usedMaterials.Keys)
            {
                mats[matId++] = allMats[mat];
            }

            List<int> inds = new List<int>();

            int subMesh = 0;
            foreach (var mat in usedMaterials.Keys)
            {
                int baseVert = 0;
                inds.Clear();

                foreach (var node in nodes)
                {
                    foreach (var strip in node.m_strips)
                    {
                        if (strip.m_texture == mat)
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

                        baseVert += strip.m_stripVerts.Count;
                    }
                }

                mesh.SetIndices(inds.ToArray(), MeshTopology.Triangles, subMesh);
                subMesh++;
            }

            return mesh;
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

        Transform CreateBone(uint id, MT5.Node node, Transform parent, Transform[] existingBones)
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

                bone.gameObject.AddComponent<ShenmueTransform>();
            }
            
            return bone;
        }

        public void CreateAvatar()
        {
            var hd = new HumanDescription();

            var mapping = new List<HumanBone>();
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

            //if(GUILayout.Button("Create Avatar"))
            //{
            //    smar.CreateAvatar();
            //}

            DrawDefaultInspector();
        }
    }
#endif
}