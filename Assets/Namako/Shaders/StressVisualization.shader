Shader "Namako/StressVisualization"
{
    Properties
    {
        _Intensity ("Color Intensity", Range(0,2)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        half _Intensity;

        struct Input
        {
            float4 color : COLOR;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // 頂点カラーを直接使用
            o.Albedo = IN.color.rgb * _Intensity;
            o.Metallic = 0;
            o.Smoothness = 0.3;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
