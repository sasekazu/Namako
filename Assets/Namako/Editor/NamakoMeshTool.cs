using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Namako
{

    public class NamakoMeshTool : EditorWindow
    {
        private TextAsset textAsset;
        private float[] pos;
        private int nodes;
        private int[] tet;
        private int tets;
        private GameObject visObj;
        private GameObject meshObj;
        private GameObject nodeRootObj;
        private GameObject[] nodeObj;
        private GameObject tetObj;
        private GameObject proxyObj;
        private GameObject inputObj;
        private string meshObjName = "TetMesh";
        private string proxyObjName = "Proxy";
        private string inputObjName = "Input";
        private string surfaceMeshObjName = "SurfaceMesh";
        public const float r = 0.005f;
        private TextAsset jsonAsset;
        private float tetraScale = 0.9f;
        private bool invertX = true;
        private bool scaleTo20cm = true;
        private bool generateSurfaceMesh = false;
        private string savePath = "";
        private int divisions = 5;
        private bool showBoundaryConditionTools = false;

        [MenuItem("Window/NamakoMeshTool")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(NamakoMeshTool));
        }

        void OnGUI()
        {
            savePath = SceneManager.GetActiveScene().path.Replace(".unity", "-generatedmesh.json");

            EditorGUILayout.LabelField("Mesh Generator");
            EditorGUI.indentLevel++;
            visObj = EditorGUILayout.ObjectField("Visual Mesh Object", visObj, typeof(UnityEngine.Object), true) as GameObject;
            divisions = EditorGUILayout.IntField("Divisions", divisions);
            if (GUILayout.Button("Generate Mesh"))
            {
                if(visObj)
                {
                    Clean();
                    InitMesh();
                    GenerateMesh();
                    SetupSolver();
                }
                else
                {
                    Debug.LogError("Visual Mesh Object not found");
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Mesh Loader");
            EditorGUI.indentLevel++;

            textAsset = EditorGUILayout.ObjectField("Mesh Source (TextAsset)", textAsset, typeof(UnityEngine.Object), true) as TextAsset;
            invertX = EditorGUILayout.ToggleLeft("Invert X", invertX);
            scaleTo20cm = EditorGUILayout.ToggleLeft("Scale to 10-cm box", scaleTo20cm);
            generateSurfaceMesh = EditorGUILayout.ToggleLeft("Generate Surface Mesh", generateSurfaceMesh);
            if (GUILayout.Button("Load Mesh"))
            {
                if(textAsset)
                {
                    Clean();
                    InitMesh();
                    LoadMesh();
                    SetupSolver();
                }
                else
                {
                    Debug.LogError("Mesh Source not found");
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Boundary Condition Tools");
            EditorGUI.indentLevel++;
            showBoundaryConditionTools = EditorGUILayout.Foldout(showBoundaryConditionTools, "Boundary Condition Settings");
            if (showBoundaryConditionTools)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("Fix Bottom Nodes (Bottom 10%)"))
                {
                    SetBoundaryConditionsByPosition("bottom");
                }
                if (GUILayout.Button("Fix Top Nodes (Top 10%)"))
                {
                    SetBoundaryConditionsByPosition("top");
                }
                if (GUILayout.Button("Fix Left Nodes (Left 10%)"))
                {
                    SetBoundaryConditionsByPosition("left");
                }
                if (GUILayout.Button("Fix Right Nodes (Right 10%)"))
                {
                    SetBoundaryConditionsByPosition("right");
                }
                if (GUILayout.Button("Clear All Boundary Conditions"))
                {
                    SetBoundaryConditionsByPosition("clear");
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Etc");
            EditorGUI.indentLevel++;
            if (GUILayout.Button("Clean"))
            {
                Clean();
            }
            EditorGUI.indentLevel--;
        }

        void Clean()
        {
            DestroyImmediate(GameObject.Find(meshObjName));
            DestroyImmediate(GameObject.Find(inputObjName));
            DestroyImmediate(GameObject.Find(proxyObjName));
            DestroyImmediate(GameObject.Find(surfaceMeshObjName));
            AssetDatabase.DeleteAsset(savePath);
            AssetDatabase.Refresh();
        }

        void SaveMeshJSON()
        {
            // to JSON
            var obj = new MeshForJSON();
            obj.pos = new float[pos.Length];
            System.Array.Copy(pos, obj.pos, pos.Length);
            obj.tet = new int[tet.Length];
            System.Array.Copy(tet, obj.tet, tet.Length);

            // JSONデータを文字列として取得
            string jsonString = JsonUtility.ToJson(obj);

            // ファイルシステムに直接書き込み
            string fullPath = Application.dataPath.Replace("Assets", "") + savePath;
            System.IO.File.WriteAllText(fullPath, jsonString);

            // Unityにファイルをインポートさせる
            AssetDatabase.ImportAsset(savePath);
            AssetDatabase.Refresh();

            // TextAssetとして読み込み
            jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(savePath);
        }

        void InitMesh()
        {
            meshObj = new GameObject();
            meshObj.name = meshObjName;
        }

        void SetupSolver()
        {
            SaveMeshJSON();

            // TetContainerを追加
            TetContainer tetContainer = meshObj.GetComponent<TetContainer>();
            if (tetContainer == null)
            {
                tetContainer = meshObj.AddComponent<TetContainer>();
            }
            tetContainer.meshJsonAsset = jsonAsset;
            tetContainer.tetraScale = tetraScale;
            
            // NamakoSolverを安全に作成
            NamakoSolver solver = NamakoSolver.CreateFromTool(meshObj);
            if (solver == null)
            {
                Debug.LogError("NamakoSolverの作成に失敗しました。シーンに既に存在する可能性があります。");
                return;
            }

            Debug.Log("NamakoSolverが正常に作成されました。");

            // Generate Input and Proxy objects
            inputObj = new GameObject(inputObjName);
            inputObj.transform.SetPositionAndRotation(new Vector3(0.0f, 0.2f, 0.0f), Quaternion.identity);
            proxyObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proxyObj.name = proxyObjName;
            proxyObj.transform.localScale = Vector3.one * solver.HIPRad * 2.0f;
            proxyObj.transform.SetPositionAndRotation(inputObj.transform.position, Quaternion.identity);
            solver.proxyObj = proxyObj;
            solver.inputObj = inputObj;
            if (visObj)
            {
                solver.visualObj = visObj;
            }
        }

        void GenerateMesh()
        {
            // Input
            MeshExtractor mex = new MeshExtractor(visObj);
            IntPtr vmesh_pos = Marshal.AllocHGlobal(3 * mex.n_vert_all * sizeof(float));
            Marshal.Copy(mex.vmesh_pos, 0, vmesh_pos, 3 * mex.n_vert_all);
            IntPtr vmesh_indices = Marshal.AllocHGlobal(3 * mex.n_tri_all * sizeof(int));
            Marshal.Copy(mex.indices_all, 0, vmesh_indices, 3 * mex.n_tri_all);
            // Output
            int pos_grid_size = 0;
            int tet_size = 0;
            // Generate mesh
            NamakoNative.GenerateGridMesh(divisions, vmesh_pos, mex.n_vert_all, vmesh_indices, mex.n_tri_all,
            out pos, out pos_grid_size, out tet, out tet_size);
            Marshal.FreeHGlobal(vmesh_pos);
            Marshal.FreeHGlobal(vmesh_indices);

            nodes = pos_grid_size / 3;
            tets = tet_size / 4;
            // Generate objects
            GenerateNodeObjects();
            GenerateTetraObjects();
        }

        // Read text file and generate pos and tet
        void LoadMesh()
        {
            string[] lines = textAsset.text.Split('\n');
            int rows = lines.Length;
            int i = 0;
            for (; i < rows; ++i)
            {
                if (lines[i].Contains("$Nodes"))
                    break;
            }
            ++i;
            nodes = System.Int32.Parse(lines[i]);
            pos = new float[3 * nodes];
            ++i;
            // Read nodes
            for (int j = 0; j < nodes; j++)
            {
                string[] tmp = lines[i]
                    .TrimEnd('\r', '\n')
                    .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                pos[3 * j + 0] = float.Parse(tmp[1], CultureInfo.InvariantCulture);
                pos[3 * j + 1] = float.Parse(tmp[2], CultureInfo.InvariantCulture);
                pos[3 * j + 2] = float.Parse(tmp[3], CultureInfo.InvariantCulture);

                ++i;
            }
            // Read tetra
            for (; i < rows; ++i)
            {
                if (lines[i].Contains("$Elements"))
                    break;
            }
            ++i;
            int elms = System.Int32.Parse(lines[i]);
            var tetList = new List<int[]>();
            ++i;
            const int TETRA = 4; // Tetra ID of GMSH
            for (int j = 0; j < elms; ++j)
            {
                string[] tmp = lines[i]
                    .TrimEnd('\r', '\n')
                    .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (System.Int32.Parse(tmp[1]) == TETRA)
                {
                    int tags = System.Int32.Parse(tmp[2]);
                    int[] newTet = new int[4];
                    newTet[0] = System.Int32.Parse(tmp[3 + tags]) - 1;
                    newTet[1] = System.Int32.Parse(tmp[3 + tags + 1]) - 1;
                    newTet[2] = System.Int32.Parse(tmp[3 + tags + 2]) - 1;
                    newTet[3] = System.Int32.Parse(tmp[3 + tags + 3]) - 1;
                    tetList.Add(newTet);
                }
                ++i;
            }
            tets = tetList.Count;
            tet = new int[4 * tets];
            for (int j = 0; j < tets; ++j)
            {
                for (int k = 0; k < 4; ++k)
                {
                    tet[4 * j + k] = tetList[j][k];
                }
            }

            // Invert x coordinages to modify right-hand system to left-hand system (unity coordinate system)
            if (invertX)
            {
                for (int j = 0; j < nodes; j++)
                {
                    pos[3 * j + 0] *= -1;
                }
                for (int j = 0; j < tets; ++j)
                {
                    int tmp = tet[4 * j + 1];
                    tet[4 * j + 1] = tet[4 * j + 2];
                    tet[4 * j + 2] = tmp;
                }
            }

            // Scale the model to fit 20cm-20cm-20cm box
            if (scaleTo20cm)
            {
                float[] posx = new float[nodes];
                float[] posy = new float[nodes];
                float[] posz = new float[nodes];
                for(int j=0; j<nodes; ++j)
                {
                    posx[j] = pos[3 * j + 0];
                    posy[j] = pos[3 * j + 1];
                    posz[j] = pos[3 * j + 2];
                }
                float maxx = posx.Max();
                float maxy = posy.Max();
                float maxz = posz.Max();
                float minx = posx.Min();
                float miny = posy.Min();
                float minz = posz.Min();
                float[] width = { maxx - minx, maxy - miny, maxz - minz };
                Vector3 center = new Vector3((maxx + minx) * 0.5f, (maxy + miny) * 0.5f, (maxz + minz) * 0.5f);
                float maxw = width.Max();
                float scale = 0.2f / maxw;
                Debug.Log(center);
                for (int j = 0; j < nodes; ++j)
                {
                    pos[3 * j + 0] = (pos[3 * j + 0] - center.x) * scale;
                    pos[3 * j + 1] = (pos[3 * j + 1] - center.y + width[1] * 0.5f) * scale;
                    pos[3 * j + 2] = (pos[3 * j + 2] - center.z) * scale;
                }
            }

            // Create GameObjects
            GenerateNodeObjects();
            GenerateTetraObjects();
            
            // 表面メッシュを抽出してGameObjectとして保存（チェックボックスがオンの場合のみ）
            if (generateSurfaceMesh)
            {
                GameObject surfaceMeshObj = TetrahedralMeshTools.ExtractSurfaceMesh(pos, tet, nodes, tets, meshObj.transform.parent, surfaceMeshObjName);
                visObj = surfaceMeshObj; // 作成した表面メッシュをvisObjに格納
            }
        }


        // Generate node objects (nodeObj) 
        // based on "nodes", "pos"
        void GenerateNodeObjects()
        {
            nodeRootObj = new GameObject();
            nodeRootObj.name = "nodes";
            nodeRootObj.transform.parent = meshObj.transform;
            nodeObj = new GameObject[nodes];
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Renderer goRend = go.GetComponent<Renderer>();
            var goMat = new Material(goRend.sharedMaterial);
            goMat.color = Color.cyan;
            goRend.sharedMaterial = goMat;
            goRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            goRend.receiveShadows = false;
            go.transform.localScale = new Vector3(r, r, r);
            
            for (int j = 0; j < nodes; ++j)
            {
                nodeObj[j] = GameObject.Instantiate(go) as GameObject;
                nodeObj[j].name = "node " + j;
                nodeObj[j].transform.parent = nodeRootObj.transform;
                nodeObj[j].transform.localPosition = new Vector3(pos[3 * j + 0], pos[3 * j + 1], pos[3 * j + 2]);
                
                // 境界条件コンポーネントを追加
                Type boundaryConditionType = Type.GetType("Namako.NodeBoundaryCondition, Assembly-CSharp");
                if (boundaryConditionType != null)
                {
                    var boundaryCondition = nodeObj[j].AddComponent(boundaryConditionType);
                    if (boundaryCondition != null)
                    {
                        // リフレクションを使用してプロパティを設定
                        var isFixedField = boundaryConditionType.GetField("isFixed");
                        var displacementField = boundaryConditionType.GetField("displacement");
                        
                        if (isFixedField != null) isFixedField.SetValue(boundaryCondition, false);
                        if (displacementField != null) displacementField.SetValue(boundaryCondition, Vector3.zero);
                    }
                }
            }
            DestroyImmediate(go);
        }

        // Generate tetra objects 
        // based on "tets" and "tet"
        void GenerateTetraObjects()
        {
            tetObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(tetObj.GetComponent<BoxCollider>());
            tetObj.name = "tetras";
            var mat = new Material(tetObj.GetComponent<Renderer>().sharedMaterial);
            mat.color = new Color32(230, 151, 62, 10);
            mat.shader = Shader.Find("Diffuse");
            tetObj.GetComponent<Renderer>().sharedMaterial = mat;
            tetObj.transform.parent = meshObj.transform;
            Mesh mesh = new Mesh();
            tetObj.GetComponent<MeshFilter>().mesh = mesh;
            Vector3[] vertices = new Vector3[12 * tets];
            CalcTetraVertices(vertices);
            mesh.vertices = vertices;
            int[] triangles = new int[12 * tets];
            for (int j = 0; j < 12 * tets; ++j)
            {
                triangles[j] = j;
            }
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
        }

        void CalcTetraVertices(Vector3[] vertices)
        {
            for (int j = 0; j < tets; ++j)
            {
                Vector3 v0 = nodeObj[tet[4 * j + 0]].transform.localPosition;
                Vector3 v1 = nodeObj[tet[4 * j + 1]].transform.localPosition;
                Vector3 v2 = nodeObj[tet[4 * j + 2]].transform.localPosition;
                Vector3 v3 = nodeObj[tet[4 * j + 3]].transform.localPosition;
                Vector3 c = (v0 + v1 + v2 + v3) / 4.0f;
                float ts = tetraScale;
                Vector3 w0 = c + ts * (v0 - c);
                Vector3 w1 = c + ts * (v1 - c);
                Vector3 w2 = c + ts * (v2 - c);
                Vector3 w3 = c + ts * (v3 - c);
                vertices[12 * j] = w0;
                vertices[12 * j + 1] = w2;
                vertices[12 * j + 2] = w1;
                vertices[12 * j + 3] = w0;
                vertices[12 * j + 4] = w3;
                vertices[12 * j + 5] = w2;
                vertices[12 * j + 6] = w0;
                vertices[12 * j + 7] = w1;
                vertices[12 * j + 8] = w3;
                vertices[12 * j + 9] = w1;
                vertices[12 * j + 10] = w2;
                vertices[12 * j + 11] = w3;
            }
        }

        void SetBoundaryConditionsByPosition(string mode)
        {
            if (nodeObj == null || nodeObj.Length == 0)
            {
                Debug.LogWarning("ノードが生成されていません。先にメッシュを読み込んでください。");
                return;
            }

            Type boundaryConditionType = Type.GetType("Namako.NodeBoundaryCondition, Assembly-CSharp");
            if (boundaryConditionType == null)
            {
                Debug.LogError("NodeBoundaryConditionクラスが見つかりません。");
                return;
            }

            // ノードの座標範囲を計算
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < nodeObj.Length; i++)
            {
                if (nodeObj[i] == null) continue;
                Vector3 pos = nodeObj[i].transform.localPosition;
                
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
                if (pos.z < minZ) minZ = pos.z;
                if (pos.z > maxZ) maxZ = pos.z;
            }

            // 10%の範囲を計算
            float rangeX = maxX - minX;
            float rangeY = maxY - minY;
            float rangeZ = maxZ - minZ;
            float threshold = 0.1f; // 10%

            Debug.Log($"座標範囲: X({minX:F3}~{maxX:F3}), Y({minY:F3}~{maxY:F3}), Z({minZ:F3}~{maxZ:F3})");

            int count = 0;
            for (int i = 0; i < nodeObj.Length; i++)
            {
                if (nodeObj[i] == null) continue;

                var boundaryCondition = nodeObj[i].GetComponent(boundaryConditionType);
                if (boundaryCondition == null) continue;

                Vector3 position = nodeObj[i].transform.localPosition;
                bool shouldFix = false;

                switch (mode)
                {
                    case "bottom":
                        // 下から10%の範囲
                        shouldFix = position.y <= (minY + rangeY * threshold);
                        break;
                    case "top":
                        // 上から10%の範囲
                        shouldFix = position.y >= (maxY - rangeY * threshold);
                        break;
                    case "left":
                        // 左から10%の範囲
                        shouldFix = position.x <= (minX + rangeX * threshold);
                        break;
                    case "right":
                        // 右から10%の範囲
                        shouldFix = position.x >= (maxX - rangeX * threshold);
                        break;
                    case "clear":
                        shouldFix = false;
                        break;
                }

                // リフレクションを使用してプロパティを設定
                var isFixedField = boundaryConditionType.GetField("isFixed");
                var displacementField = boundaryConditionType.GetField("displacement");

                if (isFixedField != null) 
                {
                    isFixedField.SetValue(boundaryCondition, shouldFix);
                    if (shouldFix) count++;
                }
                if (displacementField != null) 
                {
                    displacementField.SetValue(boundaryCondition, Vector3.zero);
                }

                // エディター上での可視化を即座に更新
                var updateVisualizationMethod = boundaryConditionType.GetMethod("UpdateVisualization");
                if (updateVisualizationMethod != null)
                {
                    updateVisualizationMethod.Invoke(boundaryCondition, null);
                }

                // インスペクターの更新を強制
                EditorUtility.SetDirty(nodeObj[i]);
            }

            Debug.Log($"境界条件を設定しました。モード: {mode}, 固定ノード数: {count}");
            
            // シーンビューの再描画を強制
            SceneView.RepaintAll();
            
            // ヒエラルキーの更新を強制
            EditorApplication.RepaintHierarchyWindow();
        }


    }

}
