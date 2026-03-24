// FOVOverlayRendererFeature.cs
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FOVOverlayRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material overlayMaterial; // 오버레이용 머티리얼(아래 셰이더 사용)
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRendering; // 주입 지점(투명 렌더 이후 권장)
        public int materialPass = 0; // 머티리얼 패스 인덱스
    }

    public Settings settings = new Settings(); // 인스펙터 설정 보관

    class FOVOverlayPass : ScriptableRenderPass
    {
        private Material material;           // 오버레이 재질
        private int passIndex;               // 패스 인덱스
        private ProfilingSampler profiler;   // 프로파일러

        public FOVOverlayPass(string name) // 생성자: 프로파일러 준비
        {
            profiler = new ProfilingSampler(name); // 프로파일러 이름 설정
        }

        public void Setup(Material mat, int pass) // 패스에 사용할 자료 설정
        {
            material = mat; // 머티리얼 할당
            passIndex = pass; // 패스 인덱스 보관
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) // 실제 렌더 실행
        {
            if (material == null) return; // 머티리얼 없으면 종료

            var cmd = CommandBufferPool.Get("FOV Overlay"); // 커맨드 버퍼 획득
            using (new ProfilingScope(cmd, profiler))       // 프로파일 범위
            {
#if UNITY_6000_0_OR_NEWER
                // URP 16+ (Unity 6) : 카메라 컬러 타겟 핸들 가져와서 자기 자신에 블릿
                var color = renderingData.cameraData.renderer.cameraColorTargetHandle; // 컬러 타겟
                Blitter.BlitCameraTexture(cmd, color, color, material, passIndex);     // 자기 자신에 풀스크린 드로우
#else
                // 구버전 호환(필요 시)
                var color = renderingData.cameraData.renderer.cameraColorTarget;
                Blit(cmd, color, color, material, passIndex);
#endif
            }
            context.ExecuteCommandBuffer(cmd);  // 커맨드 실행
            CommandBufferPool.Release(cmd);     // 버퍼 반납
        }
    }

    private FOVOverlayPass pass; // 내부 패스 인스턴스

    public override void Create() // RendererFeature 생성 시 호출
    {
        pass = new FOVOverlayPass("FOV Overlay Pass"); // 패스 생성
        pass.renderPassEvent = settings.injectionPoint; // 이벤트 시점 설정
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) // 렌더 파이프에 패스 추가
    {
        if (settings.overlayMaterial == null) return; // 머티리얼 없으면 패스 추가 안함
        pass.Setup(settings.overlayMaterial, settings.materialPass); // 머티리얼/패스 설정
        renderer.EnqueuePass(pass); // 큐에 추가
    }
}
