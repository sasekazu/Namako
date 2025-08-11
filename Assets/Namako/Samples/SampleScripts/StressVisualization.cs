using UnityEngine;
using System.Runtime.InteropServices;

namespace Namako.Samples
{
    /// <summary>
    /// ScalarBarGraphを使用してNamakoTetraMeshの最大主応力値を可視化するサンプル
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
        private NamakoTetraMesh namakoTetraMesh;
        private float currentStress = 0f;
        private float lastUpdateTime;

        void Start()
        {
            // ScalarBarGraphを自動作成
            stressBarGraph = gameObject.AddComponent<ScalarBarGraph>();
            
            // NamakoTetraMeshを検索
            namakoTetraMesh = FindObjectOfType<NamakoTetraMesh>();
            
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
            if (namakoTetraMesh == null) return;

            try
            {
                int tetraCount = namakoTetraMesh.Tets;
                if (tetraCount > 0)
                {
                    float[] stressValues = GetPrincipalStressFromNative(tetraCount);
                    
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
                Debug.LogWarning($"主応力値取得に失敗: {e.Message}");
            }
        }

        private float[] GetPrincipalStressFromNative(int tetraCount)
        {
            if (tetraCount <= 0) return null;

            try
            {
                System.IntPtr stressPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(tetraCount * sizeof(float));
                NamakoNative.GetNodePrincipalStress(stressPtr);
                
                float[] stressValues = new float[tetraCount];
                System.Runtime.InteropServices.Marshal.Copy(stressPtr, stressValues, 0, tetraCount);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(stressPtr);
                
                return stressValues;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"主応力値の取得エラー: {e.Message}");
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
