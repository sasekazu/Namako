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

        [Tooltip("FEMに埋め込む描画用オブジェクト"), Header("Input Objects")]
        public GameObject visualObj;
        [Tooltip("描画用剛体オブジェクト")]
        public GameObject proxyObj;
        [Tooltip("剛体位置入力用オブジェクト")]
        public GameObject inputObj;
        [Tooltip("剛体半径"), Range(0.001f, 0.1f), Header("Simulation Parameters")]
        public float HIPRad = 0.03f;
        [Tooltip("ヤング率 [kPa]"), Range(0.0f, 10.0f)]
        public float youngsModulusKPa = 0.6f;
        [Tooltip("ポアソン比"), Range(0.0f, 0.49f)]
        public float poisson = 0.4f;
        [Tooltip("密度"), Range(0.0f, 2000.0f)]
        public float density = 1000.0f;
        [Tooltip("ダンパ係数α"), Range(0.0f, 1.0f)]
        public float damping_alpha = 0.0f;
        [Tooltip("ダンパ係数β"), Range(0.0f, 1.0f)]
        public float damping_beta = 0.1f;
        [Tooltip("疑似摩擦係数（0-1）"), Range(0, 1)]
        public float friction = 0.2f;
        [Tooltip("固定高さ（底面から, 0-1）"), Range(0, 1)]
        public float fixHeight = 0.01f;
        [Tooltip("バーチャルカップリングのばね定数"), Range(0.0f, 1000.0f)]
        public float VCStiffness = 300.0f;
        [Tooltip("グローバルダンピング係数"), Range(0.0f, 1.0f)]
        public float globalDamping = 0.0f;
        [Tooltip("柔軟物体にかかる重力")]
        public Vector3 gravityFEM = Vector3.zero;
        [Tooltip("剛体にかかる重力")]
        public Vector3 gravityRb = Vector3.zero;
        public enum CollisionDetectionType
        {
            // from namako.h
            FEMVTXVsRBSDF = 0,
            FEMSDFVsRBVTX = 1
        }
        [SerializeField] CollisionDetectionType cdtype = CollisionDetectionType.FEMVTXVsRBSDF;

        [Tooltip("力覚提示を有効にする"), Header("Haptics")]
        public bool hapticEnabled = true;
        [Tooltip("床に触れるようにする")]
        public bool floorEnabled = true;
        [Tooltip("力覚提示を開始するまでの猶予時間[s]")]
        public float waitTime = 0.5f;

        [Tooltip("応力の可視化を有効にする"), Header("Stress Visualization")]
        public bool stressVisualization = false;
        [Tooltip("応力描画の下限値")]
        public float stressLowerLimit = 0.0f;
        [Tooltip("応力描画の上限値")]
        public float stressUpperLimit = 200.0f;

        private float time = 0.0f;
        private IntPtr vmesh_pos_cpp;
        private IntPtr vmesh_indices_cpp;
        private IntPtr vmesh_stress_cpp;
        private float[] vmesh_pos;
        private float[] vmesh_stress;
        private float[] fem_pos;
        private IntPtr fem_pos_cpp;
        private IntPtr fem_tet_cpp;

        private MeshExtractor extractor = null;
        private TetContainer tetContainer;

        private Queue<float> calcTime;


        private GameObject[] nodeObj;

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
            NamakoNative.SetupFEM(HIPRad, youngsModulusKPa, poisson, density, damping_alpha, damping_beta,
                fem_pos_cpp, num_nodes, fem_tet_cpp, num_tets,
                vmesh_pos_cpp, vmesh_vertices, vmesh_indices_cpp, vmesh_nfaces, (int)cdtype);

            nodeObj = tetContainer.NodeObj;

            //StartLog();

            NamakoNative.SetFloorHapticsEnabled(true);

            Debug.Log("Number of nodes: " + NamakoNative.GetNumNodes());
            Debug.Log("Number of elements: " + NamakoNative.GetNumElems());

            calcTime = new Queue<float>();
        }

        void Update()
        {

            time += Time.deltaTime;

            // Measure time
            calcTime.Enqueue((float)NamakoNative.GetCalcTime());
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
                NamakoNative.SetHapticEnabled(false);
            }
            else
            {
                NamakoNative.SetHapticEnabled(hapticEnabled);
            }

            // Set handle offset
            Vector3 handleOffset = inputObj.transform.position;
            NamakoNative.SetHandleOffset(handleOffset.x, handleOffset.y, handleOffset.z);

            // Rigid body
            {
                // Position
                IntPtr p_cpp = Marshal.AllocHGlobal(3 * sizeof(float));
                NamakoNative.GetRBPos(p_cpp);
                float[] p = new float[3];
                Vector3 pVec;
                Marshal.Copy(p_cpp, p, 0, 3);
                pVec.x = p[0];
                pVec.y = p[1];
                pVec.z = p[2];
                if (!float.IsNaN(pVec.magnitude))
                {
                    proxyObj.transform.position = pVec;
                }
                Marshal.FreeHGlobal(p_cpp);
                // Rotation
                IntPtr q_cpp = Marshal.AllocHGlobal(4 * sizeof(float));
                NamakoNative.GetRotationXYZW(q_cpp);
                float[] q = new float[4];
                Marshal.Copy(q_cpp, q, 0, 4);
                proxyObj.transform.rotation = new Quaternion(q[0], q[1], q[2], q[3]);
                Marshal.FreeHGlobal(q_cpp);
                // Misc
                NamakoNative.SetGravityRb(gravityRb.x, gravityRb.y, gravityRb.z);
            }

            // Change haptic properties
            NamakoNative.ScaleStiffness(youngsModulusKPa);
            NamakoNative.SetFriction(friction);
            NamakoNative.SetVCStiffness(VCStiffness);
            NamakoNative.SetGlobalDamping(globalDamping);
            NamakoNative.SetFloorHapticsEnabled(floorEnabled);

            // Apply boundary conditions
            NamakoNative.FixBottom(fixHeight);
            NamakoNative.SetGravity(gravityFEM.x, gravityFEM.y, gravityFEM.z);

            if(visualObj != null)
            {
                // Get vertices of visual mesh
                NamakoNative.GetVisMeshPos(vmesh_pos_cpp);
                // Copy pos_cpp to mesh.vertices
                Marshal.Copy(vmesh_pos_cpp, vmesh_pos, 0, 3 * extractor.n_vert_all);
                extractor.UpdatePosition(vmesh_pos);

                if (stressVisualization)
                {
                    NamakoNative.GetVisMeshStress(vmesh_stress_cpp);
                    Marshal.Copy(vmesh_stress_cpp, vmesh_stress, 0, extractor.n_vert_all);
                    extractor.UpdateVertexColor(vmesh_stress, stressLowerLimit, stressUpperLimit);
                }
            }

            // Nodes 
            int num_nodes = NamakoNative.GetNumNodes();
            NamakoNative.GetNodePos(fem_pos_cpp);
            Marshal.Copy(fem_pos_cpp, fem_pos, 0, 3 * num_nodes);
            for (int i = 0; i < num_nodes; ++i)
            {
                nodeObj[i].GetComponent<MeshRenderer>().enabled = true;
                nodeObj[i].transform.position = new Vector3(fem_pos[3*i+0], fem_pos[3*i+1], fem_pos[3*i+2]);
            }
        }


        private void OnDestroy()
        {
            Marshal.FreeHGlobal(vmesh_indices_cpp);
            Marshal.FreeHGlobal(vmesh_pos_cpp);
            Marshal.FreeHGlobal(vmesh_stress_cpp);
            Marshal.FreeHGlobal(fem_pos_cpp);
            Marshal.FreeHGlobal(fem_tet_cpp);
            NamakoNative.Terminate();
        }

        public Vector3 GetForce()
        {
            IntPtr ptr = Marshal.AllocHGlobal(3 * sizeof(float));
            var arr = new float[3];
            NamakoNative.GetDisplayingForce(ptr);
            Marshal.Copy(ptr, arr, 0, 3);
            Marshal.FreeHGlobal(ptr);
            return new Vector3(arr[0], arr[1], arr[2]);
        }

        public Vector3 GetNormal()
        {
            IntPtr ptr = Marshal.AllocHGlobal(3 * sizeof(float));
            var arr = new float[3];
            NamakoNative.GetContactNormal(ptr);
            Marshal.Copy(ptr, arr, 0, 3);
            Marshal.FreeHGlobal(ptr);
            return new Vector3(arr[0], arr[1], arr[2]);
        }
        public bool IsContact()
        {
            return NamakoNative.IsContactC();
        }
    }

}