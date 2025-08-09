using UnityEngine;
using System.Collections.Generic;

namespace Namako
{
    [System.Serializable]
    public class TetraData
    {
        public int[] nodeIndices = new int[4];
    }

    public class WireframeRenderer : MonoBehaviour
    {
        [SerializeField] private GameObject[] nodeObjects;
        [SerializeField] private TetraData[] tetraData;
        [SerializeField] private Material wireframeMaterial;
        
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh wireframeMesh;
        private Vector3[] lastNodePositions;
        private bool isInitialized = false;
        
        public void Initialize(GameObject[] nodes, int[] tetIndices, int tetCount)
        {
            nodeObjects = nodes;
            
            // Convert tet array to TetraData array
            tetraData = new TetraData[tetCount];
            for (int i = 0; i < tetCount; i++)
            {
                tetraData[i] = new TetraData();
                tetraData[i].nodeIndices[0] = tetIndices[4 * i + 0];
                tetraData[i].nodeIndices[1] = tetIndices[4 * i + 1];
                tetraData[i].nodeIndices[2] = tetIndices[4 * i + 2];
                tetraData[i].nodeIndices[3] = tetIndices[4 * i + 3];
            }
            
            // Setup components
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
                
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            
            // Create wireframe material
            wireframeMaterial = new Material(Shader.Find("Unlit/Color"));
            wireframeMaterial.color = new Color(0.2f, 0.8f, 1.0f, 1.0f);
            meshRenderer.sharedMaterial = wireframeMaterial;
            
            // Initialize position tracking
            lastNodePositions = new Vector3[nodeObjects.Length];
            UpdateLastNodePositions();
            
            // Generate initial wireframe mesh
            UpdateWireframeMesh();
            isInitialized = true;
        }
        
        void LateUpdate()
        {
            if (!isInitialized) return;
            
            if (HasNodesMoved())
            {
                UpdateWireframeMesh();
                UpdateLastNodePositions();
            }
        }
        
        bool HasNodesMoved()
        {
            if (lastNodePositions.Length != nodeObjects.Length) return true;
                
            for (int i = 0; i < nodeObjects.Length; i++)
            {
                if (Vector3.Distance(nodeObjects[i].transform.position, lastNodePositions[i]) > 0.001f)
                {
                    return true;
                }
            }
            return false;
        }
        
        void UpdateLastNodePositions()
        {
            for (int i = 0; i < nodeObjects.Length; i++)
            {
                lastNodePositions[i] = nodeObjects[i].transform.position;
            }
        }
        
        void UpdateWireframeMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();
            
            for (int i = 0; i < tetraData.Length; i++)
            {
                Vector3 v0 = nodeObjects[tetraData[i].nodeIndices[0]].transform.localPosition;
                Vector3 v1 = nodeObjects[tetraData[i].nodeIndices[1]].transform.localPosition;
                Vector3 v2 = nodeObjects[tetraData[i].nodeIndices[2]].transform.localPosition;
                Vector3 v3 = nodeObjects[tetraData[i].nodeIndices[3]].transform.localPosition;
                
                int baseIndex = vertices.Count;
                
                // Add vertices
                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);
                vertices.Add(v3);
                
                // Add edges (lines between vertices)
                indices.Add(baseIndex + 0); indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 0); indices.Add(baseIndex + 2);
                indices.Add(baseIndex + 0); indices.Add(baseIndex + 3);
                indices.Add(baseIndex + 1); indices.Add(baseIndex + 2);
                indices.Add(baseIndex + 1); indices.Add(baseIndex + 3);
                indices.Add(baseIndex + 2); indices.Add(baseIndex + 3);
            }
            
            if (wireframeMesh == null)
            {
                wireframeMesh = new Mesh();
                wireframeMesh.name = "Wireframe Mesh";
                meshFilter.mesh = wireframeMesh;
            }
            
            wireframeMesh.Clear();
            wireframeMesh.vertices = vertices.ToArray();
            wireframeMesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            wireframeMesh.RecalculateBounds();
        }
        
        public void SetVisible(bool visible)
        {
            if (meshRenderer != null)
                meshRenderer.enabled = visible;
        }
        
        public void ForceUpdateWireframe()
        {
            if (!isInitialized) return;
            
            UpdateWireframeMesh();
            UpdateLastNodePositions();
        }
    }
}
