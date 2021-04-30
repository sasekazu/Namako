using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Runtime.InteropServices;


namespace Namako
{

    [RequireComponent(typeof(TetContainer))]
    public class NamakoSolver : MonoBehaviour
    {

        public GameObject deviceObj;
        public GameObject inputObj;
        public GameObject visualObj;
        public Boolean viewNodes = false;
        public float HIPRad = 0.05f;
        public float youngsModulusKPa = 0.6f;
        public float poisson = 0.4f;
        public float density = 1000.0f;
        public float damping_alpha = 0.0f;
        public float damping_beta = 0.1f;
        public float friction = 0.2f;
        public float fixHeight = 0.01f;
        public float VCStiffness = 300.0f;
        public float globalDamping = 0.0f;
        public bool hapticEnabled = true;
        public bool floorEnabled = true;
        public float waitTime = 0.5f;
        public Vector3 gravityFEM = Vector3.zero;
        public Vector3 gravityRb = Vector3.zero;
        
        private float time = 0.0f;
        private IntPtr vmesh_pos_cpp;
        private IntPtr vmesh_indices_cpp;
        private float[] fem_pos;
        private IntPtr fem_pos_cpp;
        private IntPtr fem_tet_cpp;
        private float[] managed_array;

        private Vector3 vec3tmp;
        private float[] float3tmp;
        private float[] float4tmp;
        private Quaternion qtmp;

        private MeshFilter[] mfs;
        private int n_mesh;
        private int[] n_tri;
        private int[] n_vert;
        private int n_tri_all;
        private int n_vert_all;
        private int[] vert_offsets;
        private int[] tri_offsets;
        private TetContainer tetContainer;

        private Queue<float> calcTime;


        private GameObject[] nodeObj;

        [DllImport("namako")]
        private static extern void SetupFEM(
            float hip_rad, float young_kPa, float poisson,
            float density, float damping_alpha, float damping_beta,
            IntPtr fem_pos, int fem_nnodes, IntPtr fem_indices4, int fem_nfaces,
            IntPtr vismesh_pos, int vismesh_nnodes, IntPtr vismesh_indices3, int vismesh_nfaces);
        [DllImport("namako")] private static extern void UpdateFEM(float dt, IntPtr pos);
        [DllImport("namako")] private static extern int GetNumNodes();
        [DllImport("namako")] private static extern int GetNumElems();
        [DllImport("namako")] private static extern void GetNodePos(IntPtr pos);
        [DllImport("namako")] private static extern void GetRBPos(IntPtr pos);
        [DllImport("namako")] private static extern void Terminate();
        [DllImport("namako")] private static extern void FixBottom(float range_zero_to_one);
        [DllImport("namako")] private static extern void ScaleStiffness(float scale);
        [DllImport("namako")] private static extern void SetFriction(float friction);
        [DllImport("namako")] private static extern void SetVCStiffness(float kc);
        [DllImport("namako")] private static extern void GetRotationXYZW(IntPtr xyzw);
        [DllImport("namako")] private static extern void SetHandleOffset(float x, float y, float z);
        [DllImport("namako")] private static extern float GetScaledYoungsModulus();
        [DllImport("namako")] private static extern double GetCalcTime();
        [DllImport("namako")] private static extern double GetLoopTime();
        [DllImport("namako")] private static extern void SetHapticEnabled(bool enabled);
        [DllImport("namako")] private static extern void SetFloorCollisionEnabled(bool enabled);
        [DllImport("namako")] private static extern void SetRBFEMCollisionEnabled(bool enabled);
        [DllImport("namako")] private static extern void SetFloorHapticsEnabled(bool enabled);
        [DllImport("namako")] private static extern void SetGlobalDamping(float damping);
        [DllImport("namako")] private static extern void SetGravity(float gx, float gy, float gz);
        [DllImport("namako")] private static extern void SetGravityRb(float gx, float gy, float gz);
        [DllImport("namako")] private static extern void SetWaitTime(int wait_ms);
        [DllImport("namako")] private static extern void StartLog();
        [DllImport("namako")] private static extern void StopLog();



        void Start()
        {
            tetContainer = GetComponent<TetContainer>();

            mfs = visualObj.GetComponentsInChildren<MeshFilter>();

            n_mesh = mfs.Length;
            n_tri = new int[n_mesh];
            n_vert = new int[n_mesh];
            n_tri_all = 0;
            n_vert_all = 0;
            vert_offsets = new int[n_mesh];
            tri_offsets = new int[n_mesh];
            for (int i = 0; i < n_mesh; ++i)
            {
                Mesh mesh = mfs[i].mesh;
                // We consider only one submesh context.
                n_tri[i] = mesh.GetIndices(0).GetLength(0) / 3;
                n_vert[i] = mesh.vertices.GetLength(0);
                n_tri_all += n_tri[i];
                n_vert_all += n_vert[i];
                for (int j = 0; j < i; ++j)
                {
                    vert_offsets[i] += n_vert[j];
                    tri_offsets[i] += n_tri[j];
                }
            }

            vmesh_indices_cpp = Marshal.AllocHGlobal(3 * n_tri_all * sizeof(int));
            vmesh_pos_cpp = Marshal.AllocHGlobal(3 * n_vert_all * sizeof(float));

            // Prepare vmesh_indices_cpp
            Vector3[] pos_all = new Vector3[n_vert_all];
            int[] indices_all = new int[3 * n_tri_all];
            for (int i = 0; i < n_mesh; ++i)
            {
                Mesh mesh = mfs[i].mesh;
                for (int j = 0; j < n_vert[i]; ++j)
                {
                    pos_all[vert_offsets[i] + j] = mesh.vertices[j];
                }
                int[] tri = mesh.GetIndices(0);
                for (int j = 0; j < 3 * n_tri[i]; ++j)
                {
                    indices_all[3 * tri_offsets[i] + j] = tri[j] + vert_offsets[i];
                }
            }

            Marshal.Copy(indices_all, 0, vmesh_indices_cpp, indices_all.Length);

            // Prepare vmesh_pos_cpp
            int l = 3 * n_vert_all;
            float[] vmesh_pos = new float[l];
            for (int i = 0; i < n_vert_all; ++i)
            {
                // Convert local coodinate to world coordinate
                Vector3 worldpt = visualObj.transform.localToWorldMatrix.MultiplyPoint3x4(pos_all[i]);
                for (int j = 0; j < 3; ++j)
                {
                    vmesh_pos[3 * i + j] = worldpt[j];
                }
            }
            vmesh_pos_cpp = Marshal.AllocHGlobal(l * sizeof(float));
            Marshal.Copy(vmesh_pos, 0, vmesh_pos_cpp, l);

            Vector3[] posw = tetContainer.GetNodePosW();
            int num_nodes = posw.Length;
            fem_pos = new float[3 * num_nodes];
            Vector3 s = transform.localScale;
            Vector3 t = transform.position;
            for (int i = 0; i < num_nodes; ++i)
            {
                fem_pos[3 * i + 0] = posw[i].x;
                fem_pos[3 * i + 1] = posw[i].y;
                fem_pos[3 * i + 2] = posw[i].z;
            }
            fem_pos_cpp = Marshal.AllocHGlobal(3 * num_nodes * sizeof(float));
            Marshal.Copy(fem_pos, 0, fem_pos_cpp, 3 * num_nodes);

            // Prepare fem_indices_cpp
            int num_tets = tetContainer.Tets;
            int[] fem_tet = new int[4 * num_tets];
            System.Array.Copy(tetContainer.Tet, fem_tet, 4 * num_tets);
            fem_tet_cpp = Marshal.AllocHGlobal(4 * num_tets * sizeof(int));
            Marshal.Copy(fem_tet, 0, fem_tet_cpp, 4 * num_tets);

            // FEM
            SetupFEM(HIPRad, youngsModulusKPa, poisson, density, damping_alpha, damping_beta,
                fem_pos_cpp, num_nodes, fem_tet_cpp, num_tets,
                vmesh_pos_cpp, n_vert_all, vmesh_indices_cpp, n_tri_all);
            nodeObj = tetContainer.NodeObj;

            StartLog();

            // Alloc copy buffer
            managed_array = new float[3 * n_vert_all];
            vec3tmp = new Vector3();
            float3tmp = new float[3];
            float4tmp = new float[4];
            qtmp = new Quaternion();

            SetFloorHapticsEnabled(true);

            Debug.Log("Number of nodes: " + GetNumNodes());
            Debug.Log("Number of elements: " + GetNumElems());

            calcTime = new Queue<float>();
        }

        void Update()
        {

            time += Time.deltaTime;

            // Measure time
            calcTime.Enqueue((float)GetCalcTime());
            if (calcTime.Count == 100)
            {
                float ct = 0.0f;
                foreach (float t in calcTime)
                {
                    ct += t;
                }
                ct /= calcTime.Count;
                Debug.Log("time " + ct);
                calcTime.Clear();
            }


            if (time < waitTime)
            {
                SetHapticEnabled(false);
            }
            else
            {
                SetHapticEnabled(hapticEnabled);
            }

            // Set handle offset
            Vector3 handleOffset = inputObj.transform.position;
            SetHandleOffset(handleOffset.x, handleOffset.y, handleOffset.z);

            // Rigid body
            {
                // Position
                IntPtr p_cpp = Marshal.AllocHGlobal(3 * sizeof(float));
                GetRBPos(p_cpp);
                Marshal.Copy(p_cpp, float3tmp, 0, 3);
                vec3tmp.x = float3tmp[0];
                vec3tmp.y = float3tmp[1];
                vec3tmp.z = float3tmp[2];
                deviceObj.transform.position = vec3tmp;
                Marshal.FreeHGlobal(p_cpp);
                // Rotation
                IntPtr q_cpp = Marshal.AllocHGlobal(4 * sizeof(float));
                GetRotationXYZW(q_cpp);
                Marshal.Copy(q_cpp, float4tmp, 0, 4);
                qtmp.Set(float4tmp[0], float4tmp[1], float4tmp[2], float4tmp[3]);
                deviceObj.transform.rotation = qtmp;
                Marshal.FreeHGlobal(q_cpp);
                // Misc
                SetGravityRb(gravityRb.x, gravityRb.y, gravityRb.z);
            }

            // Change haptic properties
            ScaleStiffness(youngsModulusKPa);
            SetFriction(friction);
            SetVCStiffness(VCStiffness);
            SetGlobalDamping(globalDamping);
            SetFloorHapticsEnabled(floorEnabled);

            // Apply boundary conditions
            FixBottom(fixHeight);
            SetGravity(gravityFEM.x, gravityFEM.y, gravityFEM.z);

            // Solve FEM
            UpdateFEM(0.03f, vmesh_pos_cpp);

            // Copy pos_cpp to mesh.vertices
            Marshal.Copy(vmesh_pos_cpp, managed_array, 0, 3 * n_vert_all);
            for (int m = 0; m < n_mesh; ++m)
            {
                Mesh mesh = mfs[m].mesh;
                Vector3[] pos = mesh.vertices;
                for (int i = 0; i < n_vert[m]; ++i)
                {
                    int pos_id = vert_offsets[m] + i;
                    pos[i][0] = managed_array[3 * pos_id + 0];
                    pos[i][1] = managed_array[3 * pos_id + 1];
                    pos[i][2] = managed_array[3 * pos_id + 2];
                    pos[i] = visualObj.transform.worldToLocalMatrix.MultiplyPoint3x4(pos[i]);
                }

                mesh.vertices = pos;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
            }

            // Nodes 
            if (viewNodes)
            {
                int num_nodes = GetNumNodes();
                GetNodePos(fem_pos_cpp);
                Marshal.Copy(fem_pos_cpp, fem_pos, 0, 3 * num_nodes);
                for (int i = 0; i < num_nodes; ++i)
                {
                    nodeObj[i].GetComponent<MeshRenderer>().enabled = true;
                    vec3tmp.x = fem_pos[3 * i + 0];
                    vec3tmp.y = fem_pos[3 * i + 1];
                    vec3tmp.z = fem_pos[3 * i + 2];
                    nodeObj[i].transform.position = vec3tmp;
                }
            }
            else
            {
                int num_nodes = GetNumNodes();
                for (int i = 0; i < num_nodes; ++i)
                {
                    nodeObj[i].GetComponent<MeshRenderer>().enabled = false;
                }
            }
        }


        private void OnDestroy()
        {
            Marshal.FreeHGlobal(vmesh_indices_cpp);
            Marshal.FreeHGlobal(vmesh_pos_cpp);
            Marshal.FreeHGlobal(fem_pos_cpp);
            Marshal.FreeHGlobal(fem_tet_cpp);
            Terminate();
        }
    }

}