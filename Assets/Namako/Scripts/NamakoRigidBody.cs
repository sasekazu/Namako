using UnityEngine;
using System;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Namako
{
    /// <summary>
    /// Namakoシステムで接触剛体として使用されるコンポーネント
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class NamakoRigidBody : MonoBehaviour
    {
        private bool isGrabbing = false;
        private MeshFilter meshFilter;
        
        /// <summary>
        /// Rigid body identifier name
        /// </summary>
        public string RigidBodyName => gameObject.name;
        
        /// <summary>
        /// Whether currently grabbing nodes
        /// </summary>
        public bool IsGrabbing => isGrabbing;
        
        void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
        }
        
#if UNITY_EDITOR
        void Reset()
        {
            // Component added in editor
            CheckAndSetCollisionDetectionType();
            AddNamakoForceVisualizerIfNeeded();
        }
        
        void OnValidate()
        {
            // Component validated in editor
            if (!Application.isPlaying)
            {
                CheckAndSetCollisionDetectionType();
            }
        }
        
        private void CheckAndSetCollisionDetectionType()
        {
            NamakoSolver solver = FindObjectOfType<NamakoSolver>();
            if (solver == null)
            {
                Debug.LogWarning("[NamakoRigidBody] NamakoSolver not found in scene");
                return;
            }

            // Use SerializedObject to access the field
            SerializedObject serializedSolver = new SerializedObject(solver);
            SerializedProperty collisionDetectionProperty = serializedSolver.FindProperty("collisionDetectionType");
            
            if (collisionDetectionProperty != null)
            {
                var currentType = (NamakoSolver.CollisionDetectionType)collisionDetectionProperty.enumValueIndex;
                
                Debug.Log($"[NamakoRigidBody] Current collision detection type: {currentType}");
                
                if (currentType != NamakoSolver.CollisionDetectionType.FEMSDFVsRBVTX)
                {
                    if (EditorUtility.DisplayDialog(
                        "Collision Detection Type Setting",
                        $"NamakoRigidBody requires CollisionDetectionType to be set to 'FEMSDF vs RBVTX'.\n\n" +
                        $"Current setting: {currentType}\n" +
                        $"Required setting: FEMSDF vs RBVTX\n\n" +
                        "Would you like to change it automatically?",
                        "Yes, Change It",
                        "No, Keep Current"))
                    {
                        // Change the collision detection type
                        collisionDetectionProperty.enumValueIndex = (int)NamakoSolver.CollisionDetectionType.FEMSDFVsRBVTX;
                        serializedSolver.ApplyModifiedProperties();
                        Debug.Log($"[NamakoRigidBody] Changed CollisionDetectionType to FEMSDFVsRBVTX");
                    }
                    else
                    {
                        Debug.LogWarning($"[NamakoRigidBody] CollisionDetectionType is set to {currentType}. " +
                                       "NamakoRigidBody may not work correctly. Please set it to FEMSDFVsRBVTX manually.");
                    }
                }
                else
                {
                    Debug.Log("[NamakoRigidBody] CollisionDetectionType is already set to FEMSDFVsRBVTX");
                }
            }
            else
            {
                Debug.LogError("[NamakoRigidBody] Could not find collisionDetectionType property in NamakoSolver");
            }
        }
        
        private void AddNamakoForceVisualizerIfNeeded()
        {
            // Check if NamakoForceVisualizer is already attached
            NamakoForceVisualizer existingVisualizer = GetComponent<NamakoForceVisualizer>();
            if (existingVisualizer == null)
            {
                // Add NamakoForceVisualizer component
                NamakoForceVisualizer visualizer = gameObject.AddComponent<NamakoForceVisualizer>();
                Debug.Log($"[NamakoRigidBody] Automatically added NamakoForceVisualizer to {gameObject.name}");
            }
        }
#endif
        
        /// <summary>
        /// Grab contact nodes
        /// </summary>
        public void GrabNodes()
        {
            NamakoNative.GrabWithRigidBody(RigidBodyName);
            isGrabbing = true;
        }
        
        /// <summary>
        /// Release contact nodes
        /// </summary>
        public void ReleaseNodes()
        {
            NamakoNative.ReleaseWithRigidBody(RigidBodyName);
            isGrabbing = false;
        }
        
        /// <summary>
        /// Get all meshes from this object and its children
        /// </summary>
        /// <returns>Array of mesh data with their transforms</returns>
        public (Mesh mesh, Transform transform)[] GetAllMeshes()
        {
            var meshFilters = GetComponentsInChildren<MeshFilter>();
            var meshes = new (Mesh, Transform)[meshFilters.Length];
            
            for (int i = 0; i < meshFilters.Length; i++)
            {
                meshes[i] = (meshFilters[i].mesh, meshFilters[i].transform);
            }
            
            return meshes;
        }
        
        /// <summary>
        /// Get world coordinate vertex array from this object and its children
        /// このメソッドは外部コンポーネントからも使用可能です
        /// </summary>
        /// <returns>World coordinate vertex array</returns>
        public Vector3[] GetWorldVertices()
        {
            var allMeshes = GetAllMeshes();
            if (allMeshes.Length == 0) return null;
            
            // Count total vertices
            int totalVertexCount = 0;
            foreach (var (mesh, transform) in allMeshes)
            {
                if (mesh != null)
                    totalVertexCount += mesh.vertexCount;
            }
            
            if (totalVertexCount == 0) return null;
            
            // Collect all world vertices
            Vector3[] worldVertices = new Vector3[totalVertexCount];
            int currentIndex = 0;
            
            foreach (var (mesh, meshTransform) in allMeshes)
            {
                if (mesh == null) continue;
                
                Vector3[] localVertices = mesh.vertices;
                for (int i = 0; i < localVertices.Length; i++)
                {
                    worldVertices[currentIndex] = meshTransform.TransformPoint(localVertices[i]);
                    currentIndex++;
                }
            }
            
            return worldVertices;
        }
        
        /// <summary>
        /// Get triangle indices from this object and its children
        /// このメソッドは外部コンポーネントからも使用可能です
        /// </summary>
        /// <returns>Triangle index array</returns>
        public int[] GetTriangles()
        {
            var allMeshes = GetAllMeshes();
            if (allMeshes.Length == 0) return null;
            
            // Count total triangles
            int totalTriangleCount = 0;
            foreach (var (mesh, transform) in allMeshes)
            {
                if (mesh != null)
                    totalTriangleCount += mesh.triangles.Length;
            }
            
            if (totalTriangleCount == 0) return null;
            
            // Collect all triangles with vertex offset
            int[] allTriangles = new int[totalTriangleCount];
            int currentTriangleIndex = 0;
            int vertexOffset = 0;
            
            foreach (var (mesh, meshTransform) in allMeshes)
            {
                if (mesh == null) continue;
                
                int[] meshTriangles = mesh.triangles;
                for (int i = 0; i < meshTriangles.Length; i++)
                {
                    allTriangles[currentTriangleIndex] = meshTriangles[i] + vertexOffset;
                    currentTriangleIndex++;
                }
                
                vertexOffset += mesh.vertexCount;
            }
            
            return allTriangles;
        }
        
        /// <summary>
        /// Register this rigid body to native library
        /// </summary>
        public void RegisterToNative()
        {
            var allMeshes = GetAllMeshes();
            if (allMeshes.Length == 0)
            {
                Debug.LogWarning($"[{gameObject.name}] No mesh found in this object or its children");
                return;
            }
            
            Vector3[] worldVertices = GetWorldVertices();
            if (worldVertices == null)
            {
                Debug.LogWarning($"[{gameObject.name}] Failed to get world vertices");
                return;
            }
            
            int[] triangles = GetTriangles();
            if (triangles == null)
            {
                Debug.LogWarning($"[{gameObject.name}] Failed to get triangles");
                return;
            }
            
            // Convert vertices to float array
            float[] vertexData = new float[worldVertices.Length * 3];
            for (int i = 0; i < worldVertices.Length; i++)
            {
                vertexData[i * 3 + 0] = worldVertices[i].x;
                vertexData[i * 3 + 1] = worldVertices[i].y;
                vertexData[i * 3 + 2] = worldVertices[i].z;
            }
            
            // Allocate memory
            IntPtr vertexPtr = Marshal.AllocHGlobal(vertexData.Length * sizeof(float));
            IntPtr facePtr = Marshal.AllocHGlobal(triangles.Length * sizeof(int));
            
            Marshal.Copy(vertexData, 0, vertexPtr, vertexData.Length);
            Marshal.Copy(triangles, 0, facePtr, triangles.Length);
            
            try
            {
                NamakoNative.AddContactRigidBody(
                    RigidBodyName,
                    vertexPtr, worldVertices.Length,
                    facePtr, triangles.Length / 3);
                    
                Debug.Log($"[{gameObject.name}] Registered rigid body with {worldVertices.Length} vertices and {triangles.Length / 3} triangles from {allMeshes.Length} mesh(es)");
            }
            finally
            {
                Marshal.FreeHGlobal(vertexPtr);
                Marshal.FreeHGlobal(facePtr);
            }
        }
        
        /// <summary>
        /// Update position in native library
        /// </summary>
        public void UpdatePositionInNative()
        {
            Vector3[] worldVertices = GetWorldVertices();
            if (worldVertices == null)
                return;
                
            // Convert vertices to float array
            float[] vertexData = new float[worldVertices.Length * 3];
            for (int i = 0; i < worldVertices.Length; i++)
            {
                vertexData[i * 3 + 0] = worldVertices[i].x;
                vertexData[i * 3 + 1] = worldVertices[i].y;
                vertexData[i * 3 + 2] = worldVertices[i].z;
            }
            
            IntPtr vertexPtr = Marshal.AllocHGlobal(vertexData.Length * sizeof(float));
            Marshal.Copy(vertexData, 0, vertexPtr, vertexData.Length);
            
            try
            {
                NamakoNative.UpdateContactRigidBodyPos(
                    RigidBodyName,
                    vertexPtr, worldVertices.Length);
            }
            finally
            {
                Marshal.FreeHGlobal(vertexPtr);
            }
        }
        
        // Inspector buttons
        [ContextMenu("Grab Nodes")]
        private void GrabNodesMenu() => GrabNodes();
        
        [ContextMenu("Release Nodes")]
        private void ReleaseNodesMenu() => ReleaseNodes();
    }
}
