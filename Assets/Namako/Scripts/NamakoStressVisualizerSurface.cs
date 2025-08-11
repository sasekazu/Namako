using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace Namako
{
    /// <summary>
    /// FEMシミュレーションの応力可視化を行うコンポーネント（サーフェス用）
    /// NamakoSolverと同じGameObjectにアタッチして使用する
    /// </summary>
    [RequireComponent(typeof(NamakoSolver))]
    public class NamakoStressVisualizerSurface : MonoBehaviour
    {
        [Header("Stress Visualization Settings")]
        [Tooltip("Enable stress visualization")]
        public bool enableVisualization = false;
        
        [Tooltip("Lower limit for stress display")]
        public float stressLowerLimit = 0.0f;
        
        [Tooltip("Upper limit for stress display")]
        public float stressUpperLimit = 200.0f;

        [Tooltip("Automatically change to vertex color compatible shader")]
        public bool autoChangeShader = true;

        private NamakoSolver namakoSolver;
        private IntPtr vmesh_stress_cpp;
        private float[] vmesh_stress;
        private bool isInitialized = false;
        private Material[] originalMaterials; // 元のマテリアルを保存

        void Start()
        {
            // 同じGameObjectのNamakoSolverを取得
            namakoSolver = GetComponent<NamakoSolver>();
            
            if (namakoSolver == null)
            {
                Debug.LogError("StressVisualizer: NamakoSolver component not found on the same GameObject. Please attach StressVisualizer to the same GameObject as NamakoSolver.");
                enableVisualization = false;
                return;
            }

            // NamakoSolverの初期化を待つ
            StartCoroutine(WaitForNamakoSolverInitialization());
        }

        private IEnumerator WaitForNamakoSolverInitialization()
        {
            // NamakoSolverが初期化されるまで待機
            while (!namakoSolver.IsInitialized)
            {
                yield return null;
            }

            Initialize();
        }

        private void Initialize()
        {
            if (isInitialized) return;

            // NamakoSolverから必要な情報を取得
            var extractor = namakoSolver.GetMeshExtractor();
            if (extractor != null)
            {
                // 応力データ用のメモリを確保
                vmesh_stress_cpp = Marshal.AllocHGlobal(extractor.n_vert_all * sizeof(float));
                vmesh_stress = new float[extractor.n_vert_all];
                
                isInitialized = true;
                Debug.Log($"StressVisualizer initialized: {extractor.n_vert_all} vertices");
                
                // 自動的にシェーダーを変更
                if (autoChangeShader)
                {
                    ChangeToVertexColorShader();
                }
            }
            else
            {
                Debug.LogWarning("StressVisualizer: No visual object found in NamakoSolver");
            }
        }

        /// <summary>
        /// 頂点カラー対応シェーダーに変更
        /// </summary>
        [ContextMenu("Change to Vertex Color Shader")]
        public void ChangeToVertexColorShader()
        {
            var visualObj = namakoSolver.visualObj;
            if (visualObj == null) return;

            var renderers = visualObj.GetComponentsInChildren<Renderer>();
            List<Material> originals = new List<Material>();

            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.materials)
                {
                    originals.Add(material);
                }

                // 新しいマテリアルを作成してシェーダーを変更
                Material[] newMaterials = new Material[renderer.materials.Length];
                for (int i = 0; i < renderer.materials.Length; i++)
                {
                    Material newMat = new Material(renderer.materials[i]);
                    
                    // 応力可視化専用シェーダーを使用
                    Shader stressShader = Shader.Find("Namako/StressVisualization");
                    if (stressShader != null)
                    {
                        newMat.shader = stressShader;
                    }
                    else
                    {
                        Debug.LogWarning("Could not find Namako/StressVisualization shader");
                    }
                    
                    newMaterials[i] = newMat;
                }
                
                renderer.materials = newMaterials;
            }

            originalMaterials = originals.ToArray();
        }

        /// <summary>
        /// 元のマテリアルに戻す
        /// </summary>
        [ContextMenu("Restore Original Materials")]
        public void RestoreOriginalMaterials()
        {
            if (originalMaterials == null) return;

            var visualObj = namakoSolver.visualObj;
            if (visualObj == null) return;

            var renderers = visualObj.GetComponentsInChildren<Renderer>();
            int materialIndex = 0;

            foreach (var renderer in renderers)
            {
                Material[] restoredMaterials = new Material[renderer.materials.Length];
                for (int i = 0; i < renderer.materials.Length && materialIndex < originalMaterials.Length; i++)
                {
                    restoredMaterials[i] = originalMaterials[materialIndex];
                    materialIndex++;
                }
                renderer.materials = restoredMaterials;
            }
        }

        void Update()
        {
            if (!enableVisualization || !isInitialized || !namakoSolver.IsFEMStarted)
            {
                return;
            }

            UpdateStressVisualization();
        }

        private void UpdateStressVisualization()
        {
            var extractor = namakoSolver.GetMeshExtractor();
            if (extractor == null) return;

            try
            {
                // ネイティブライブラリから応力データを取得
                NamakoNative.GetVisMeshStress(vmesh_stress_cpp);
                Marshal.Copy(vmesh_stress_cpp, vmesh_stress, 0, extractor.n_vert_all);
                
                // メッシュの頂点カラーを更新
                extractor.UpdateVertexColor(vmesh_stress, stressLowerLimit, stressUpperLimit);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"StressVisualizer: Error updating stress visualization: {e.Message}");
            }
        }

        /// <summary>
        /// 応力可視化の有効/無効を切り替え
        /// </summary>
        /// <param name="enable">有効にするかどうか</param>
        public void SetVisualizationEnabled(bool enable)
        {
            enableVisualization = enable;
        }

        /// <summary>
        /// 応力表示範囲を設定
        /// </summary>
        /// <param name="lowerLimit">下限値</param>
        /// <param name="upperLimit">上限値</param>
        public void SetStressRange(float lowerLimit, float upperLimit)
        {
            stressLowerLimit = lowerLimit;
            stressUpperLimit = upperLimit;
        }

        private void OnDestroy()
        {
            if (isInitialized && vmesh_stress_cpp != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(vmesh_stress_cpp);
            }
            
            // 元のマテリアルを復元
            if (originalMaterials != null)
            {
                RestoreOriginalMaterials();
            }
        }

        private void OnValidate()
        {
            // インスペクターで値が変更された時の処理
            if (stressLowerLimit < 0)
                stressLowerLimit = 0;
            
            if (stressUpperLimit < stressLowerLimit)
                stressUpperLimit = stressLowerLimit + 1;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (enableVisualization && isInitialized)
            {
                // デバッグ情報をScene viewに表示
                UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, 
                    $"Stress Range: {stressLowerLimit:F1} - {stressUpperLimit:F1}");
            }
        }
#endif
    }
}
