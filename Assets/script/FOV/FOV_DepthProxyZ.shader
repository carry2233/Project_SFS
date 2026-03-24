// FOV_DepthProxyZ.shader
Shader "Hidden/FOV/DepthProxyZ"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+5" }
        Pass
        {
            Name "DepthProxy"
            Tags { "LightMode"="SRPDefaultUnlit" }

            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS: SV_POSITION; };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
