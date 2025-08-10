using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Namako
{


    [ExecuteInEditMode]
    public class NamakoTetraMesh : MonoBehaviour
    {

        public bool drawWireframe = false;
        public float nodeRadius = 0.005f;
        public float tetraScale = 0.9f;
        public TextAsset meshJsonAsset;
        private MeshForJSON meshJson = null;
        private GameObject nodeRootObj;
        private GameObject[] nodeObj;
        private GameObject tetObj;
        private Vector3[] pos;
        private int[] tet;
        private int tets;
        private int nodes;

        public Vector3[] Pos
        {
            get { return this.pos; }
        }

        public int[] Tet
        {
            get { return this.tet; }
        }

        public int Tets
        {
            get { return this.tets; }
        }

        public int Nodes
        {
            get { return this.nodes; }
        }

        public GameObject NodeRootObj
        {
            get { return NodeRootObj; }
        }

        public GameObject[] NodeObj
        {
            get { return nodeObj; }
        }

        private void Awake()
        {
            LoadTextAsset();
        }

        void Start()
        {
            if (meshJson == null)
            {
                LoadTextAsset();
            }
        }

        private void LoadTextAsset()
        {
            if (!meshJsonAsset)
            {
                return;
            }
            meshJson = JsonUtility.FromJson<MeshForJSON>(meshJsonAsset.text);

            nodes = meshJson.pos.Length / 3;
            pos = new Vector3[nodes];
            // pos values are to be synchronized with nodeObj position

            tets = meshJson.tet.Length / 4;
            tet = new int[4 * tets];
            System.Array.Copy(meshJson.tet, tet, 4 * tets);

            nodeRootObj = transform.Find("nodes").gameObject;
            nodes = nodeRootObj.transform.childCount;
            nodeObj = new GameObject[nodes];
            for (int i = 0; i < nodes; ++i)
            {
                nodeObj[i] = nodeRootObj.transform.Find("node " + i).gameObject;
            }
            tetObj = transform.Find("tetras").gameObject;
        }

        void Update()
        {
            if (drawWireframe)
            {
                DrawTetraWireframe();
            }

            // Keep node's scale constant
            if (transform.hasChanged
                && transform.localScale.x != 0.0f
                && transform.localScale.y != 0.0f
                && transform.localScale.z != 0.0f)
            {
                Vector3 constScale = nodeRadius * Vector3.one;
                foreach (GameObject go in nodeObj)
                {
                    go.transform.parent = null;
                    go.transform.localScale = constScale;
                    go.transform.parent = nodeRootObj.transform;
                }
            }

            // Update "pos" and tetra vertices
            UpdatePos();
            UpdateTetra();
        }


        public void MoveVertex(int id, Vector3 v)
        {
            pos[id] = v;
            nodeObj[id].transform.position = v;
        }

        void UpdatePos()
        {
            for (int i = 0; i < nodes; ++i)
            {
                pos[i] = nodeObj[i].transform.position;
            }
        }


        void UpdateTetra()
        {
            Vector3[] vertices = new Vector3[12 * tets];
            CalcTetraVertices(vertices);
            Mesh mesh = tetObj.GetComponent<MeshFilter>().sharedMesh;
            mesh.vertices = vertices;
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

        void DrawTetraWireframe()
        {
            for (int i = 0; i < tets; ++i)
            {
                Vector3 p0 = nodeObj[tet[4 * i + 0]].transform.position;
                Vector3 p1 = nodeObj[tet[4 * i + 1]].transform.position;
                Vector3 p2 = nodeObj[tet[4 * i + 2]].transform.position;
                Vector3 p3 = nodeObj[tet[4 * i + 3]].transform.position;
                Debug.DrawLine(p0, p1);
                Debug.DrawLine(p0, p2);
                Debug.DrawLine(p0, p3);
                Debug.DrawLine(p1, p2);
                Debug.DrawLine(p1, p3);
                Debug.DrawLine(p2, p3);
            }
        }

        public Vector3[] GetNodePosW()
        {
            Vector3[] posw = new Vector3[nodes];
            for (int i = 0; i < nodes; ++i)
            {
                posw[i] = nodeObj[i].transform.position;
            }
            return posw;
        }
    }

}