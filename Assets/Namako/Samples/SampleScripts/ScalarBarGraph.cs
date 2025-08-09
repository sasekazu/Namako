using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Namako.Samples
{
    /// <summary>
    /// シンプルな縦棒グラフ - 値を設定するだけで使用可能
    /// </summary>
    public class ScalarBarGraph : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("現在の値")]
        [SerializeField] private float currentValue = 50f;
        
        [Tooltip("最小値")]
        [SerializeField] private float minValue = 0f;
        
        [Tooltip("最大値")]
        [SerializeField] private float maxValue = 100f;
        
        [Tooltip("バーの色")]
        [SerializeField] private Color barColor = Color.red;
        
        [Tooltip("外枠の色")]
        [SerializeField] private Color borderColor = Color.white;
        
        [Tooltip("外枠の太さ")]
        [SerializeField] private float borderWidth = 2f;
        
        [Header("Scale Lines")]
        [Tooltip("基準線を引く値")]
        [SerializeField] private float[] scaleValues = new float[] { 25f, 50f, 75f };
        
        [Tooltip("基準線の色")]
        [SerializeField] private Color scaleLineColor = Color.gray;
        
        [Tooltip("基準線の太さ")]
        [SerializeField] private float scaleLineWidth = 1f;
        
        [Header("Bar Position")]
        [Tooltip("バーの描画領域の左上")]
        [SerializeField] private Vector2 barTopLeft = new Vector2(0.9f, 0.9f);
        
        [Tooltip("バーの描画領域の右下")]
        [SerializeField] private Vector2 barBottomRight = new Vector2(0.95f, 0.1f);

        private Image barImage;
        private Image borderImage;
        private List<GameObject> scaleLineObjects = new List<GameObject>();

        void Start()
        {
            SetupComponents();
            UpdateGraph();
        }

        void OnValidate()
        {
            if (Application.isPlaying)
            {
                UpdateGraph();
            }
        }

        void OnDestroy()
        {
            ClearScaleLines();
        }

        void SetupComponents()
        {
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(transform, false);
            borderImage = borderObj.AddComponent<Image>();
            borderImage.color = borderColor;
            
            RectTransform borderRect = borderImage.GetComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(barTopLeft.x, barBottomRight.y);
            borderRect.anchorMax = new Vector2(barBottomRight.x, barTopLeft.y);
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;

            GameObject barObj = new GameObject("Bar");
            barObj.transform.SetParent(transform, false);
            barImage = barObj.AddComponent<Image>();
            barImage.color = barColor;
            
            RectTransform barRect = barImage.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(barTopLeft.x, barBottomRight.y);
            barRect.anchorMax = new Vector2(barBottomRight.x, barBottomRight.y);
            barRect.offsetMin = new Vector2(borderWidth, borderWidth);
            barRect.offsetMax = new Vector2(-borderWidth, -borderWidth);
            
            CreateScaleLines();
        }

        /// <summary>
        /// 基準線を作成
        /// </summary>
        void CreateScaleLines()
        {
            ClearScaleLines();
            
            if (scaleValues == null) return;
            
            foreach (float value in scaleValues)
            {
                if (value < minValue || value > maxValue) continue;
                
                float normalizedValue = (value - minValue) / (maxValue - minValue);
                float yPosition = Mathf.Lerp(barBottomRight.y, barTopLeft.y, normalizedValue);
                
                GameObject lineObj = new GameObject($"ScaleLine_{value}");
                lineObj.transform.SetParent(transform, false);
                scaleLineObjects.Add(lineObj);
                
                Image lineImage = lineObj.AddComponent<Image>();
                lineImage.color = scaleLineColor;
                
                RectTransform lineRect = lineImage.GetComponent<RectTransform>();
                lineRect.anchorMin = new Vector2(barTopLeft.x, yPosition);
                lineRect.anchorMax = new Vector2(barBottomRight.x, yPosition);
                lineRect.offsetMin = Vector2.zero;
                lineRect.offsetMax = Vector2.zero;
                lineRect.sizeDelta = new Vector2(0, scaleLineWidth);
                lineRect.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>
        /// 既存の基準線を削除
        /// </summary>
        void ClearScaleLines()
        {
            foreach (GameObject obj in scaleLineObjects)
            {
                if (obj != null)
                {
                    if (Application.isPlaying)
                        Destroy(obj);
                    else
                        DestroyImmediate(obj);
                }
            }
            scaleLineObjects.Clear();
        }

        void UpdateGraph()
        {
            if (barImage == null) return;

            if (borderImage != null)
            {
                RectTransform borderRect = borderImage.GetComponent<RectTransform>();
                borderRect.anchorMin = new Vector2(barTopLeft.x, barBottomRight.y);
                borderRect.anchorMax = new Vector2(barBottomRight.x, barTopLeft.y);
                borderImage.color = borderColor;
            }

            float normalizedValue = Mathf.Clamp01((currentValue - minValue) / (maxValue - minValue));
            RectTransform barRect = barImage.GetComponent<RectTransform>();
            
            float currentHeight = Mathf.Lerp(barBottomRight.y, barTopLeft.y, normalizedValue);
            barRect.anchorMin = new Vector2(barTopLeft.x, barBottomRight.y);
            barRect.anchorMax = new Vector2(barBottomRight.x, currentHeight);
            barRect.offsetMin = new Vector2(borderWidth, borderWidth);
            barRect.offsetMax = new Vector2(-borderWidth, -borderWidth);
            
            barImage.color = barColor;
            
            CreateScaleLines();
        }

        public void SetValue(float value)
        {
            currentValue = value;
            UpdateGraph();
        }

        public void SetRange(float min, float max)
        {
            minValue = min;
            maxValue = max;
            UpdateGraph();
        }

        public void SetBarColor(Color color)
        {
            barColor = color;
            if (barImage != null)
                barImage.color = color;
        }

        public void SetBorderColor(Color color)
        {
            borderColor = color;
            if (borderImage != null)
                borderImage.color = color;
        }

        public void SetBorderWidth(float width)
        {
            borderWidth = width;
            UpdateGraph();
        }

        public void SetBarPosition(Vector2 topLeft, Vector2 bottomRight)
        {
            barTopLeft = topLeft;
            barBottomRight = bottomRight;
            UpdateGraph();
        }

        public void SetScaleValues(float[] values)
        {
            scaleValues = values;
            CreateScaleLines();
        }

        public void SetScaleLineColor(Color color)
        {
            scaleLineColor = color;
            CreateScaleLines();
        }

        public void SetScaleLineWidth(float width)
        {
            scaleLineWidth = width;
            CreateScaleLines();
        }

        // プロパティ
        public float CurrentValue => currentValue;
        public float MinValue => minValue;
        public float MaxValue => maxValue;
        public Vector2 BarTopLeft => barTopLeft;
        public Vector2 BarBottomRight => barBottomRight;
        public Color BorderColor => borderColor;
        public float BorderWidth => borderWidth;
        public float[] ScaleValues => scaleValues;
        public Color ScaleLineColor => scaleLineColor;
        public float ScaleLineWidth => scaleLineWidth;
    }
}
