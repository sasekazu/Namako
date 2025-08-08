using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Namako
{
    /// <summary>
    /// Simple contact force visualization component
    /// </summary>
    public class ContactForceVisualizer : MonoBehaviour
    {
        [Header("Force Visualization Settings")]
        [Tooltip("Enable visualization")]
        public bool enableVisualization = true;
        
        [Tooltip("Enable individual vertex force visualization")]
        public bool enableVertexForceVisualization = true;
        
        [Tooltip("Force scale (arrow length)")]
        public float forceScale = 1.0f;
        
        [Tooltip("Arrow color")]
        public Color arrowColor = Color.red;
        
        [Tooltip("Line width")]
        public float lineWidth = 0.001f;

        [Header("Total Force Visualization")]
        [Tooltip("Enable total force visualization")]
        public bool enableTotalForceVisualization = true;
        
        [Tooltip("Total force arrow color")]
        public Color totalForceColor = Color.blue;
        
        [Tooltip("Total force line width")]
        public float totalForceLineWidth = 0.005f;
        
        [Tooltip("Total force scale")]
        public float totalForceScale = 1.0f;

        // Internal data
        private List<LineRenderer> lineRenderers = new List<LineRenderer>();
        private LineRenderer totalForceLineRenderer;
        private MeshFilter targetMeshFilter;
        private Vector3[] targetVertices;
        private bool isInitialized = false;
        private NamakoSolver namakoSolver;

        void Start()
        {
            InitializeForceVisualizer();
        }

        void Update()
        {
            if (enableVisualization && isInitialized && namakoSolver != null && namakoSolver.IsFEMStarted)
            {
                UpdateForceVisualization();
            }
            else
            {
                HideAllArrows();
            }
        }

        /// <summary>
        /// Initialize force visualization system
        /// </summary>
        private void InitializeForceVisualizer()
        {
            namakoSolver = FindObjectOfType<NamakoSolver>();
            if (namakoSolver == null)
            {
                Debug.LogError("NamakoSolver not found.");
                return;
            }

            // Get MeshFilter of target GameObject
            targetMeshFilter = GetComponent<MeshFilter>();

            if (targetMeshFilter == null || targetMeshFilter.mesh == null)
            {
                Debug.LogError($"MeshFilter or Mesh not found on {gameObject.name}");
                return;
            }

            targetVertices = targetMeshFilter.mesh.vertices;
            CreateLineRenderers();
            
            isInitialized = true;
            Debug.Log($"ContactForceVisualizerLite initialized for {gameObject.name} with {targetVertices.Length} vertices");
        }

        /// <summary>
        /// Create LineRenderers
        /// </summary>
        private void CreateLineRenderers()
        {
            ClearLineRenderers();

            // Create empty parent object
            GameObject lineParent = new GameObject($"ForceLines_{gameObject.name}");

            // Create LineRenderer for each vertex as child of empty parent
            for (int i = 0; i < targetVertices.Length; i++)
            {
                GameObject lineObj = new GameObject($"ForceLine_{i}");
                lineObj.transform.SetParent(lineParent.transform);
                
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.material = CreateLineMaterial();
                lr.startWidth = lineWidth;
                lr.endWidth = lineWidth * 0.5f;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.enabled = false;
                
                lineRenderers.Add(lr);
            }

            // Create total force LineRenderer
            if (enableTotalForceVisualization)
            {
                GameObject totalForceObj = new GameObject("TotalForce");
                totalForceObj.transform.SetParent(lineParent.transform);
                
                totalForceLineRenderer = totalForceObj.AddComponent<LineRenderer>();
                totalForceLineRenderer.material = CreateTotalForceMaterial();
                totalForceLineRenderer.startWidth = totalForceLineWidth;
                totalForceLineRenderer.endWidth = totalForceLineWidth * 0.5f;
                totalForceLineRenderer.positionCount = 2;
                totalForceLineRenderer.useWorldSpace = true;
                totalForceLineRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Create material for LineRenderer
        /// </summary>
        private Material CreateLineMaterial()
        {
            Material material = new Material(Shader.Find("Sprites/Default"));
            material.color = arrowColor;
            return material;
        }

        /// <summary>
        /// Create material for Total Force LineRenderer
        /// </summary>
        private Material CreateTotalForceMaterial()
        {
            Material material = new Material(Shader.Find("Sprites/Default"));
            material.color = totalForceColor;
            return material;
        }

        /// <summary>
        /// Update force visualization
        /// </summary>
        private void UpdateForceVisualization()
        {
            if (targetVertices == null || lineRenderers.Count != targetVertices.Length)
            {
                return;
            }

            // Get force data from native library
            Vector3[] forces = GetContactForces();
            if (forces == null || forces.Length != targetVertices.Length)
            {
                HideAllArrows();
                return;
            }

            // ワールド座標の頂点位置を直接取得
            Vector3[] worldVertices = new Vector3[targetVertices.Length];
            for (int i = 0; i < targetVertices.Length; i++)
            {
                worldVertices[i] = transform.TransformPoint(targetVertices[i]);
            }

            // Update arrows for each vertex
            if (enableVertexForceVisualization)
            {
                for (int i = 0; i < targetVertices.Length; i++)
                {
                    UpdateLineRenderer(i, worldVertices[i], forces[i]);
                }
            }
            else
            {
                HideVertexArrows();
            }

            // Update total force visualization
            if (enableTotalForceVisualization && totalForceLineRenderer != null)
            {
                UpdateTotalForceVisualization(worldVertices, forces);
            }
        }

        /// <summary>
        /// Update total force visualization
        /// </summary>
        private void UpdateTotalForceVisualization(Vector3[] worldVertices, Vector3[] forces)
        {
            // Calculate total force
            Vector3 totalForce = Vector3.zero;
            Vector3 contactCenterPosition = Vector3.zero;
            int activeContactCount = 0;

            for (int i = 0; i < forces.Length; i++)
            {
                if (forces[i].magnitude > 0.01f) // Only count significant forces
                {
                    totalForce += forces[i];
                    contactCenterPosition += worldVertices[i];
                    activeContactCount++;
                }
            }

            if (activeContactCount == 0 || totalForce.magnitude < 0.01f)
            {
                totalForceLineRenderer.enabled = false;
                return;
            }

            // Calculate average contact position
            contactCenterPosition /= activeContactCount;

            // Update total force LineRenderer
            totalForceLineRenderer.enabled = true;
            
            float scaledLength = totalForce.magnitude * totalForceScale;
            Vector3 endPosition = contactCenterPosition + totalForce.normalized * scaledLength;
            
            totalForceLineRenderer.SetPosition(0, contactCenterPosition);
            totalForceLineRenderer.SetPosition(1, endPosition);
            
            // Update material
            totalForceLineRenderer.material.color = totalForceColor;
            totalForceLineRenderer.startWidth = totalForceLineWidth;
            totalForceLineRenderer.endWidth = totalForceLineWidth * 0.5f;
        }

        /// <summary>
        /// Get contact forces from native library
        /// </summary>
        private Vector3[] GetContactForces()
        {
            if (targetVertices == null)
            {
                return null;
            }

            int numVertices = targetVertices.Length;
            
            IntPtr forcesPtr = Marshal.AllocHGlobal(numVertices * 3 * sizeof(float));
            
            try
            {
                int result = NamakoNative.GetContactForces(gameObject.name, forcesPtr);
                
                if (result <= 0)
                {
                    return null;
                }

                float[] forceData = new float[numVertices * 3];
                Marshal.Copy(forcesPtr, forceData, 0, numVertices * 3);

                Vector3[] forces = new Vector3[numVertices];
                for (int i = 0; i < numVertices; i++)
                {
                    // Convert external force to reaction force by inverting sign
                    forces[i] = new Vector3(
                        -forceData[i * 3 + 0],
                        -forceData[i * 3 + 1],
                        -forceData[i * 3 + 2]
                    );
                }

                return forces;
            }
            finally
            {
                Marshal.FreeHGlobal(forcesPtr);
            }
        }

        /// <summary>
        /// Update LineRenderer
        /// </summary>
        private void UpdateLineRenderer(int index, Vector3 position, Vector3 force)
        {
            if (index >= lineRenderers.Count)
            {
                return;
            }

            LineRenderer lr = lineRenderers[index];
            float forceMagnitude = force.magnitude;
            
            if (forceMagnitude < 0.01f)
            {
                lr.enabled = false;
                return;
            }

            lr.enabled = true;
            
            // Calculate arrow end point
            float scaledLength = forceMagnitude * forceScale;
            Vector3 endPosition = position + force.normalized * scaledLength;
            
            // Set LineRenderer positions
            lr.SetPosition(0, position);
            lr.SetPosition(1, endPosition);
            
            // Update material
            lr.material.color = arrowColor;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth * 0.5f;
        }

        /// <summary>
        /// Hide all arrows
        /// </summary>
        private void HideAllArrows()
        {
            HideVertexArrows();

            if (totalForceLineRenderer != null)
            {
                totalForceLineRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Hide vertex arrows only
        /// </summary>
        private void HideVertexArrows()
        {
            foreach (LineRenderer lr in lineRenderers)
            {
                if (lr != null)
                {
                    lr.enabled = false;
                }
            }
        }

        /// <summary>
        /// Clear LineRenderers
        /// </summary>
        private void ClearLineRenderers()
        {
            foreach (LineRenderer lr in lineRenderers)
            {
                if (lr != null)
                {
                    DestroyImmediate(lr.gameObject);
                }
            }
            lineRenderers.Clear();

            if (totalForceLineRenderer != null)
            {
                DestroyImmediate(totalForceLineRenderer.gameObject);
                totalForceLineRenderer = null;
            }
        }

        private void OnDestroy()
        {
            ClearLineRenderers();
        }
    }
}
