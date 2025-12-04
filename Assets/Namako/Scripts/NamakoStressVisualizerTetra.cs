using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace Namako
{
    /// <summary>
    /// FEMシミュレーションの応力可視化を行うコンポーネント（Tetramesh版）
    /// NamakoSolverと同じGameObjectにアタッチして使用する
    /// </summary>
    [RequireComponent(typeof(NamakoSolver))]
    public class NamakoStressVisualizerTetra : MonoBehaviour
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
        private IntPtr tetramesh_stress_cpp;
        private float[] tetramesh_stress;
        private bool isInitialized = false;
        private Material[] originalMaterials; // 元のマテリアルを保存

        void Start()
        {
            // 同じGameObjectのNamakoSolverを取得
            namakoSolver = GetComponent<NamakoSolver>();
            
            if (namakoSolver == null)
            {
                Debug.LogError("StressVisualizerTetra: NamakoSolver component not found on the same GameObject. Please attach StressVisualizerTetra to the same GameObject as NamakoSolver.");
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

            // NamakoSolverからNamakoTetraMeshを取得
            var tetraMesh = namakoSolver.tetraMeshGameObject?.GetComponent<NamakoTetraMesh>();
            if (tetraMesh != null)
            {
                // 四面体数を取得
                int tetraCount = tetraMesh.Tets;
                
                // 応力データ用のメモリを確保（四面体数分）
                tetramesh_stress_cpp = Marshal.AllocHGlobal(tetraCount * sizeof(float));
                tetramesh_stress = new float[tetraCount];
                
                isInitialized = true;
                Debug.Log($"StressVisualizerTetra initialized: {tetraCount} tetrahedra");
                
                // 自動的にシェーダーを変更
                if (autoChangeShader)
                {
                    ChangeToVertexColorShader();
                }
            }
            else
            {
                Debug.LogWarning("StressVisualizerTetra: No NamakoTetraMesh found in NamakoSolver");
            }
        }

        /// <summary>
        /// 頂点カラー対応シェーダーに変更
        /// </summary>
        [ContextMenu("Change to Vertex Color Shader")]
        public void ChangeToVertexColorShader()
        {
            var tetraMesh = namakoSolver.tetraMeshGameObject?.GetComponent<NamakoTetraMesh>();
            if (tetraMesh == null) return;

            // tetrasオブジェクトを探す
            Transform tetrasTransform = tetraMesh.transform.Find("tetras");
            if (tetrasTransform == null) return;

            GameObject tetrasObj = tetrasTransform.gameObject;
            var renderer = tetrasObj.GetComponent<Renderer>();
            if (renderer == null) return;

            List<Material> originals = new List<Material>();
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
            originalMaterials = originals.ToArray();
        }

        /// <summary>
        /// 元のマテリアルに戻す
        /// </summary>
        [ContextMenu("Restore Original Materials")]
        public void RestoreOriginalMaterials()
        {
            if (originalMaterials == null) return;

            var tetraMesh = namakoSolver.tetraMeshGameObject?.GetComponent<NamakoTetraMesh>();
            if (tetraMesh == null) return;

            // tetrasオブジェクトを探す
            Transform tetrasTransform = tetraMesh.transform.Find("tetras");
            if (tetrasTransform == null) return;

            GameObject tetrasObj = tetrasTransform.gameObject;
            var renderer = tetrasObj.GetComponent<Renderer>();
            if (renderer == null) return;

            Material[] restoredMaterials = new Material[renderer.materials.Length];
            for (int i = 0; i < renderer.materials.Length && i < originalMaterials.Length; i++)
            {
                restoredMaterials[i] = originalMaterials[i];
            }
            renderer.materials = restoredMaterials;
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
            var tetraMesh = namakoSolver.tetraMeshGameObject?.GetComponent<NamakoTetraMesh>();
            if (tetraMesh == null) return;

            try
            {
                // ネイティブライブラリから四面体主応力データを取得
                NamakoNative.GetNodePrincipalStress(tetramesh_stress_cpp);
                Marshal.Copy(tetramesh_stress_cpp, tetramesh_stress, 0, tetraMesh.Tets);
                
                // 四面体の応力からノードの応力を計算
                float[] nodeStressValues = CalculateNodeStressFromTetraStress(tetraMesh, tetramesh_stress);
                
                // テトラメッシュオブジェクトの頂点カラーを更新
                UpdateTetraMeshVertexColors(tetraMesh, nodeStressValues);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"StressVisualizerTetra: Error updating stress visualization: {e.Message}");
            }
        }

        /// <summary>
        /// 四面体の応力値からノードの応力値を計算（周囲の四面体の平均）
        /// </summary>
        /// <param name="tetraMesh">四面体メッシュ</param>
        /// <param name="tetraStressValues">四面体の応力値配列</param>
        /// <returns>ノードの応力値配列</returns>
        private float[] CalculateNodeStressFromTetraStress(NamakoTetraMesh tetraMesh, float[] tetraStressValues)
        {
            int nodeCount = tetraMesh.Nodes;
            int tetraCount = tetraMesh.Tets;
            int[] tetIndices = tetraMesh.Tet;
            
            float[] nodeStressSums = new float[nodeCount];
            int[] nodeTetraCount = new int[nodeCount];
            
            // 各四面体について、その4つのノードに応力値を加算
            for (int t = 0; t < tetraCount; t++)
            {
                float tetraStress = tetraStressValues[t];
                
                for (int i = 0; i < 4; i++)
                {
                    int nodeIndex = tetIndices[4 * t + i];
                    if (nodeIndex >= 0 && nodeIndex < nodeCount)
                    {
                        nodeStressSums[nodeIndex] += tetraStress;
                        nodeTetraCount[nodeIndex]++;
                    }
                }
            }
            
            // 各ノードの応力値を平均化
            float[] nodeStressValues = new float[nodeCount];
            for (int n = 0; n < nodeCount; n++)
            {
                if (nodeTetraCount[n] > 0)
                {
                    nodeStressValues[n] = nodeStressSums[n] / nodeTetraCount[n];
                }
                else
                {
                    nodeStressValues[n] = 0.0f;
                }
            }
            
            return nodeStressValues;
        }

        private void UpdateTetraMeshVertexColors(NamakoTetraMesh tetraMesh, float[] stressValues)
        {
            // tetrasオブジェクトを探す
            Transform tetrasTransform = tetraMesh.transform.Find("tetras");
            if (tetrasTransform == null) return;

            GameObject tetrasObj = tetrasTransform.gameObject;
            MeshFilter meshFilter = tetrasObj.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.mesh == null) return;

            Mesh mesh = meshFilter.mesh;
            
            // 四面体の頂点数を計算（12頂点 × 四面体数）
            int tetraCount = tetraMesh.Tets;
            int vertexCount = 12 * tetraCount;
            
            Color[] colors = new Color[vertexCount];
            
            // 各四面体について頂点カラーを設定
            for (int t = 0; t < tetraCount; t++)
            {
                int[] tetIndices = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    tetIndices[i] = tetraMesh.Tet[4 * t + i];
                }
                
                // 四面体の4つのノードの応力値から色を計算
                Color[] tetColors = new Color[4];
                for (int i = 0; i < 4; i++)
                {
                    float stress = stressValues[tetIndices[i]];
                    tetColors[i] = StressToColor(stress, stressLowerLimit, stressUpperLimit);
                }
                
                // 12頂点の色を正しいノードインデックスに基づいて設定
                // 四面体の頂点配置パターンに従って色を適用
                // 面1: v0, v2, v1 (vertices 0-2)
                colors[12 * t + 0] = tetColors[0]; // v0
                colors[12 * t + 1] = tetColors[2]; // v2
                colors[12 * t + 2] = tetColors[1]; // v1
                
                // 面2: v0, v3, v2 (vertices 3-5)
                colors[12 * t + 3] = tetColors[0]; // v0
                colors[12 * t + 4] = tetColors[3]; // v3
                colors[12 * t + 5] = tetColors[2]; // v2
                
                // 面3: v0, v1, v3 (vertices 6-8)
                colors[12 * t + 6] = tetColors[0]; // v0
                colors[12 * t + 7] = tetColors[1]; // v1
                colors[12 * t + 8] = tetColors[3]; // v3
                
                // 面4: v1, v2, v3 (vertices 9-11)
                colors[12 * t + 9] = tetColors[1]; // v1
                colors[12 * t + 10] = tetColors[2]; // v2
                colors[12 * t + 11] = tetColors[3]; // v3
            }
            
            mesh.colors = colors;
        }

        private Color StressToColor(float stress, float minStress, float maxStress)
        {
            // 応力値を0-1の範囲に正規化
            float t = Mathf.Clamp01((stress - minStress) / (maxStress - minStress));
            
            // 青→緑→赤のカラーマッピング（NamakoStressVisualizerSurfaceと同じ）
            if (t < 0.5f)
                return Color.Lerp(Color.blue, Color.green, t * 2.0f);
            else
                return Color.Lerp(Color.green, Color.red, (t - 0.5f) * 2.0f);
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
            if (isInitialized && tetramesh_stress_cpp != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(tetramesh_stress_cpp);
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
