using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Namako
{
    /// <summary>
    /// Simple contact force visualization component
    /// </summary>
    [RequireComponent(typeof(NamakoRigidBody))]
    public class NamakoForceVisualizer : MonoBehaviour
    {
        [Header("Force Visualization Settings")]
        [Tooltip("Enable visualization")]
        public bool enableVisualization = false;
        
        [Tooltip("Enable individual vertex force visualization")]
        public bool enableVertexForceVisualization = true;
        
        [Tooltip("Arrow color")]
        public Color arrowColor = Color.red;
        
        [Tooltip("Line width")]
        public float lineWidth = 0.001f;

        [Tooltip("Force scale (arrow length)")]
        public float forceScale = 0.2f;
        
        [Header("Total Force Visualization")]
        [Tooltip("Enable total force visualization")]
        public bool enableTotalForceVisualization = true;
                
        [Tooltip("Total force arrow color")]
        public Color totalForceColor = Color.blue;
        
        [Tooltip("Total force line width")]
        public float totalForceLineWidth = 0.001f;

        [Tooltip("Total force scale")]
        public float totalForceScale = 0.2f;
        
        // Internal data
        private List<GameObject> arrowObjects = new List<GameObject>();
        private GameObject totalForceArrow;
        private GameObject arrowParent;
        private Vector3[] targetVertices;
        private bool isInitialized = false;
        private NamakoSolver namakoSolver;
        
        // Cache for detecting enable/disable changes
        private bool lastEnableTotalForceVisualization;

        void Start()
        {
            InitializeForceVisualizer();
        }

        void Update()
        {
            // Check if total force visualization setting has changed
            if (isInitialized && lastEnableTotalForceVisualization != enableTotalForceVisualization)
            {
                if (!enableTotalForceVisualization && totalForceArrow != null)
                {
                    totalForceArrow.SetActive(false);
                }
                lastEnableTotalForceVisualization = enableTotalForceVisualization;
            }
            
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

            // Get NamakoRigidBody component (guaranteed to exist due to RequireComponent)
            NamakoRigidBody rigidBody = GetComponent<NamakoRigidBody>();
            targetVertices = rigidBody.GetWorldVertices();
            
            if (targetVertices == null || targetVertices.Length == 0)
            {
                Debug.LogError($"No vertices found in {gameObject.name} or its children");
                return;
            }

            CreateArrowRenderers();
            
            // Initialize cache
            lastEnableTotalForceVisualization = enableTotalForceVisualization;
            
            isInitialized = true;
            Debug.Log($"NamakoForceVisualizer initialized for {gameObject.name} with {targetVertices.Length} vertices");
        }

        /// <summary>
        /// Create 3D arrow objects
        /// </summary>
        private void CreateArrowRenderers()
        {
            ClearArrowRenderers();

            // Create empty parent object
            arrowParent = new GameObject($"ForceArrows_{gameObject.name}");

            // Create 3D arrow for each vertex as child of empty parent
            for (int i = 0; i < targetVertices.Length; i++)
            {
                GameObject arrowObj = CreateArrowObject($"ForceArrow_{i}");
                arrowObj.transform.SetParent(arrowParent.transform);
                arrowObj.SetActive(false);
                
                arrowObjects.Add(arrowObj);
            }

            // Create total force arrow
            if (enableTotalForceVisualization)
            {
                totalForceArrow = CreateTotalForceArrowObject("TotalForceArrow");
                totalForceArrow.transform.SetParent(arrowParent.transform);
                totalForceArrow.SetActive(false);
            }
        }

        /// <summary>
        /// Create 3D arrow object (cylinder + cone)
        /// </summary>
        private GameObject CreateArrowObject(string name)
        {
            return CreateArrowObject(name, lineWidth, arrowColor);
        }

        /// <summary>
        /// Create 3D total force arrow object (cylinder + cone) with total force line width
        /// </summary>
        private GameObject CreateTotalForceArrowObject(string name)
        {
            return CreateArrowObject(name, totalForceLineWidth, totalForceColor);
        }

        /// <summary>
        /// Create 3D arrow object (cylinder + cone) with specified width and color
        /// </summary>
        private GameObject CreateArrowObject(string name, float width, Color color)
        {
            GameObject arrowRoot = new GameObject(name);
            
            // Create cylinder (shaft)
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = "Shaft";
            cylinder.transform.SetParent(arrowRoot.transform);
            cylinder.transform.localPosition = new Vector3(0, 0.35f, 0);
            cylinder.transform.localScale = new Vector3(width, 0.35f, width);
            
            // Create cone (head) with fixed aspect ratio (length:diameter = 3)
            GameObject cone = CreateConeObject();
            cone.name = "Head";
            cone.transform.SetParent(arrowRoot.transform);
            cone.transform.localPosition = new Vector3(0, 0.85f, 0);
            cone.transform.localScale = new Vector3(width * 3f, width * 3f, width * 3f);
            
            // Set materials
            SetArrowColor(arrowRoot, color);
            
            return arrowRoot;
        }

        /// <summary>
        /// Create cone object by modifying cylinder mesh
        /// </summary>
        private GameObject CreateConeObject()
        {
            GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            
            // Get the mesh and create a cone by modifying vertices
            MeshFilter meshFilter = cone.GetComponent<MeshFilter>();
            Mesh mesh = Instantiate(meshFilter.mesh);
            
            Vector3[] vertices = mesh.vertices;
            
            // Modify top vertices to create cone shape
            for (int i = 0; i < vertices.Length; i++)
            {
                // Top vertices (y > 0.4) should be moved to center
                if (vertices[i].y > 0.4f)
                {
                    vertices[i] = new Vector3(0, vertices[i].y, 0);
                }
            }
            
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshFilter.mesh = mesh;
            
            return cone;
        }

        /// <summary>
        /// Set arrow color
        /// </summary>
        private void SetArrowColor(GameObject arrow, Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.3f);
            
            Renderer[] renderers = arrow.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.material = material;
            }
        }

        /// <summary>
        /// Update force visualization
        /// </summary>
        private void UpdateForceVisualization()
        {
            if (targetVertices == null || arrowObjects.Count != targetVertices.Length)
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

            // Get world vertices from NamakoRigidBody (guaranteed to exist due to RequireComponent)
            NamakoRigidBody rigidBody = GetComponent<NamakoRigidBody>();
            Vector3[] worldVertices = rigidBody.GetWorldVertices();

            if (worldVertices == null || worldVertices.Length != targetVertices.Length)
            {
                HideAllArrows();
                return;
            }

            // Update arrows for each vertex
            if (enableVertexForceVisualization)
            {
                for (int i = 0; i < targetVertices.Length; i++)
                {
                    UpdateArrowObject(i, worldVertices[i], forces[i]);
                }
            }
            else
            {
                HideVertexArrows();
            }

            // Update total force visualization
            if (enableTotalForceVisualization && totalForceArrow != null)
            {
                UpdateTotalForceVisualization(worldVertices, forces);
            }
            else if (totalForceArrow != null)
            {
                totalForceArrow.SetActive(false);
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
                totalForceArrow.SetActive(false);
                return;
            }

            // Calculate average contact position
            contactCenterPosition /= activeContactCount;

            // Update total force arrow
            totalForceArrow.SetActive(true);
            
            float scaledLength = totalForce.magnitude * totalForceScale;
            UpdateArrowTransform(totalForceArrow, contactCenterPosition, totalForce.normalized, scaledLength);
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
        /// Update arrow object
        /// </summary>
        private void UpdateArrowObject(int index, Vector3 position, Vector3 force)
        {
            if (index >= arrowObjects.Count)
            {
                return;
            }

            GameObject arrow = arrowObjects[index];
            float forceMagnitude = force.magnitude;
            
            if (forceMagnitude < 0.01f)
            {
                arrow.SetActive(false);
                return;
            }

            arrow.SetActive(true);
            
            float scaledLength = forceMagnitude * forceScale;
            UpdateArrowTransform(arrow, position, force.normalized, scaledLength);
        }

        /// <summary>
        /// Update arrow transform (position, rotation, scale)
        /// </summary>
        private void UpdateArrowTransform(GameObject arrow, Vector3 position, Vector3 direction, float length)
        {
            arrow.transform.position = position;
            // Rotate so that Y-axis (up) aligns with the force direction
            arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            
            // Determine which line width to use
            float width = (arrow == totalForceArrow) ? totalForceLineWidth : lineWidth;
            
            // Calculate fixed cone height (length:diameter ratio = 3)
            float coneHeight = width * 3f;
            
            // Get arrow components
            Transform shaft = arrow.transform.Find("Shaft");
            Transform head = arrow.transform.Find("Head");
            
            // Update shaft (cylinder) scale and position
            if (shaft != null)
            {
                // Shaft extends from base to just before the cone
                float shaftHeight = length - coneHeight;
                shaft.localPosition = new Vector3(0, shaftHeight * 0.5f, 0);
                shaft.localScale = new Vector3(width, shaftHeight * 0.5f, width);
            }
            
            // Update head (cone) position and scale with fixed aspect ratio
            if (head != null)
            {
                // Position cone at the tip
                head.localPosition = new Vector3(0, length - coneHeight * 0.5f, 0);
                head.localScale = new Vector3(width * 3f, coneHeight, width * 3f);
            }
        }

        /// <summary>
        /// Hide all arrows
        /// </summary>
        private void HideAllArrows()
        {
            HideVertexArrows();

            if (totalForceArrow != null)
            {
                totalForceArrow.SetActive(false);
            }
        }

        /// <summary>
        /// Hide vertex arrows only
        /// </summary>
        private void HideVertexArrows()
        {
            SetArrowsActive(arrowObjects, false);
        }

        /// <summary>
        /// Set active state for a list of arrows
        /// </summary>
        private void SetArrowsActive(List<GameObject> arrows, bool active)
        {
            foreach (GameObject arrow in arrows)
            {
                if (arrow != null)
                {
                    arrow.SetActive(active);
                }
            }
        }

        /// <summary>
        /// Clear arrow objects
        /// </summary>
        private void ClearArrowRenderers()
        {
            ClearArrowList(arrowObjects);
            
            if (totalForceArrow != null)
            {
                DestroyImmediate(totalForceArrow);
                totalForceArrow = null;
            }
            
            if (arrowParent != null)
            {
                DestroyImmediate(arrowParent);
                arrowParent = null;
            }
        }
        
        /// <summary>
        /// Helper method to clear a list of arrow GameObjects
        /// </summary>
        private void ClearArrowList(List<GameObject> arrows)
        {
            foreach (GameObject arrow in arrows)
            {
                if (arrow != null)
                {
                    DestroyImmediate(arrow);
                }
            }
            arrows.Clear();
        }

        private void OnDestroy()
        {
            ClearArrowRenderers();
        }
    }
}
