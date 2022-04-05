using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Runtime.InteropServices;
using System.Linq;


namespace Namako
{

    [RequireComponent(typeof(TetContainer))]
    public class NamakoSolver : MonoBehaviour
    {

        public GameObject visualObj;
        public GameObject proxyObj;
        public GameObject inputObj;
        public float HIPRad = 0.03f;
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
        public enum CollisionDetectionType
        {
            // from namako.h
            FEMVTXVsRBSDF = 0,
            FEMSDFVsRBVTX = 1
        }
        public bool stressVisualization = false;
        public float stressLowerLimit = 0.0f;
        public float stressUpperLimit = 200.0f;
        [SerializeField] CollisionDetectionType cdtype = CollisionDetectionType.FEMVTXVsRBSDF;

        private float time = 0.0f;
        private IntPtr vmesh_pos_cpp;
        private IntPtr vmesh_indices_cpp;
        private IntPtr vmesh_stress_cpp;
        private float[] vmesh_pos;
        private float[] vmesh_stress;
        private float[] fem_pos;
        private IntPtr fem_pos_cpp;
        private IntPtr fem_tet_cpp;

        private Vector3 vec3tmp;
        private float[] float3tmp;
        private float[] float4tmp;
        private Quaternion qtmp;

        private MeshExtractor extractor = null;
        private TetContainer tetContainer;

        private Queue<float> calcTime;


        private GameObject[] nodeObj;

        [DllImport("namako")]
        private static extern void SetupFEM(
            float hip_rad, float young_kPa, float poisson,
            float density, float damping_alpha, float damping_beta,
            IntPtr fem_pos, int fem_nnodes, IntPtr fem_indices4, int fem_nfaces,
            IntPtr vismesh_pos, int vismesh_nnodes,
            IntPtr vismesh_faces, int vismesh_nfaces,
            int collision_detection_mode);
        [DllImport("namako")] private static extern int GetNumNodes();
        [DllImport("namako")] private static extern int GetNumElems();
        [DllImport("namako")] private static extern void GetNodePos(IntPtr pos);
        [DllImport("namako")] private static extern void GetRBPos(IntPtr pos);
        [DllImport("namako")] private static extern void GetVisMeshPos(IntPtr pos);
        [DllImport("namako")] private static extern void GetVisMeshStress(IntPtr stress);
        [DllImport("namako")] private static extern void Terminate();
        [DllImport("namako")] private static extern void FixBottom(float range_zero_to_one);
        [DllImport("namako")] private static extern void ScaleStiffness(float scale);
        [DllImport("namako")] private static extern void SetFriction(float friction);
        [DllImport("namako")] private static extern void SetVCStiffness(float kc);
        [DllImport("namako")] private static extern void GetRotationXYZW(IntPtr xyzw);
        [DllImport("namako")] private static extern void SetHandleOffset(float x, float y, float z);
        [DllImport("namako")] private static extern float GetScaledYoungsModulus();
        [DllImport("namako", EntryPoint = "IsContact")] private static extern bool IsContactC();
        [DllImport("namako")] private static extern void GetDisplayingForce(IntPtr force);
        [DllImport("namako")] private static extern void GetContactNormal(IntPtr n);
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

            // Prepare fem_pos_cpp
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

            // Prepare visual mesh information
            if(visualObj != null)
            {
                extractor = new MeshExtractor(visualObj);

                vmesh_indices_cpp = Marshal.AllocHGlobal(3 * extractor.n_tri_all * sizeof(int));

                Marshal.Copy(
                    extractor.indices_all, 0,
                    vmesh_indices_cpp, extractor.indices_all.Length);

                vmesh_pos_cpp = Marshal.AllocHGlobal(3 * extractor.n_vert_all * sizeof(float));

                Marshal.Copy(
                    extractor.vmesh_pos, 0,
                    vmesh_pos_cpp, 3 * extractor.n_vert_all);

                vmesh_pos = new float[3 * extractor.n_vert_all];

                vmesh_stress_cpp = Marshal.AllocHGlobal(extractor.n_vert_all * sizeof(float));
                vmesh_stress = new float[extractor.n_vert_all];
            } else
            {
                vmesh_pos_cpp = IntPtr.Zero;
                vmesh_stress_cpp = IntPtr.Zero;
            }

            // FEM
            int vmesh_vertices = -1;
            int vmesh_nfaces = -1;
            if (extractor != null)
            {
                vmesh_vertices = extractor.n_vert_all;
                vmesh_nfaces = extractor.n_tri_all;
            }
            SetupFEM(HIPRad, youngsModulusKPa, poisson, density, damping_alpha, damping_beta,
                fem_pos_cpp, num_nodes, fem_tet_cpp, num_tets,
                vmesh_pos_cpp, vmesh_vertices, vmesh_indices_cpp, vmesh_nfaces, (int)cdtype);

            nodeObj = tetContainer.NodeObj;

            //StartLog();

            // Alloc copy buffer
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
                Debug.Log("Calc time: " + ct.ToString($"F2") + " [ms/loop]");
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
                if (!float.IsNaN(vec3tmp.magnitude))
                {
                    proxyObj.transform.position = vec3tmp;
                }
                Marshal.FreeHGlobal(p_cpp);
                // Rotation
                IntPtr q_cpp = Marshal.AllocHGlobal(4 * sizeof(float));
                GetRotationXYZW(q_cpp);
                Marshal.Copy(q_cpp, float4tmp, 0, 4);
                qtmp.Set(float4tmp[0], float4tmp[1], float4tmp[2], float4tmp[3]);
                proxyObj.transform.rotation = qtmp;
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

            if(visualObj != null)
            {
                // Get vertices of visual mesh
                GetVisMeshPos(vmesh_pos_cpp);
                // Copy pos_cpp to mesh.vertices
                Marshal.Copy(vmesh_pos_cpp, vmesh_pos, 0, 3 * extractor.n_vert_all);
                extractor.UpdatePosition(vmesh_pos);

                if (stressVisualization)
                {
                    GetVisMeshStress(vmesh_stress_cpp);
                    Marshal.Copy(vmesh_stress_cpp, vmesh_stress, 0, extractor.n_vert_all);
                    extractor.UpdateVertexColor(vmesh_stress, stressLowerLimit, stressUpperLimit);
                }
            }

            // Nodes 
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


        private void OnDestroy()
        {
            Marshal.FreeHGlobal(vmesh_indices_cpp);
            Marshal.FreeHGlobal(vmesh_pos_cpp);
            Marshal.FreeHGlobal(vmesh_stress_cpp);
            Marshal.FreeHGlobal(fem_pos_cpp);
            Marshal.FreeHGlobal(fem_tet_cpp);
            Terminate();
        }

        public Vector3 GetForce()
        {
            IntPtr ptr = Marshal.AllocHGlobal(3 * sizeof(float));
            var arr = new float[3];
            GetDisplayingForce(ptr);
            Marshal.Copy(ptr, arr, 0, 3);
            Marshal.FreeHGlobal(ptr);
            return new Vector3(arr[0], arr[1], arr[2]);
        }

        public Vector3 GetNormal()
        {
            IntPtr ptr = Marshal.AllocHGlobal(3 * sizeof(float));
            var arr = new float[3];
            GetContactNormal(ptr);
            Marshal.Copy(ptr, arr, 0, 3);
            Marshal.FreeHGlobal(ptr);
            return new Vector3(arr[0], arr[1], arr[2]);
        }
        public bool IsContact()
        {
            return IsContactC();
        }
    }

}