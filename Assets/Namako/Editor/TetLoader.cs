using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;


namespace Namako
{


    public class TetLoader : EditorWindow
    {
        private TextAsset textAsset;
        private float[] pos;
        private int nodes;
        private int[] tet;
        private int tets;
        private GameObject meshObj;
        private GameObject nodeRootObj;
        private GameObject[] nodeObj;
        private GameObject tetObj;
        private string meshObjName = "TetMesh";
        public const float r = 0.005f;
        private TextAsset jsonAsset;
        private float tetraScale = 0.9f;

        [MenuItem("Window/TetLoader")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(TetLoader));
        }

        void OnGUI()
        {
            textAsset = EditorGUILayout.ObjectField("Mesh Source (TextAsset)", textAsset, typeof(Object), true) as TextAsset;
            if (GUILayout.Button("Load Mesh"))
            {
                meshObj = new GameObject();
                meshObj.name = meshObjName;
                LoadMesh();
                SaveMeshJSON();
                TetContainer tetContainer = meshObj.AddComponent<TetContainer>();
                tetContainer.meshJsonAsset = jsonAsset;
                tetContainer.tetraScale = tetraScale;
                meshObj.AddComponent<NamakoSolver>();
            }
            if (GUILayout.Button("Clean"))
            {
                DestroyImmediate(GameObject.Find(meshObjName));
                AssetDatabase.DeleteAsset(MeshForJSON.savePath);
                AssetDatabase.Refresh();
            }
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
            AssetDatabase.CreateAsset(jsonAsset, MeshForJSON.savePath);
            AssetDatabase.Refresh();
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

            // Create GameObjects
            GenerateNodeObjects();
            GenerateTetraObjects();
        }


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
