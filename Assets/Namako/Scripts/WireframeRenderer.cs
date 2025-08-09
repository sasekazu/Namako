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
            // Reset initialization flag
            isInitialized = false;
            
            if (nodes == null)
            {
                return;
            }
            
            if (tetIndices == null)
            {
                return;
            }
            
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
            
            // Create wireframe material if not provided
            if (wireframeMaterial == null)
            {
                wireframeMaterial = new Material(Shader.Find("Unlit/Color"));
                wireframeMaterial.color = new Color(0.2f, 0.8f, 1.0f, 1.0f);
            }
            meshRenderer.sharedMaterial = wireframeMaterial;
            
            // Initialize position tracking (use world positions like NamakoSolver)
            if (nodeObjects != null)
            {
                lastNodePositions = new Vector3[nodeObjects.Length];
                for (int i = 0; i < nodeObjects.Length; i++)
                {
                    if (nodeObjects[i] != null)
                    {
                        lastNodePositions[i] = nodeObjects[i].transform.position;
                    }
                }
            }
            
            // Generate initial wireframe mesh
            UpdateWireframeMesh();
            
            // Mark as initialized
            isInitialized = true;
        }
        
        void LateUpdate()
        {
            // Don't update if not properly initialized
            if (!isInitialized)
            {
                return;
            }
            
            // Safety checks to prevent null reference exceptions
            if (nodeObjects == null)
            {
                return;
            }
            
            if (tetraData == null)
            {
                return;
            }
            
            // Update wireframe mesh only when nodes have moved (like TetContainer)
            if (HasNodesMoved())
            {
                UpdateWireframeMesh();
                UpdateLastNodePositions();
            }
        }
        
        bool HasNodesMoved()
        {
            if (nodeObjects == null)
            {
                return false;
            }
            
            if (lastNodePositions == null || lastNodePositions.Length != nodeObjects.Length)
            {
                return true;
            }
                
            for (int i = 0; i < nodeObjects.Length; i++)
            {
                if (nodeObjects[i] == null)
                {
                    continue;
                }
                
                if (Vector3.Distance(nodeObjects[i].transform.position, lastNodePositions[i]) > 0.001f)
                {
                    return true;
                }
            }
            return false;
        }
        
        void UpdateLastNodePositions()
        {
            if (nodeObjects == null)
            {
                return;
            }
            
            if (lastNodePositions == null)
            {
                lastNodePositions = new Vector3[nodeObjects.Length];
            }
            
            for (int i = 0; i < nodeObjects.Length; i++)
            {
                if (nodeObjects[i] == null)
                {
                    continue;
                }
                lastNodePositions[i] = nodeObjects[i].transform.position;
            }
        }
        
        void UpdateWireframeMesh()
        {
            if (nodeObjects == null || tetraData == null) 
            {
                return;
            }
            
            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();
            
            for (int i = 0; i < tetraData.Length; i++)
            {
                // Safety check for tetraData indices
                if (tetraData[i] == null || tetraData[i].nodeIndices == null)
                {
                    continue;
                }
                
                // Check if node indices are valid
                bool validIndices = true;
                for (int j = 0; j < 4; j++)
                {
                    int nodeIndex = tetraData[i].nodeIndices[j];
                    if (nodeIndex < 0 || nodeIndex >= nodeObjects.Length || nodeObjects[nodeIndex] == null)
                    {
                        validIndices = false;
                        break;
                    }
                }
                
                if (!validIndices) continue; // Skip this tetrahedron
                
                // Use the exact same coordinate system as TetContainer.CalcTetraVertices
                Vector3 v0, v1, v2, v3;
                v0 = nodeObjects[tetraData[i].nodeIndices[0]].transform.localPosition;
                v1 = nodeObjects[tetraData[i].nodeIndices[1]].transform.localPosition;
                v2 = nodeObjects[tetraData[i].nodeIndices[2]].transform.localPosition;
                v3 = nodeObjects[tetraData[i].nodeIndices[3]].transform.localPosition;
                
                int baseIndex = vertices.Count;
                
                // Add vertices
                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);
                vertices.Add(v3);
                
                // Add edges (lines between vertices)
                // Edge 0-1
                indices.Add(baseIndex + 0);
                indices.Add(baseIndex + 1);
                // Edge 0-2
                indices.Add(baseIndex + 0);
                indices.Add(baseIndex + 2);
                // Edge 0-3
                indices.Add(baseIndex + 0);
                indices.Add(baseIndex + 3);
                // Edge 1-2
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 2);
                // Edge 1-3
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 3);
                // Edge 2-3
                indices.Add(baseIndex + 2);
                indices.Add(baseIndex + 3);
            }
            
            if (wireframeMesh == null)
            {
                wireframeMesh = new Mesh();
                wireframeMesh.name = "Wireframe Mesh";
                if (meshFilter != null)
                {
                    meshFilter.mesh = wireframeMesh;
                }
                else
                {
                    return;
                }
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
            if (!isInitialized)
            {
                return;
            }
            
            UpdateWireframeMesh();
            if (nodeObjects != null)
            {
                UpdateLastNodePositions();
            }
        }
    }
}
