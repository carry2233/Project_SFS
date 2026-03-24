// FOV_Overlay.shader  — include 불필요 버전 (URP 17/Unity 6 호환)
Shader "Hidden/FOV/Overlay"
{
    Properties
    {
        _OverlayColor ("Overlay Color (RGBA)", Color) = (0,0,0,0.6)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+500" }

        Pass
        {
            Name "Overlay"
            Tags { "LightMode"="SRPDefaultUnlit" }

            ZWrite Off
            ZTest  Always
            Blend  SrcAlpha OneMinusSrcAlpha
            Cull   Off
            ColorMask RGBA

            // 시야(Stencil=1) 영역은 덮지 않음 -> 바깥/장애물만 덮기
            Stencil { Ref 1  Comp NotEqual  Pass Keep }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            float4 _OverlayColor;

            // 풀스크린 삼각형을 직접 생성(SV_VertexID 이용)
            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;

                // 세 점(-1,1), (-1,-3), (3,1) — 화면 전체를 덮는 큰 삼각형
                float2 pos;
                if (IN.vertexID == 0)      pos = float2(-1.0,  1.0);
                else if (IN.vertexID == 1) pos = float2(-1.0, -3.0);
                else                       pos = float2( 3.0,  1.0);

                OUT.positionHCS = float4(pos, 0.0, 1.0);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                return _OverlayColor;
            }
            ENDHLSL
        }
    }
}
