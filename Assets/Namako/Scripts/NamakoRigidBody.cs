using UnityEngine;
using System;
using System.Runtime.InteropServices;

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
        /// Get mesh data
        /// </summary>
        /// <returns>Mesh data</returns>
        public Mesh GetMesh()
        {
            return meshFilter?.mesh;
        }
        
        /// <summary>
        /// Get world coordinate vertex array
        /// </summary>
        /// <returns>World coordinate vertex array</returns>
        public Vector3[] GetWorldVertices()
        {
            Mesh mesh = GetMesh();
            if (mesh == null) return null;
            
            Vector3[] localVertices = mesh.vertices;
            Vector3[] worldVertices = new Vector3[localVertices.Length];
            
            for (int i = 0; i < localVertices.Length; i++)
            {
                worldVertices[i] = transform.TransformPoint(localVertices[i]);
            }
            
            return worldVertices;
        }
        
        /// <summary>
        /// Get triangle indices
        /// </summary>
        /// <returns>Triangle index array</returns>
        public int[] GetTriangles()
        {
            return GetMesh()?.triangles;
        }
        
        /// <summary>
        /// Register this rigid body to native library
        /// </summary>
        public void RegisterToNative()
        {
            Mesh mesh = GetMesh();
            if (mesh == null)
            {
                Debug.LogWarning($"[{gameObject.name}] No mesh found");
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
