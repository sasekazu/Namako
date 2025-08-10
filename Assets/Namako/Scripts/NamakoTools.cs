
using UnityEngine;

namespace Namako
{

    [System.Serializable]
    public class MeshForJSON
    {
        public float[] pos;
        public int[] tet;
    }

    [System.Serializable]
    public class MeshExtractor
    {
        public int n_vert_all { get; }
        public float[] vmesh_pos { get; }
        public int n_tri_all { get; }
        public int[] indices_all { get; }

        private MeshFilter[] mfs;
        private int n_mesh;
        private int[] vert_offsets;
        private int[] tri_offsets;
        private int[] n_vert;
        private GameObject visualObj;

        public MeshExtractor(GameObject obj)
        {
            visualObj = obj;
            mfs = visualObj.GetComponentsInChildren<MeshFilter>();
            n_mesh = mfs.Length;
            int[] n_tri = new int[n_mesh];
            n_vert = new int[n_mesh];
            n_tri_all = 0;
            n_vert_all = 0;
            vert_offsets = new int[n_mesh];
            tri_offsets = new int[n_mesh];
            for (int i = 0; i < n_mesh; ++i)
            {
                Mesh mesh = mfs[i].sharedMesh;
                // We consider only one submesh context.
                n_tri[i] = mesh.GetIndices(0).GetLength(0) / 3;
                n_vert[i] = mesh.vertices.GetLength(0);
                n_tri_all += n_tri[i];
                n_vert_all += n_vert[i];
                for (int j = 0; j < i; ++j)
                {
                    vert_offsets[i] += n_vert[j];
                    tri_offsets[i] += n_tri[j];
                }
            }
            // Prepare vmesh_indices_cpp
            Vector3[] pos_all = new Vector3[n_vert_all];
            indices_all = new int[3 * n_tri_all];
            for (int i = 0; i < n_mesh; ++i)
            {
                Mesh mesh = mfs[i].sharedMesh;
                for (int j = 0; j < n_vert[i]; ++j)
                {
                    pos_all[vert_offsets[i] + j] = mesh.vertices[j];
                }
                int[] tri = mesh.GetIndices(0);
                for (int j = 0; j < 3 * n_tri[i]; ++j)
                {
                    indices_all[3 * tri_offsets[i] + j] = tri[j] + vert_offsets[i];
                }
            }
            // Prepare vmesh_pos_cpp
            int l = 3 * n_vert_all;
            vmesh_pos = new float[l];
            int vertex_index = 0;
            for (int i = 0; i < n_mesh; ++i)
            {
                for (int j = 0; j < n_vert[i]; ++j)
                {
                    // Convert local coordinate to world coordinate considering child object's transform
                    Vector3 localPos = pos_all[vert_offsets[i] + j];
                    Vector3 worldpt = mfs[i].transform.localToWorldMatrix.MultiplyPoint3x4(localPos);
                    
                    for (int k = 0; k < 3; ++k)
                    {
                        vmesh_pos[3 * vertex_index + k] = worldpt[k];
                    }
                    vertex_index++;
                }
            }
        }

        public void UpdatePosition(float[] new_pos)
        {
            for (int m = 0; m < n_mesh; ++m)
            {
                Mesh mesh = mfs[m].mesh;
                Vector3[] pos = mesh.vertices;
                for (int i = 0; i < n_vert[m]; ++i)
                {
                    int pos_id = vert_offsets[m] + i;
                    Vector3 worldPos = new Vector3(new_pos[3 * pos_id + 0], new_pos[3 * pos_id + 1], new_pos[3 * pos_id + 2]);
                    pos[i] = mfs[m].transform.worldToLocalMatrix.MultiplyPoint3x4(worldPos);
                }

                //Debug.Log(pos[0]);
                mesh.vertices = pos;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
            }
        }

        public void UpdateVertexColor(float[] scalar, float lowerLimit, float upperLimit)
        {
            for (int m = 0; m < n_mesh; ++m)
            {
                Mesh mesh = mfs[m].mesh;
                Vector3[] pos = mesh.vertices;
                Color[] colors = new Color[pos.Length];
                for (int i = 0; i < n_vert[m]; ++i)
                {
                    int pos_id = vert_offsets[m] + i;
                    float t = (scalar[pos_id] - lowerLimit) / (upperLimit - lowerLimit);

                    // blue - green - red
                    if (t < 0.5)
                        colors[i] = Color.Lerp(Color.blue, Color.green, t * 2.0f);
                    else
                        colors[i] = Color.Lerp(Color.green, Color.red, (t - 0.5f) * 2.0f);
                }

                mesh.colors = colors;
            }
        }
    }

    public static class TetrahedralMeshTools
    {
        /// <summary>
        /// 四面体メッシュから表面メッシュを抽出してGameObjectを作成
        /// </summary>
        /// <param name="pos">頂点座標配列 (x0,y0,z0,x1,y1,z1,...)</param>
        /// <param name="tet">四面体インデックス配列 (t0_v0,t0_v1,t0_v2,t0_v3,t1_v0,...)</param>
        /// <param name="nodes">頂点数</param>
        /// <param name="tets">四面体数</param>
        /// <param name="parent">親オブジェクト（nullの場合はルートに配置）</param>
        /// <param name="objectName">作成するGameObjectの名前</param>
        /// <returns>表面メッシュGameObject</returns>
        public static GameObject ExtractSurfaceMesh(float[] pos, int[] tet, int nodes, int tets, Transform parent = null, string objectName = "SurfaceMesh")
        {
            // 表面の三角形を抽出
            var surfaceTriangles = ExtractSurfaceTriangles(tet, tets);
            
            // 使用される頂点のマッピングを作成
            var vertexMapping = CreateVertexMapping(surfaceTriangles);
            
            // Meshを作成
            Mesh surfaceMesh = CreateSurfaceMesh(pos, surfaceTriangles, vertexMapping);
            
            // GameObjectを作成
            GameObject surfaceObj = new GameObject(objectName);
            if (parent != null)
                surfaceObj.transform.SetParent(parent);
                
            var meshFilter = surfaceObj.AddComponent<MeshFilter>();
            var meshRenderer = surfaceObj.AddComponent<MeshRenderer>();
            
            meshFilter.mesh = surfaceMesh;
            
            // マテリアルを設定
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(0.8f, 0.8f, 1.0f, 0.8f);
            material.SetFloat("_Mode", 3); // Transparent mode
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = 3000;
            meshRenderer.material = material;
            
            return surfaceObj;
        }
        
        /// <summary>
        /// 四面体から表面の三角形を抽出
        /// </summary>
        private static System.Collections.Generic.List<int[]> ExtractSurfaceTriangles(int[] tet, int tets)
        {
            var faceCount = new System.Collections.Generic.Dictionary<string, int>();
            var faceToTriangle = new System.Collections.Generic.Dictionary<string, int[]>();
            
            // 各四面体の4つの面をチェック
            for (int t = 0; t < tets; t++)
            {
                int v0 = tet[4 * t + 0];
                int v1 = tet[4 * t + 1];  
                int v2 = tet[4 * t + 2];
                int v3 = tet[4 * t + 3];
                
                // 4つの三角形面（外向きの法線になるように）
                var faces = new int[4][]
                {
                    new int[] { v0, v2, v1 }, // 面012を反時計回りに
                    new int[] { v0, v1, v3 }, // 面013を反時計回りに
                    new int[] { v0, v3, v2 }, // 面032を反時計回りに
                    new int[] { v1, v2, v3 }  // 面123を反時計回りに
                };
                
                foreach (var face in faces)
                {
                    // 頂点インデックスをソートしてキーを作成
                    var sortedFace = new int[] { face[0], face[1], face[2] };
                    System.Array.Sort(sortedFace);
                    string key = $"{sortedFace[0]},{sortedFace[1]},{sortedFace[2]}";
                    
                    if (faceCount.ContainsKey(key))
                    {
                        faceCount[key]++;
                    }
                    else
                    {
                        faceCount[key] = 1;
                        faceToTriangle[key] = face; // 元の順序を保持
                    }
                }
            }
            
            // 1回だけ現れる面（表面）を抽出
            var surfaceTriangles = new System.Collections.Generic.List<int[]>();
            foreach (var kvp in faceCount)
            {
                if (kvp.Value == 1)
                {
                    surfaceTriangles.Add(faceToTriangle[kvp.Key]);
                }
            }
            
            return surfaceTriangles;
        }
        
        /// <summary>
        /// 使用される頂点のマッピングを作成
        /// </summary>
        private static System.Collections.Generic.Dictionary<int, int> CreateVertexMapping(System.Collections.Generic.List<int[]> triangles)
        {
            var usedVertices = new System.Collections.Generic.HashSet<int>();
            foreach (var triangle in triangles)
            {
                usedVertices.Add(triangle[0]);
                usedVertices.Add(triangle[1]);
                usedVertices.Add(triangle[2]);
            }
            
            var mapping = new System.Collections.Generic.Dictionary<int, int>();
            int newIndex = 0;
            foreach (int vertexIndex in usedVertices)
            {
                mapping[vertexIndex] = newIndex++;
            }
            
            return mapping;
        }
        
        /// <summary>
        /// 表面メッシュを作成
        /// </summary>
        private static Mesh CreateSurfaceMesh(float[] pos, System.Collections.Generic.List<int[]> surfaceTriangles, System.Collections.Generic.Dictionary<int, int> vertexMapping)
        {
            // 頂点配列を作成
            var vertices = new Vector3[vertexMapping.Count];
            foreach (var kvp in vertexMapping)
            {
                int originalIndex = kvp.Key;
                int newIndex = kvp.Value;
                vertices[newIndex] = new Vector3(
                    pos[3 * originalIndex + 0],
                    pos[3 * originalIndex + 1],
                    pos[3 * originalIndex + 2]
                );
            }
            
            // 三角形インデックスを作成
            var triangleIndices = new int[surfaceTriangles.Count * 3];
            for (int i = 0; i < surfaceTriangles.Count; i++)
            {
                triangleIndices[i * 3 + 0] = vertexMapping[surfaceTriangles[i][0]];
                triangleIndices[i * 3 + 1] = vertexMapping[surfaceTriangles[i][1]];
                triangleIndices[i * 3 + 2] = vertexMapping[surfaceTriangles[i][2]];
            }
            
            // Meshを作成
            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangleIndices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            return mesh;
        }
    }
}