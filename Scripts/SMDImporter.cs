//=================================================================================
// Copyright (c) 2025 Generalisk
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//=================================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.Rendering;

namespace Generalisk.Editor.Importers.SMD
{
    [ScriptedImporter(1, "smd")]
    public class SMDImporter : ScriptedImporter
    {
        public const float ROTATION_REFERENCE = 1.570796f;

        // Model Properties
        public float scale = 1;
        public bool forceRig = false;

        // Material Properties
        public string[] materialNames = { };
        public Material[] materials = { };

        public override void OnImportAsset(AssetImportContext ctx)
        {
            // Embracing my inner Valve by making this code A mess
            // -Generalik 10/08/2025

            // Setup Variables
            string modelName = new DirectoryInfo(ctx.assetPath).Name;
            modelName = modelName.Substring(0, modelName.Length - 4);

            string[] lines = File.ReadAllLines(ctx.assetPath);

            string block = "";

            Mesh mesh = new Mesh();

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<int> submeshes = new List<int>();
            List<Matrix4x4> bindposes = new List<Matrix4x4>();

            List<SMDBone> boneRefs = new List<SMDBone>();
            List<Transform> bones = new List<Transform>();
            List<BoneWeight> weights = new List<BoneWeight>();

            List<Material> materials = new List<Material>();
            List<string> matNames = new List<string>();

            int currentMatIndex = 0;

            // Read File
            foreach (string line in lines)
            {
                string lineNoComment = line.Split("//")[0];

                string[] i = lineNoComment.Split(" ");
                i = i.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

                if (i.Length <= 0) { continue; }

                if (i.Length == 1 && i[0] == "end")
                { block = ""; continue; }

                switch (block)
                {
                    case "":
                        if (i.Length == 1)
                        { block = i[0]; }
                        break;
                    case "nodes":
                        string boneName = i[1];
                        if (boneName.StartsWith("\""))
                        { boneName = boneName.Substring(1); }
                        if (boneName.EndsWith("\""))
                        { boneName = boneName.Substring(0, boneName.Length - 1); }
                        boneRefs.Add(new SMDBone(boneName, int.Parse(i[2])));
                        break;
                    case "skeleton":
                        // TODO: Add Animation Support
                        if (i[0].ToLower() == "time") continue;

                        boneRefs[int.Parse(i[0])].position = new Vector3(float.Parse(i[1]) * scale, float.Parse(i[2]) * scale, float.Parse(i[3]) * scale);

                        float rotAngleX = (90f / ROTATION_REFERENCE) * float.Parse(i[4]);
                        float rotAngleY = (90f / ROTATION_REFERENCE) * float.Parse(i[5]);
                        float rotAngleZ = (90f / ROTATION_REFERENCE) * float.Parse(i[6]);
                        boneRefs[int.Parse(i[0])].rotation = new Vector3(rotAngleX, rotAngleY, rotAngleZ);
                        break;
                    case "triangles":
                        if (i.Length == 1)
                        {
                            if (!matNames.Contains(i[0]))
                            {
                                Material mat;
                                if (materialNames.Contains(i[0]))
                                {
                                    int cachedMatIndex = materialNames.ToList().FindIndex(x => x == i[0]);
                                    mat = this.materials[cachedMatIndex];

                                    if (mat == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat.GetEntityId())))
                                    {
                                        // If render pipeline (URP, HDRP etc.) is used, used the pipelines default shader.
                                        // If no pipeline is used (catch null reference), use built-in standard shader.
                                        try { mat = new Material(GraphicsSettings.defaultRenderPipeline.defaultShader); }
                                        catch { mat = new Material(Shader.Find("Standard")); }
                                        mat.name = i[0];
                                    }
                                }
                                else
                                {
                                    try { mat = new Material(GraphicsSettings.defaultRenderPipeline.defaultShader); }
                                    catch { mat = new Material(Shader.Find("Standard")); }
                                    mat.name = i[0];
                                }

                                matNames.Add(i[0]);
                                materials.Add(mat);
                                currentMatIndex = matNames.Count - 1;
                            }
                            else { currentMatIndex = matNames.FindIndex(x => x == i[0]); }

                            break;
                        }

                        Vector3 vert = new Vector3(float.Parse(i[1]) * scale, float.Parse(i[2]) * scale, float.Parse(i[3]) * scale);
                        Vector3 normal = new Vector3(float.Parse(i[4]), float.Parse(i[5]), float.Parse(i[6]));
                        Vector2 uv = new Vector2(float.Parse(i[7]), float.Parse(i[8]));
                        BoneWeight bone = new BoneWeight();

                        // Some smd files, such as physics meshes decompiled by Crowbar, do not have A weight link value.
                        // So, as an alternative, in the event that, for any reason, the 
                        if (i.Length >= 10)
                        {
                            int boneCount = int.Parse(i[9]);

                            // TODO: Replace with newer BlendWeights1 System
                            // I did try, but this kept on causing the weights
                            // that aren't 0 or 1 to completely break.
                            // Also, I kept on getting an importing error.
                            // So, i'm taking my chances with the old and
                            // outdated method for now.
                            if (boneCount > 0)
                            {
                                bone.boneIndex0 = int.Parse(i[10]);
                                bone.weight0 = float.Parse(i[11]);
                            }
                            if (boneCount > 1)
                            {
                                bone.boneIndex1 = int.Parse(i[12]);
                                bone.weight1 = float.Parse(i[13]);
                            }
                            if (boneCount > 2)
                            {
                                bone.boneIndex2 = int.Parse(i[14]);
                                bone.weight2 = float.Parse(i[15]);
                            }
                            if (boneCount > 3)
                            {
                                bone.boneIndex3 = int.Parse(i[16]);
                                bone.weight3 = float.Parse(i[17]);
                            }
                            if (boneCount > 4)
                            { Debug.LogWarningFormat("SMD Importer: Vertex contains too many bone weights. {0} provided, 4 maximum", boneCount); }
                        }
                        else
                        {
                            bone = new BoneWeight()
                            {
                                boneIndex1 = int.Parse(i[0]),
                                weight1 = 1,
                            };
                        }

                        if (verts.Contains(vert))
                        {
                            int vertIndex = verts.FindIndex(x => x == vert);
                            if (uvs[vertIndex] == uv && normals[vertIndex] == normal)
                            { tris.Add(vertIndex); submeshes.Add(currentMatIndex); break; }
                        }

                        verts.Add(vert);
                        tris.Add(verts.Count - 1);
                        uvs.Add(uv);
                        normals.Add(normal);
                        submeshes.Add(currentMatIndex);
                        weights.Add(bone);

                        break;
                    default: Debug.LogError(string.Format("SMD Importer: Unknown Block \"{0}\"!", block)); break;
                }
            }

            bool includeRig = boneRefs.Count > 1 || forceRig;

            // Create Main Object
            GameObject obj = new GameObject(modelName);
            ctx.AddObjectToAsset("main", obj);
            ctx.SetMainObject(obj);

            // Create Mesh Object
            GameObject meshObj;
            if (includeRig)
            {
                meshObj = new GameObject(modelName);
                meshObj.transform.SetParent(obj.transform);
                ctx.AddObjectToAsset("model", meshObj);
            }
            else { meshObj = obj; }

            // Create Rig/Skeleton
            if (includeRig)
            {
                GameObject skeleton = new GameObject(modelName + "_skeleton");
                skeleton.transform.SetParent(obj.transform);
                ctx.AddObjectToAsset("skeleton", skeleton);

                foreach (SMDBone bone in boneRefs)
                {
                    Transform boneObj = new GameObject(bone.name).transform;
                    if (bone.parent < 0) { boneObj.parent = skeleton.transform; }
                    else { boneObj.parent = bones[bone.parent]; }
                    boneObj.localPosition = bone.position;
                    boneObj.localEulerAngles = bone.rotation;
                    boneObj.localScale = Vector3.one;

                    ctx.AddObjectToAsset(bone.name, boneObj);
                    bindposes.Add(boneObj.worldToLocalMatrix * meshObj.transform.localToWorldMatrix);
                    bones.Add(boneObj);
                }
            }

            // Setup Mesh
            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.normals = normals.ToArray();
            mesh.subMeshCount = materials.Count;
            if (includeRig)
            {
                mesh.bindposes = bindposes.ToArray();
                mesh.boneWeights = weights.ToArray();
            }

            // Not really optimal but idk any better way to do this...
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                List<int> targetTris = new List<int>();
                for (int x = 0; x < submeshes.Count; x++)
                {
                    if (submeshes[x] == i)
                    { targetTris.Add(tris[x]); }
                }
                mesh.SetTriangles(targetTris, i);
            }

            //mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            mesh.name = modelName;

            ctx.AddObjectToAsset("mesh", mesh);

            // Add Materials
            foreach (Material mat in materials)
            {
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat.GetEntityId())))
                { ctx.AddObjectToAsset(mat.name + " (material)", mat); }
            }

            // Add Mesh to Object
            if (includeRig)
            {
                meshObj.AddComponent<SkinnedMeshRenderer>().sharedMesh = mesh;
                meshObj.GetComponent<SkinnedMeshRenderer>().SetSharedMaterials(materials);
                meshObj.GetComponent<SkinnedMeshRenderer>().bones = bones.ToArray();
                meshObj.GetComponent<SkinnedMeshRenderer>().rootBone = bones[0];
                meshObj.GetComponent<SkinnedMeshRenderer>().bounds = mesh.bounds;
            }
            else
            {
                meshObj.AddComponent<MeshFilter>().sharedMesh = mesh;
                meshObj.AddComponent<MeshRenderer>().SetSharedMaterials(materials);
                meshObj.GetComponent<MeshRenderer>().bounds = mesh.bounds;
            }

            // Clean up
            materialNames = matNames.ToArray();
            this.materials = materials.ToArray();
        }
    }
}
