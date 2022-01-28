using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine.SceneManagement;
using System.Runtime.InteropServices;
using System;
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
        public const float r = 0.005f;
        private TextAsset jsonAsset;
        private float tetraScale = 0.9f;
        private bool invertX = true;
        private bool scaleTo20cm = true;
        private string savePath = "";
        private int divisions = 5;

        [DllImport("namako")]
        private static extern void GenerateGridMesh(
            int divisions, IntPtr pos, int n_pos, IntPtr indices, int n_indices,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 6)] out float[] out_pos,
            out int n_out_pos,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 8)] out int[] out_tet,
            out int n_out_tet);
        
        
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
            // Save
            jsonAsset = new TextAsset(JsonUtility.ToJson(obj));
            AssetDatabase.CreateAsset(jsonAsset, savePath);
            AssetDatabase.Refresh();
        }

        void InitMesh()
        {
            meshObj = new GameObject();
            meshObj.name = meshObjName;
        }

        void SetupSolver()
        {
            SaveMeshJSON();

            TetContainer tetContainer = meshObj.AddComponent<TetContainer>();
            tetContainer.meshJsonAsset = jsonAsset;
            tetContainer.tetraScale = tetraScale;
            NamakoSolver solver = meshObj.AddComponent<NamakoSolver>();

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
            GenerateGridMesh(divisions, vmesh_pos, mex.n_vert_all, vmesh_indices, mex.n_tri_all,
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
                string[] tmp = lines[i].Split(' ');
                pos[3 * j + 0] = float.Parse(tmp[1]);
                pos[3 * j + 1] = float.Parse(tmp[2]);
                pos[3 * j + 2] = float.Parse(tmp[3]);
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
                string[] tmp = lines[i].Split(' ');
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


    }

}
