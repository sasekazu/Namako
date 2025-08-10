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
        private string meshObjName = "TetraMesh";
        private string surfaceMeshObjName = "SurfaceMesh";
        public const float r = 0.005f;
        private TextAsset jsonAsset;
        private float tetraScale = 0.9f;
        private bool zupToyup = true;
        private bool scaleTo20cm = true;
        private bool generateSurfaceMesh = false;
        private string savePath = "";
        private int divisions = 5;
        private bool showBoundaryConditionTools = false;
        private bool showNodes = true;
        private bool showTetras = false;
        private bool showWireframe = true;
        private bool showVisualModel = true;
        private Vector2 scrollPosition = Vector2.zero;
        private bool showMeshInfo = true;
        private Component wireframeRenderer;

        public static void ShowWindow()
        {
            NamakoMeshTool window = EditorWindow.GetWindow(typeof(NamakoMeshTool)) as NamakoMeshTool;
            window.minSize = new Vector2(350, 600);
            window.titleContent = new GUIContent("Namako Mesh Tool");
        }

        void OnGUI()
        {
            savePath = SceneManager.GetActiveScene().path.Replace(".unity", "-generatedmesh.json");

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Namako Mesh Tool", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Tetrahedral Mesh Generation & Management", EditorStyles.miniLabel);
            
            DrawSeparator();

            // Mesh Generator Section
            DrawSectionHeader("Mesh Generator", "Generate tetrahedral mesh from visual object");
            EditorGUI.indentLevel++;
            visObj = EditorGUILayout.ObjectField("Visual Mesh Object", visObj, typeof(UnityEngine.Object), true) as GameObject;
            divisions = EditorGUILayout.IntField("Divisions", divisions);
            
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Generate Mesh", GUILayout.Height(30)))
            {
                if(visObj)
                {
                    CleanMesh();
                    InitMesh();
                    GenerateMesh();
                    SetupSolver();
                }
                else
                {
                    Debug.LogError("Visual Mesh Object not found");
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.indentLevel--;
            
            DrawSeparator();

            // Mesh Loader Section
            DrawSectionHeader("Mesh Loader", "Load tetrahedral mesh from file");
            EditorGUI.indentLevel++;

            textAsset = EditorGUILayout.ObjectField("Mesh Source (TextAsset)", textAsset, typeof(UnityEngine.Object), true) as TextAsset;
            
            EditorGUILayout.BeginVertical();
            zupToyup = EditorGUILayout.ToggleLeft("Z-Up to Y-Up", zupToyup);
            scaleTo20cm = EditorGUILayout.ToggleLeft("Scale to 10-cm box", scaleTo20cm);
            generateSurfaceMesh = EditorGUILayout.ToggleLeft("Generate Surface Mesh", generateSurfaceMesh);
            EditorGUILayout.EndVertical();
            
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Load Mesh", GUILayout.Height(30)))
            {
                if(textAsset)
                {
                    CleanMesh();
                    InitMesh();
                    LoadMesh();
                    SetupSolver();
                }
                else
                {
                    Debug.LogError("Mesh Source not found");
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.indentLevel--;

            DrawSeparator();

            DrawSeparator();

            // Boundary Condition Tools Section
            DrawSectionHeader("Boundary Conditions", "Set fixed nodes by position");
            EditorGUI.indentLevel++;
            showBoundaryConditionTools = EditorGUILayout.Foldout(showBoundaryConditionTools, "Boundary Condition Settings", true);
            if (showBoundaryConditionTools)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(1.0f, 0.8f, 0.8f);
                if (GUILayout.Button("Bottom 10%", GUILayout.Height(25)))
                {
                    SetBoundaryConditionsByPosition("bottom");
                }
                if (GUILayout.Button("Top 10%", GUILayout.Height(25)))
                {
                    SetBoundaryConditionsByPosition("top");
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Left 10%", GUILayout.Height(25)))
                {
                    SetBoundaryConditionsByPosition("left");
                }
                if (GUILayout.Button("Right 10%", GUILayout.Height(25)))
                {
                    SetBoundaryConditionsByPosition("right");
                }
                EditorGUILayout.EndHorizontal();
                
                GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
                if (GUILayout.Button("Clear All Conditions", GUILayout.Height(25)))
                {
                    SetBoundaryConditionsByPosition("clear");
                }
                GUI.backgroundColor = Color.white;
                
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;

            DrawSeparator();

            // Mesh Information Panel
            DrawSectionHeader("Mesh Information", "Current mesh status and statistics");
            EditorGUI.indentLevel++;
            showMeshInfo = EditorGUILayout.Foldout(showMeshInfo, "Mesh Statistics", true);
            if (showMeshInfo)
            {
                EditorGUI.indentLevel++;
                DrawMeshInfoPanel();
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;

            DrawSeparator();

            // Visibility Controls Section
            DrawSectionHeader("Visibility Controls", "Toggle mesh component visibility");
            EditorGUI.indentLevel++;
            
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = showNodes ? new Color(0.8f, 1.0f, 0.8f) : new Color(1.0f, 0.8f, 0.8f);
            if (GUILayout.Button(showNodes ? "Hide Nodes" : "Show Nodes", GUILayout.Height(25)))
            {
                ToggleNodeVisibility();
            }
            
            GUI.backgroundColor = showTetras ? new Color(0.8f, 1.0f, 0.8f) : new Color(1.0f, 0.8f, 0.8f);
            if (GUILayout.Button(showTetras ? "Hide Tetras" : "Show Tetras", GUILayout.Height(25)))
            {
                ToggleTetraVisibility();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = showWireframe ? new Color(0.8f, 1.0f, 0.8f) : new Color(1.0f, 0.8f, 0.8f);
            if (GUILayout.Button(showWireframe ? "Hide Wireframe" : "Show Wireframe", GUILayout.Height(25)))
            {
                ToggleWireframeVisibility();
            }
            
            GUI.backgroundColor = showVisualModel ? new Color(0.8f, 1.0f, 0.8f) : new Color(1.0f, 0.8f, 0.8f);
            if (GUILayout.Button(showVisualModel ? "Hide Visual Model" : "Show Visual Model", GUILayout.Height(25)))
            {
                ToggleVisualModelVisibility();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel--;

            DrawSeparator();

            // Utility Section
            DrawSectionHeader("Utilities", "Clean up and maintenance");
            EditorGUI.indentLevel++;
            GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
            if (GUILayout.Button("Clean Mesh", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Confirm Clean", 
                    "Are you sure you want to clean all generated mesh objects and files? This action cannot be undone.", 
                    "Yes", "Cancel"))
                {
                    CleanMesh();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.indentLevel--;
            
            GUILayout.Space(10);
            
            EditorGUILayout.EndScrollView();
        }

        void CleanMesh()
        {
            CleanMeshObjects();
            CleanGeneratedFiles();
            
            // Reset visibility flags and wireframe reference
            showNodes = true;
            showTetras = false;
            showWireframe = true;
            showVisualModel = true;
            wireframeRenderer = null;
        }

        public static void CleanMeshObjects()
        {
            int cleanedCount = 0;

            // NamakoTetraMeshコンポーネントを持つオブジェクトを検索・削除
            System.Type namakoTetraMeshType = System.Type.GetType("Namako.NamakoTetraMesh, Assembly-CSharp");
            if (namakoTetraMeshType != null)
            {
                UnityEngine.Object[] tetraMeshComponents = UnityEngine.Object.FindObjectsOfType(namakoTetraMeshType);
                foreach (UnityEngine.Object tetraMesh in tetraMeshComponents)
                {
                    if (tetraMesh != null && tetraMesh is Component component)
                    {
                        GameObject tetraMeshObj = component.gameObject;
                        Debug.Log($"Removing object with NamakoTetraMesh: {tetraMeshObj.name}");
                        UnityEngine.Object.DestroyImmediate(tetraMeshObj);
                        cleanedCount++;
                    }
                }
            }

            // フォールバック: 特定の名前のオブジェクトも削除
            string[] meshObjectNames = { "TetraMesh", "SurfaceMesh", "tetras_wireframe" };
            foreach (string objName in meshObjectNames)
            {
                GameObject obj = GameObject.Find(objName);
                if (obj != null)
                {
                    Debug.Log($"Removing mesh object by name: {objName}");
                    UnityEngine.Object.DestroyImmediate(obj);
                    cleanedCount++;
                }
            }

            Debug.Log($"Mesh cleanup completed. {cleanedCount} mesh objects removed.");
        }

        public static void CleanGeneratedFiles()
        {
            string scenePath = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(scenePath))
            {
                string savePath = scenePath.Replace(".unity", "-generatedmesh.json");
                if (AssetDatabase.DeleteAsset(savePath))
                {
                    Debug.Log($"Generated mesh file removed: {savePath}");
                }
            }
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

            // NamakoTetraMeshを追加
            NamakoTetraMesh tetContainer = meshObj.GetComponent<NamakoTetraMesh>();
            if (tetContainer == null)
            {
                tetContainer = meshObj.AddComponent<NamakoTetraMesh>();
            }
            tetContainer.meshJsonAsset = jsonAsset;
            tetContainer.tetraScale = tetraScale;
            
            // 既存のNamakoSolverManagerを検索
            GameObject solverObj = GameObject.Find("NamakoSolverManager");
            NamakoSolver solver = null;
            
            if (solverObj != null)
            {
                solver = solverObj.GetComponent<NamakoSolver>();
            }
            
            // NamakoSolverが存在しない場合のみ作成
            if (solver == null)
            {
                if (solverObj == null)
                {
                    solverObj = new GameObject("NamakoSolverManager");
                }
                
                solver = NamakoSolver.CreateFromTool(solverObj);
                if (solver == null)
                {
                    Debug.LogError("NamakoSolverの作成に失敗しました。シーンに既に存在する可能性があります。");
                    return;
                }
                
                Debug.Log("新しいNamakoSolverが作成されました。");
            }
            else
            {
                Debug.Log("既存のNamakoSolverを使用します。");
            }

            // NamakoStressVisualizerを自動的にアタッチ（新規・既存どちらの場合も）
            var stressVisualizer = solverObj.GetComponent<NamakoStressVisualizer>();
            if (stressVisualizer == null)
            {
                Type stressVisualizerType = Type.GetType("Namako.NamakoStressVisualizer, Assembly-CSharp");
                if (stressVisualizerType != null)
                {
                    stressVisualizer = solverObj.AddComponent(stressVisualizerType) as NamakoStressVisualizer;
                    Debug.Log("NamakoStressVisualizerが自動的にアタッチされました。");
                }
                else
                {
                    Debug.LogWarning("NamakoStressVisualizerクラスが見つかりません。");
                }
            }
            else
            {
                Debug.Log("NamakoStressVisualizerは既にアタッチされています。");
            }

            // SolverにTetraMeshオブジェクトを設定
            solver.tetraMeshGameObject = meshObj;

            // visObjの設定（LoadMeshの場合はgenerateSurfaceMeshの状態に依存）
            if (visObj)
            {
                solver.visualObj = visObj;
            }
            else
            {
                solver.visualObj = null;
            }
            
            Debug.Log("NamakoSolverの設定が完了しました。");
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
            
            // Automatically set bottom boundary conditions
            SetBoundaryConditionsByPosition("bottom");
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

            // Coordinates transformation to modify z-up right-hand system 
            // to y-up left-hand system (unity coordinate system)
            if (zupToyup)
            {
                for (int j = 0; j < nodes; j++)
                {
                    // Rot(-90, x) and invert z axis
                    float x = pos[3 * j + 0];
                    float y = pos[3 * j + 1];
                    float z = pos[3 * j + 2];
                    pos[3 * j + 0] = x;
                    pos[3 * j + 1] = z;
                    pos[3 * j + 2] = y;
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
            
            // Automatically set bottom boundary conditions
            SetBoundaryConditionsByPosition("bottom");
            
            // 表面メッシュを抽出してGameObjectとして保存（チェックボックスがオンの場合のみ）
            if (generateSurfaceMesh)
            {
                GameObject surfaceMeshObj = TetrahedralMeshTools.ExtractSurfaceMesh(pos, tet, nodes, tets, meshObj.transform, surfaceMeshObjName);
                visObj = surfaceMeshObj; // 作成した表面メッシュをvisObjに格納
            }
            else
            {
                // 表面メッシュを生成しない場合はvisObjを空にする
                visObj = null;
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
                Type boundaryConditionType = Type.GetType("Namako.NamakoNode, Assembly-CSharp");
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
            
            // Create material for solid rendering
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color32(230, 151, 62, 255);
            mat.SetFloat("_Mode", 0); // Opaque mode
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_Glossiness", 0.3f);
            
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
            
            // Set initial tetra visibility state
            MeshRenderer tetRenderer = tetObj.GetComponent<MeshRenderer>();
            if (tetRenderer != null)
            {
                tetRenderer.enabled = showTetras;
            }
            
            // Create wireframe by default
            if (showWireframe)
            {
                CreateWireframeRenderer();
            }
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
                
                // Tetrahedron faces with correct winding order (counter-clockwise when viewed from outside)
                // Face 1: v0, v2, v1 (triangle facing outward from v3) - reversed winding
                vertices[12 * j + 0] = w0;
                vertices[12 * j + 1] = w2;
                vertices[12 * j + 2] = w1;
                
                // Face 2: v0, v1, v3 (triangle facing outward from v2) - reversed winding
                vertices[12 * j + 3] = w0;
                vertices[12 * j + 4] = w1;
                vertices[12 * j + 5] = w3;
                
                // Face 3: v1, v2, v3 (triangle facing outward from v0) - reversed winding
                vertices[12 * j + 6] = w1;
                vertices[12 * j + 7] = w2;
                vertices[12 * j + 8] = w3;
                
                // Face 4: v2, v0, v3 (triangle facing outward from v1) - reversed winding
                vertices[12 * j + 9] = w2;
                vertices[12 * j + 10] = w0;
                vertices[12 * j + 11] = w3;
            }
        }

        void FindExistingNodeObjects()
        {
            // meshObjを検索
            if (meshObj == null)
            {
                meshObj = GameObject.Find(meshObjName);
            }
            
            if (meshObj == null) return;
            
            // nodeRootObjを検索
            Transform nodeRootTransform = meshObj.transform.Find("nodes");
            if (nodeRootTransform == null) return;
            
            nodeRootObj = nodeRootTransform.gameObject;
            
            // 子オブジェクトからnodeObjを再構築
            int childCount = nodeRootObj.transform.childCount;
            if (childCount == 0) return;
            
            nodeObj = new GameObject[childCount];
            nodes = childCount;
            
            for (int i = 0; i < childCount; i++)
            {
                nodeObj[i] = nodeRootObj.transform.GetChild(i).gameObject;
            }
        }

        void SetBoundaryConditionsByPosition(string mode)
        {
            // UnityEditor再起動時に備えてnodeObjを再検索
            if (nodeObj == null || nodeObj.Length == 0)
            {
                FindExistingNodeObjects();
            }
            
            if (nodeObj == null || nodeObj.Length == 0)
            {
                Debug.LogWarning("ノードが生成されていません。先にメッシュを読み込んでください。");
                return;
            }

            Type boundaryConditionType = Type.GetType("Namako.NamakoNode, Assembly-CSharp");
            if (boundaryConditionType == null)
            {
                Debug.LogError("NamakoNodeクラスが見つかりません。");
                return;
            }

            // ノードの座標範囲を計算
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < nodeObj.Length; i++)
            {
                if (nodeObj[i] == null) continue;
                Vector3 pos = nodeObj[i].transform.position; // ワールド座標を使用
                
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

                Vector3 position = nodeObj[i].transform.position; // ワールド座標を使用
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

        void ToggleNodeVisibility()
        {
            showNodes = !showNodes;
            if (nodeRootObj != null)
            {
                nodeRootObj.SetActive(showNodes);
                SceneView.RepaintAll();
            }
        }

        void ToggleTetraVisibility()
        {
            showTetras = !showTetras;
            if (tetObj != null)
            {
                MeshRenderer tetRenderer = tetObj.GetComponent<MeshRenderer>();
                if (tetRenderer != null)
                {
                    tetRenderer.enabled = showTetras;
                    SceneView.RepaintAll();
                }
            }
        }

        void ToggleWireframeVisibility()
        {
            showWireframe = !showWireframe;
            
            if (showWireframe)
            {
                CreateWireframeRenderer();
            }
            else
            {
                DestroyWireframeRenderer();
            }
            SceneView.RepaintAll();
        }

        void ToggleVisualModelVisibility()
        {
            showVisualModel = !showVisualModel;
            
            // NamakoSolverからvisual objを取得
            GameObject solverObj = GameObject.Find("NamakoSolverManager");
            if (solverObj != null)
            {
                NamakoSolver solver = solverObj.GetComponent<NamakoSolver>();
                if (solver != null && solver.visualObj != null)
                {
                    solver.visualObj.SetActive(showVisualModel);
                    SceneView.RepaintAll();
                    return;
                }
            }
            
            // フォールバック: 直接visObjを使用
            if (visObj != null)
            {
                visObj.SetActive(showVisualModel);
                SceneView.RepaintAll();
            }
        }

        void CreateWireframeRenderer()
        {
            if (tetObj == null || nodeObj == null) return;
            
            // Create wireframe GameObject with WireframeRenderer component
            GameObject wireframeObj = new GameObject("tetras_wireframe");
            wireframeObj.transform.parent = tetObj.transform.parent;
            wireframeObj.transform.localPosition = tetObj.transform.localPosition;
            
            // Use reflection to add NamakoWireframeRenderer component
            System.Type wireframeType = System.Type.GetType("Namako.NamakoWireframeRenderer, Assembly-CSharp");
            if (wireframeType != null)
            {
                wireframeRenderer = wireframeObj.AddComponent(wireframeType) as Component;
                
                // Call Initialize method using reflection
                var initializeMethod = wireframeType.GetMethod("Initialize");
                if (initializeMethod != null)
                {
                    initializeMethod.Invoke(wireframeRenderer, new object[] { nodeObj, tet, tets });
                }
            }
        }

        void DestroyWireframeRenderer()
        {
            // Remove wireframe object
            if (wireframeRenderer != null)
            {
                DestroyImmediate(wireframeRenderer.gameObject);
                wireframeRenderer = null;
            }
            else
            {
                // Fallback: find and destroy by name
                Transform wireframeTransform = tetObj?.transform.parent?.Find("tetras_wireframe");
                if (wireframeTransform != null)
                {
                    DestroyImmediate(wireframeTransform.gameObject);
                }
            }
        }

        // UI Helper Methods
        void DrawSeparator()
        {
            GUILayout.Space(8);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            GUILayout.Space(8);
        }

        void DrawSectionHeader(string title, string description)
        {
            GUILayout.Space(5);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(description))
            {
                EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
            }
            GUILayout.Space(5);
        }

        void DrawMeshInfoPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Basic mesh statistics
            EditorGUILayout.LabelField("Basic Statistics", EditorStyles.boldLabel);
            
            if (meshObj != null && nodes > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Nodes:", GUILayout.Width(80));
                EditorGUILayout.LabelField(nodes.ToString(), EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Tetrahedra:", GUILayout.Width(80));
                EditorGUILayout.LabelField(tets.ToString(), EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Node/Tet Ratio:", GUILayout.Width(80));
                EditorGUILayout.LabelField($"{(float)nodes / tets:F2}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(5);
                
                // Memory usage estimation
                EditorGUILayout.LabelField("Memory Usage", EditorStyles.boldLabel);
                
                float nodeMemoryKB = (nodes * 3 * sizeof(float)) / 1024f; // Position data
                float tetMemoryKB = (tets * 4 * sizeof(int)) / 1024f; // Tetrahedra indices
                float totalMemoryKB = nodeMemoryKB + tetMemoryKB;
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Node Data:", GUILayout.Width(80));
                EditorGUILayout.LabelField($"{nodeMemoryKB:F1} KB", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Tet Data:", GUILayout.Width(80));
                EditorGUILayout.LabelField($"{tetMemoryKB:F1} KB", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Total:", GUILayout.Width(80));
                string memoryUnit = totalMemoryKB > 1024 ? $"{totalMemoryKB/1024:F1} MB" : $"{totalMemoryKB:F1} KB";
                EditorGUILayout.LabelField(memoryUnit, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(5);
                
                // Mesh bounds information
                if (pos != null && pos.Length > 0)
                {
                    EditorGUILayout.LabelField("Mesh Bounds", EditorStyles.boldLabel);
                    
                    Vector3 minBounds, maxBounds;
                    CalculateMeshBounds(out minBounds, out maxBounds);
                    Vector3 size = maxBounds - minBounds;
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Size:", GUILayout.Width(80));
                    EditorGUILayout.LabelField($"({size.x:F3}, {size.y:F3}, {size.z:F3})", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Center:", GUILayout.Width(80));
                    Vector3 center = (minBounds + maxBounds) * 0.5f;
                    EditorGUILayout.LabelField($"({center.x:F3}, {center.y:F3}, {center.z:F3})", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
                
                GUILayout.Space(5);
                
                // Object status
                EditorGUILayout.LabelField("Object Status", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Nodes Visible:", GUILayout.Width(80));
                EditorGUILayout.LabelField(showNodes ? "Yes" : "No", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Tetras Visible:", GUILayout.Width(80));
                EditorGUILayout.LabelField(showTetras ? "Yes" : "No", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Wireframe:", GUILayout.Width(80));
                EditorGUILayout.LabelField(showWireframe ? "Yes" : "No", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Visual Model:", GUILayout.Width(80));
                EditorGUILayout.LabelField(showVisualModel ? "Yes" : "No", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                // Boundary conditions count
                int fixedNodesCount = CountFixedNodes();
                if (fixedNodesCount >= 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Fixed Nodes:", GUILayout.Width(80));
                    EditorGUILayout.LabelField($"{fixedNodesCount} ({(float)fixedNodesCount/nodes*100:F1}%)", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.LabelField("No mesh loaded", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.LabelField("Generate or load a mesh to see statistics", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }

        void CalculateMeshBounds(out Vector3 minBounds, out Vector3 maxBounds)
        {
            minBounds = Vector3.one * float.MaxValue;
            maxBounds = Vector3.one * float.MinValue;
            
            for (int i = 0; i < nodes; i++)
            {
                Vector3 nodePos = new Vector3(pos[3 * i], pos[3 * i + 1], pos[3 * i + 2]);
                
                if (nodePos.x < minBounds.x) minBounds.x = nodePos.x;
                if (nodePos.y < minBounds.y) minBounds.y = nodePos.y;
                if (nodePos.z < minBounds.z) minBounds.z = nodePos.z;
                
                if (nodePos.x > maxBounds.x) maxBounds.x = nodePos.x;
                if (nodePos.y > maxBounds.y) maxBounds.y = nodePos.y;
                if (nodePos.z > maxBounds.z) maxBounds.z = nodePos.z;
            }
        }

        int CountFixedNodes()
        {
            if (nodeObj == null || nodeObj.Length == 0) return -1;
            
            Type boundaryConditionType = Type.GetType("Namako.NamakoNode, Assembly-CSharp");
            if (boundaryConditionType == null) return -1;
            
            int count = 0;
            for (int i = 0; i < nodeObj.Length; i++)
            {
                if (nodeObj[i] == null) continue;
                
                var boundaryCondition = nodeObj[i].GetComponent(boundaryConditionType);
                if (boundaryCondition == null) continue;
                
                var isFixedField = boundaryConditionType.GetField("isFixed");
                if (isFixedField != null)
                {
                    bool isFixed = (bool)isFixedField.GetValue(boundaryCondition);
                    if (isFixed) count++;
                }
            }
            return count;
        }


    }

}
