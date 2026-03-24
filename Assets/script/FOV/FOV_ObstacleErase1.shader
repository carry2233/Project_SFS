// FOV_ObstacleErase.shader  — 장애물로 시야를 지우는 패스
Shader "Hidden/FOV/ObstacleErase"
{
    SubShader
    {
        // 큐는 크게 중요하지 않지만, RenderObjects 패스가 AfterRenderingTransparents에서 돌도록 설정한 상태를 가정
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }

        Pass
        {
            Name "ObstacleErase"
            Tags { "LightMode"="SRPDefaultUnlit" }

            // 화면 색은 쓰지 않음 / 깊이는 덮지 않음(깊이는 기존 불투명 렌더에서 이미 기록됨)
            ColorMask 0
            ZWrite Off
            ZTest LEqual         // 카메라에 보이는(앞에 있는) 장애물 픽셀만 처리
            Cull Off

            // 스텐실 로직:
            //  - 시야 메쉬가 찍어둔 영역(=Ref 1, 즉 스텐실==1)에서만 동작
            //  - 장애물이 가리는 곳을 다시 0으로 덮어써서 Overlay가 먹히게 함
            Stencil
            {
                Ref 1
                Comp Equal        // 스텐실이 1인 곳에서만
                Pass Zero         // 0으로 되돌리기
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                // 색을 쓰지 않지만, 파이프라인 요구상 반환
                return 0;
            }
            ENDHLSL
        }
    }
}
