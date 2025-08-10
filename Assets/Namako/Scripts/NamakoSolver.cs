using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Runtime.InteropServices;
using System.Linq;

/*
 * 【開発者向け重要事項】
 * このNamakoSolverコンポーネントは手動で追加することはできません。
 * 
 * スクリプトからNamakoSolverを追加する場合は、以下の専用メソッドを使用してください：
 * 
 * #if UNITY_EDITOR
 * NamakoSolver solver = NamakoSolver.CreateFromTool(targetGameObject);
 * #endif
 * 
 * 理由：
 * - NamakoSolverはSingletonパターンを採用しており、シーンに一つのみ存在可能
 * - コンポーネントメニューからの手動追加は無効化されています
 * - 適切な初期化と制約チェックが必要です
 * 
 * 通常の使用では、NamakoMeshToolを使用してNamakoSolverを作成してください。
 */


namespace Namako
{

    [RequireComponent(typeof(NamakoTetraMesh))]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]  // コンポーネントメニューから削除
    public class NamakoSolver : MonoBehaviour
    {
        // Singleton instance
        private static NamakoSolver instance;
        public static NamakoSolver Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<NamakoSolver>();
                }
                return instance;
            }
        }

        [Tooltip("FEMに埋め込む描画用オブジェクト"), Header("Input Objects")]
        public GameObject visualObj;
        [Tooltip("ヤング率 [kPa]"), Range(0.0f, 10.0f), Header("Simulation Parameters")]
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
        [Tooltip("グローバルダンピング係数"), Range(0.0f, 1.0f)]
        public float globalDamping = 0.0f;
        [Tooltip("柔軟物体にかかる重力")]
        public Vector3 gravityFEM = Vector3.zero;
        public enum CollisionDetectionType
        {
            // from namako.h
            FEMVTXVsRBSDF = 0,
            FEMSDFVsRBVTX = 1
        }
        [SerializeField] CollisionDetectionType collisionDetectionType = CollisionDetectionType.FEMVTXVsRBSDF;

        [Tooltip("実行開始時に自動的にFEMを開始する"), Header("FEM Control")]
        public bool autoStartFEM = true;

        private float time = 0.0f;
        private IntPtr vmesh_pos_cpp;
        private IntPtr vmesh_indices_cpp;
        private float[] vmesh_pos;
        private float[] fem_pos;
        private IntPtr fem_pos_cpp;
        private IntPtr fem_tet_cpp;

        private MeshExtractor extractor = null;
        private NamakoTetraMesh tetraMesh;

        private Queue<float> calcTime;
        private bool isInitialized = false;
        private bool isFEMStarted = false;
        private int num_nodes;
        private int num_tets;

        private GameObject[] nodeObj;
        private WireframeRenderer wireframeRenderer;

        // Contact rigid body management
        private NamakoRigidBody[] namakoRigidBodies; // NamakoRigidBodyコンポーネントを持つ剛体配列

#if UNITY_EDITOR
        /// <summary>
        /// NamakoMeshToolから呼び出される専用の作成メソッド
        /// </summary>
        public static NamakoSolver CreateFromTool(GameObject targetGameObject)
        {
            if (targetGameObject == null) return null;

            // 既に存在するかチェック
            NamakoSolver existingSolver = targetGameObject.GetComponent<NamakoSolver>();
            if (existingSolver != null) return existingSolver;

            // シーン内に他のNamakoSolverが存在するかチェック
            if (FindObjectOfType<NamakoSolver>() != null)
            {
                Debug.LogError("シーンに既にNamakoSolverが存在します。");
                return null;
            }

            // NamakoSolverを追加
            return targetGameObject.AddComponent<NamakoSolver>();
        }
#endif

#if UNITY_EDITOR
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
#endif

        void Awake()
        {
            // Singletonパターンの実装
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Debug.LogWarning("NamakoSolverは既にシーンに存在します。重複するインスタンスを削除します。");
                Destroy(gameObject);
                return;
            }
        }

        void Start()
        {
            InitializeFEM();
            
            if (autoStartFEM)
            {
                StartFEM();
            }
        }
        
        private void InitializeFEM()
        {
            if (isInitialized) return;

            tetraMesh = GetComponent<NamakoTetraMesh>();

            // Initialize wireframe renderer
            GameObject wireframeObj = GameObject.Find("tetras_wireframe");
            if (wireframeObj != null)
            {
                wireframeRenderer = wireframeObj.GetComponent<WireframeRenderer>();
            }

            // Prepare fem_pos_cpp
            Vector3[] posw = tetraMesh.GetNodePosW();
            num_nodes = posw.Length;
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
            num_tets = tetraMesh.Tets;
            int[] fem_tet = new int[4 * num_tets];
            System.Array.Copy(tetraMesh.Tet, fem_tet, 4 * num_tets);
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
            } else
            {
                vmesh_pos_cpp = IntPtr.Zero;
            }

            nodeObj = tetraMesh.NodeObj;
            calcTime = new Queue<float>();

            // Initialize wireframe renderer with tet data
            if (wireframeRenderer != null)
            {
                int[] tetIndices = tetraMesh.Tet;
                int tetCount = tetraMesh.Tets;
                wireframeRenderer.Initialize(nodeObj, tetIndices, tetCount);
            }

            // Collect contact rigid bodies using NamakoRigidBody components
            CollectNamakoRigidBodies();

            isInitialized = true;
        }

        public void StartFEM()
        {
            if (!isInitialized)
            {
                InitializeFEM();
            }
            
            if (isFEMStarted) return;

            // FEM setup - Get HIP radius from NamakoHaptics component or use default
            float hipRadius = 0.0f; // When hipRadius is zero, Haptics will not be generated

            // Try to find NamakoHaptics component in the scene
            var namakoHapticsObjects = FindObjectsOfType<MonoBehaviour>();
            foreach (var obj in namakoHapticsObjects)
            {
                if (obj.GetType().Name == "NamakoHaptics")
                {
                    var hipRadProperty = obj.GetType().GetMethod("GetHIPRadius");
                    if (hipRadProperty != null)
                    {
                        hipRadius = (float)hipRadProperty.Invoke(obj, null);
                        break;
                    }
                }
            }

            NamakoNative.SetupFEM(hipRadius, youngsModulusKPa, poisson, density, damping_alpha, damping_beta,
                fem_pos_cpp, num_nodes, fem_tet_cpp, num_tets, (int)collisionDetectionType);

            // Setup visual mesh if available
            if (extractor != null)
            {
                NamakoNative.SetupVisMesh(vmesh_pos_cpp, extractor.n_vert_all, vmesh_indices_cpp, extractor.n_tri_all);
            }

            // Start simulation
            NamakoNative.StartSimulation();

            // Register contact rigid bodies to native library after setup
            RegisterAllContactRigidBodies();

            //StartLog();

            NamakoNative.SetFloorHapticsEnabled(true);

            Debug.Log("Number of nodes: " + NamakoNative.GetNumNodes());
            Debug.Log("Number of elements: " + NamakoNative.GetNumElems());

            isFEMStarted = true;
        }

        public void StopFEM()
        {
            if (isFEMStarted)
            {
                NamakoNative.Terminate();
                isFEMStarted = false;
            }
        }

        void Update()
        {
            if (!isFEMStarted) return;

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


            // Change FEM properties
            NamakoNative.ScaleStiffness(youngsModulusKPa);
            NamakoNative.SetFriction(friction);
            NamakoNative.SetGlobalDamping(globalDamping);

            // Apply boundary conditions
            ApplyBoundaryConditions();
            NamakoNative.SetGravity(gravityFEM.x, gravityFEM.y, gravityFEM.z);

            // Update contact rigid body positions
            UpdateContactRigidBodyPositions();

            if(visualObj != null)
            {
                // Get vertices of visual mesh
                NamakoNative.GetVisMeshPos(vmesh_pos_cpp);
                // Copy pos_cpp to mesh.vertices
                Marshal.Copy(vmesh_pos_cpp, vmesh_pos, 0, 3 * extractor.n_vert_all);
                extractor.UpdatePosition(vmesh_pos);
            }

            // Nodes 
            int num_nodes = NamakoNative.GetNumNodes();
            NamakoNative.GetNodePos(fem_pos_cpp);
            Marshal.Copy(fem_pos_cpp, fem_pos, 0, 3 * num_nodes);
            for (int i = 0; i < num_nodes; ++i)
            {
                nodeObj[i].GetComponent<MeshRenderer>().enabled = true;
                
                // 境界条件をチェックして、固定ノードの場合は位置を更新しない
                NodeBoundaryCondition boundaryCondition = nodeObj[i].GetComponent<NodeBoundaryCondition>();
                bool isNodeFixed = boundaryCondition != null && boundaryCondition.isFixed;
                
                if (!isNodeFixed)
                {
                    nodeObj[i].transform.position = new Vector3(fem_pos[3*i+0], fem_pos[3*i+1], fem_pos[3*i+2]);
                }
            }
            
            // Update wireframe if it exists
            UpdateWireframeIfNeeded();
        }
        
        private void UpdateWireframeIfNeeded()
        {
            wireframeRenderer?.ForceUpdateWireframe();
        }


        private void OnDestroy()
        {
            // Singletonインスタンスのクリア
            if (instance == this)
            {
                instance = null;
            }

            if (isInitialized)
            {
                Marshal.FreeHGlobal(vmesh_indices_cpp);
                Marshal.FreeHGlobal(vmesh_pos_cpp);
                Marshal.FreeHGlobal(fem_pos_cpp);
                Marshal.FreeHGlobal(fem_tet_cpp);
            }
            if (isFEMStarted)
            {
                NamakoNative.Terminate();
            }
        }

        public bool IsInitialized => isInitialized;
        public bool IsFEMStarted => isFEMStarted;

        /// <summary>
        /// MeshExtractorへの参照を取得
        /// </summary>
        /// <returns>MeshExtractorインスタンス</returns>
        public MeshExtractor GetMeshExtractor()
        {
            return extractor;
        }

        /// <summary>
        /// 全ての接触剛体をネイティブライブラリに登録
        /// </summary>
        private void RegisterAllContactRigidBodies()
        {
            if (namakoRigidBodies == null)
                return;

            for (int i = 0; i < namakoRigidBodies.Length; i++)
            {
                if (namakoRigidBodies[i] != null)
                {
                    namakoRigidBodies[i].RegisterToNative();
                }
            }
        }

        /// <summary>
        /// 接触剛体の位置をネイティブライブラリに更新
        /// </summary>
        private void UpdateContactRigidBodyPositions()
        {
            if (namakoRigidBodies == null || !isFEMStarted)
                return;

            for (int i = 0; i < namakoRigidBodies.Length; i++)
            {
                if (namakoRigidBodies[i] != null)
                {
                    namakoRigidBodies[i].UpdatePositionInNative();
                }
            }
        }

        /// <summary>
        /// 境界条件をネイティブライブラリに適用
        /// </summary>
        private void ApplyBoundaryConditions()
        {
            if (nodeObj == null || nodeObj.Length == 0)
                return;

            // 境界条件が設定されているノードを収集
            var boundaryNodes = new List<(int nodeId, Vector3 displacement)>();
            
            for (int i = 0; i < nodeObj.Length; i++)
            {
                if (nodeObj[i] == null) continue;
                
                NodeBoundaryCondition boundaryCondition = nodeObj[i].GetComponent<NodeBoundaryCondition>();
                if (boundaryCondition != null && boundaryCondition.isFixed)
                {
                    // GameObjectの名前からノードIDを抽出
                    int nodeId = ExtractNodeIdFromName(nodeObj[i].name);
                    if (nodeId >= 0)
                    {
                        boundaryNodes.Add((nodeId, boundaryCondition.displacement));
                    }
                }
            }

            // 境界条件が設定されているノードがない場合は何もしない
            if (boundaryNodes.Count == 0)
                return;

            // ノードIDと変位データを配列に変換
            int[] nodeIds = new int[boundaryNodes.Count];
            float[] displacements = new float[boundaryNodes.Count * 3];
            
            for (int i = 0; i < boundaryNodes.Count; i++)
            {
                nodeIds[i] = boundaryNodes[i].nodeId;
                displacements[i * 3 + 0] = boundaryNodes[i].displacement.x;
                displacements[i * 3 + 1] = boundaryNodes[i].displacement.y;
                displacements[i * 3 + 2] = boundaryNodes[i].displacement.z;
            }

            // メモリを確保してデータをコピー
            IntPtr nodeIdPtr = Marshal.AllocHGlobal(nodeIds.Length * sizeof(int));
            IntPtr displacementPtr = Marshal.AllocHGlobal(displacements.Length * sizeof(float));

            try
            {
                Marshal.Copy(nodeIds, 0, nodeIdPtr, nodeIds.Length);
                Marshal.Copy(displacements, 0, displacementPtr, displacements.Length);

                // ネイティブライブラリに境界条件を設定
                NamakoNative.SetBoundaryConditions(nodeIdPtr, nodeIds.Length, displacementPtr);
            }
            finally
            {
                // メモリを解放
                Marshal.FreeHGlobal(nodeIdPtr);
                Marshal.FreeHGlobal(displacementPtr);
            }
        }

        /// <summary>
        /// シーン内のNamakoRigidBodyコンポーネントを自動検出
        /// </summary>
        private void CollectNamakoRigidBodies()
        {
            namakoRigidBodies = FindObjectsOfType<NamakoRigidBody>();
            Debug.Log($"Total NamakoRigidBody components found: {namakoRigidBodies.Length}");
            
            for (int i = 0; i < namakoRigidBodies.Length; i++)
            {
                Debug.Log($"Found NamakoRigidBody: '{namakoRigidBodies[i].RigidBodyName}'");
            }
        }

        /// <summary>
        /// GameObjectの名前からノードIDを抽出
        /// 名前の末尾の数字をノードIDとして使用
        /// </summary>
        /// <param name="name">GameObjectの名前</param>
        /// <returns>ノードID（抽出できない場合は-1）</returns>
        private int ExtractNodeIdFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;

            // 名前の末尾から数字を抽出
            string numberStr = "";
            for (int i = name.Length - 1; i >= 0; i--)
            {
                if (char.IsDigit(name[i]))
                {
                    numberStr = name[i] + numberStr;
                }
                else
                {
                    break;
                }
            }

            if (int.TryParse(numberStr, out int nodeId))
            {
                return nodeId;
            }

            return -1;
        }
    }

}