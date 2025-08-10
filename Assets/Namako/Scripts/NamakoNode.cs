using UnityEngine;

namespace Namako
{
    /// <summary>
    /// ノードの境界条件を設定するコンポーネント
    /// </summary>
    public class NamakoNode : MonoBehaviour
    {
        [Header("Boundary Condition Settings")]
        [Tooltip("このノードを固定するかどうか")]
        public bool isFixed = false;
        
        [Header("Displacement")]
        [Tooltip("ノードの変位量（固定時は強制変位、非固定時は初期変位）")]
        public Vector3 displacement = Vector3.zero;
        
        [Header("Display Settings")]
        [Tooltip("境界条件の可視化を有効にするかどうか")]
        public bool showVisualization = true;
        
        private Renderer nodeRenderer;
        private Material originalMaterial;
        private Material fixedMaterial;
        private Vector3 initialWorldPosition;
        
        void Start()
        {
            // 初期ワールド座標位置を記憶
            initialWorldPosition = transform.position;
            
            nodeRenderer = GetComponent<Renderer>();
            if (nodeRenderer != null)
            {
                originalMaterial = nodeRenderer.material;
                CreateFixedMaterial();
                UpdateVisualization();
            }
        }
        
        void Update()
        {
            // 固定ノードの場合、現在位置と初期位置の差分をdisplacementに格納
            if (isFixed)
            {
                displacement = transform.position - initialWorldPosition;
            }
        }
        
        void OnValidate()
        {
            // インスペクターで値が変更された時に呼ばれる
            UpdateVisualization();
        }
        
        /// <summary>
        /// 固定ノード用のマテリアルを作成
        /// </summary>
        void CreateFixedMaterial()
        {
            if (originalMaterial != null)
            {
                fixedMaterial = new Material(originalMaterial);
                fixedMaterial.color = Color.red; // 固定ノードは赤色で表示
            }
        }
        
        /// <summary>
        /// 境界条件の可視化を更新
        /// </summary>
        void UpdateVisualization()
        {
            if (nodeRenderer == null || !showVisualization)
                return;
                
            if (Application.isPlaying)
            {
                if (isFixed && fixedMaterial != null)
                {
                    nodeRenderer.material = fixedMaterial;
                }
                else if (originalMaterial != null)
                {
                    nodeRenderer.material = originalMaterial;
                }
            }
        }
        
        /// <summary>
        /// 境界条件を設定
        /// </summary>
        /// <param name="fixedFlag">固定フラグ</param>
        /// <param name="disp">変位量</param>
        public void SetBoundaryCondition(bool fixedFlag, Vector3 disp)
        {
            isFixed = fixedFlag;
            displacement = disp;
            UpdateVisualization();
        }
        
        /// <summary>
        /// 現在の境界条件を取得
        /// </summary>
        /// <returns>境界条件の情報</returns>
        public (bool isFixed, Vector3 displacement) GetBoundaryCondition()
        {
            return (isFixed, displacement);
        }
        
        void OnDestroy()
        {
            // メモリリークを防ぐためにマテリアルを破棄
            if (fixedMaterial != null)
            {
                DestroyImmediate(fixedMaterial);
            }
        }
        
        void OnDrawGizmos()
        {
            if (!showVisualization)
                return;
                
            // 固定ノードの場合、ギズモで表示
            if (isFixed)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(transform.position, Vector3.one * 0.01f);
            }
            
            // 変位がある場合、矢印で表示
            if (displacement.magnitude > 0.001f)
            {
                Gizmos.color = isFixed ? Color.yellow : Color.green;
                Vector3 start = transform.position;
                Vector3 end = start + displacement;
                Gizmos.DrawLine(start, end);
                Gizmos.DrawWireSphere(end, 0.002f);
            }
        }
    }
}
