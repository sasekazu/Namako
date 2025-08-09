using UnityEngine;

namespace Namako.Samples
{
    /// <summary>
    /// ScalarBarGraphを使用してNamakoSolverの最大応力値を可視化するサンプル
    /// Canvasにアタッチして使用
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class StressVisualization : MonoBehaviour
    {
        [Header("Stress Visualization Settings")]
        [Tooltip("最大応力値 (Pa)")]
        [SerializeField] private float maxStress = 3000f;
        
        [Tooltip("応力値の更新間隔 (秒)")]
        [SerializeField] private float updateInterval = 0.01f;

        private ScalarBarGraph stressBarGraph;
        private NamakoSolver namakoSolver;
        private float currentStress = 0f;
        private float lastUpdateTime;

        void Start()
        {
            // ScalarBarGraphを自動作成
            stressBarGraph = gameObject.AddComponent<ScalarBarGraph>();
            
            // NamakoSolverを検索
            namakoSolver = FindObjectOfType<NamakoSolver>();
            
            // 初期化
            InitializeStressGraph();
        }

        void Update()
        {
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateStressFromSolver();
                UpdateStressVisualization();
                lastUpdateTime = Time.time;
            }
        }

        void UpdateStressFromSolver()
        {
            if (namakoSolver == null || !namakoSolver.IsFEMStarted) return;

            try
            {
                var meshExtractor = namakoSolver.GetMeshExtractor();
                if (meshExtractor != null && meshExtractor.n_vert_all > 0)
                {
                    float[] stressValues = GetStressValuesFromNative(meshExtractor.n_vert_all);
                    
                    if (stressValues != null && stressValues.Length > 0)
                    {
                        float maxStressValue = 0f;
                        for (int i = 0; i < stressValues.Length; i++)
                        {
                            if (stressValues[i] > maxStressValue)
                            {
                                maxStressValue = stressValues[i];
                            }
                        }
                        currentStress = maxStressValue;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"応力値取得に失敗: {e.Message}");
            }
        }

        private float[] GetStressValuesFromNative(int vertexCount)
        {
            if (vertexCount <= 0) return null;

            try
            {
                System.IntPtr stressPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(vertexCount * sizeof(float));
                NamakoNative.GetVisMeshStress(stressPtr);
                
                float[] stressValues = new float[vertexCount];
                System.Runtime.InteropServices.Marshal.Copy(stressPtr, stressValues, 0, vertexCount);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(stressPtr);
                
                return stressValues;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"応力値の取得エラー: {e.Message}");
                return null;
            }
        }

        void InitializeStressGraph()
        {
            if (stressBarGraph == null) return;

            stressBarGraph.SetRange(0f, maxStress);
            stressBarGraph.SetValue(currentStress);
            UpdateBarColor();
        }

        void UpdateStressVisualization()
        {
            if (stressBarGraph == null) return;

            stressBarGraph.SetValue(currentStress);
            UpdateBarColor();
        }

        void UpdateBarColor()
        {
            if (stressBarGraph == null) return;

            float stressRatio = currentStress / maxStress;
            Color barColor;

            if (stressRatio < 0.5f)
                barColor = Color.green;
            else if (stressRatio < 0.75f)
                barColor = Color.yellow;
            else if (stressRatio < 0.9f)
                barColor = new Color(1f, 0.5f, 0f); // オレンジ
            else
                barColor = Color.red;

            stressBarGraph.SetBarColor(barColor);
        }

        public float CurrentStress => currentStress;
        public float MaxStress => maxStress;
    }
}
