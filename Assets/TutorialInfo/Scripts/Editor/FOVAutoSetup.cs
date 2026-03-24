// FOVAutoSetup.cs
// 🎯 Unity 6 + URP (특히 URP 17.x)용: RenderObjects만 설치하고, 커스텀 FOVOverlayRendererFeature는 추가하지 않음
// ⚠️ Editor 전용

using UnityEngine;                         // Unity 엔진 코어
using UnityEditor;                         // 에디터 API
using UnityEngine.Rendering;               // GraphicsSettings 등
using UnityEngine.Rendering.Universal;     // URP 타입들

public class FOVAutoSetup : EditorWindow
{
    // === 머티리얼 할당 슬롯 ===
    public Material fovWriteMat;        // 시야 메쉬가 스텐실=1을 "찍는" 머티리얼
    public Material obstacleEraseMat;   // 장애물이 스텐실=0으로 "지우는" 머티리얼
    public Material overlayMat;         // (참고) 오버레이 머티리얼 — 여기서는 등록만 받고 사용하지 않음
    public Material depthProxyMat;      // (옵션) 투명/스프라이트용 깊이 프록시 머티리얼

    [MenuItem("Tools/FOV/Setup Wizard")]                 // 메뉴 등록
    public static void ShowWindow()                      // 창 열기
    {
        GetWindow<FOVAutoSetup>("FOV Setup");
    }

    private void OnGUI()                                 // 설치 UI
    {
        GUILayout.Label("FOV 시야 시스템 자동 설치", EditorStyles.boldLabel);

        fovWriteMat      = (Material)EditorGUILayout.ObjectField("FOV Write Mat",      fovWriteMat,      typeof(Material), false);
        obstacleEraseMat = (Material)EditorGUILayout.ObjectField("Obstacle Erase Mat", obstacleEraseMat, typeof(Material), false);
        overlayMat       = (Material)EditorGUILayout.ObjectField("Overlay Mat (참고)", overlayMat,       typeof(Material), false);
        depthProxyMat    = (Material)EditorGUILayout.ObjectField("Depth Proxy Mat (옵션)", depthProxyMat, typeof(Material), false);

        using (new EditorGUI.DisabledScope(fovWriteMat == null || obstacleEraseMat == null))
        {
            if (GUILayout.Button("🚀 원클릭 설치/갱신")) SetupAll();   // 버튼: 설치 실행
        }

        EditorGUILayout.HelpBox(
            "이 버전은 RenderGraph 호환을 위해 '커스텀 오버레이 Feature'를 추가하지 않습니다.\n" +
            "PC_Renderer의 Renderer Features에 'Full Screen Pass Renderer Feature'를 수동으로 추가하고\n" +
            "Pass Material 에 FOV_Overlay, Bind Depth-Stencil 를 체크하세요.",
            MessageType.Info
        );
    }

    private void SetupAll()                               // 전체 설치
    {
        // 1) 레이어 보장
        EnsureLayer("FOVMask");                           // 시야 메쉬 레이어
        EnsureLayer("Obstacle");                          // 장애물 레이어

        // 2) URP Asset / RendererData 찾기
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;   // 현재 URP 에셋
        if (urpAsset == null) { Debug.LogError("❌ URP Asset을 찾지 못했습니다. (Project Settings > Graphics 확인)"); return; }

        ForceEnableDepthTexture(urpAsset);                // 깊이 텍스처 강제 On

        var rendererData = GetDefaultRendererData(urpAsset);  // 기본 Renderer Data(예: PC_Renderer)
        if (rendererData == null) { Debug.LogError("❌ 기본 UniversalRendererData를 찾지 못했습니다. (URP Asset의 Default Renderer 확인)"); return; }

        // 3) Renderer Features 구성 (Render Objects만 추가)
        AddRenderObjectsFeature(rendererData, "FOVMaskPass",  "FOVMask",  fovWriteMat,      RenderPassEvent.AfterRenderingOpaques);        // 스텐실=1
        AddRenderObjectsFeature(rendererData, "ObstaclePass", "Obstacle", obstacleEraseMat, RenderPassEvent.AfterRenderingTransparents);    // 스텐실=0

        // (옵션) 투명/스프라이트 깊이 프록시 — ObstaclePass보다 앞에서 수행
        if (depthProxyMat != null)
            AddRenderObjectsFeature(rendererData, "DepthProxyPass", "Obstacle", depthProxyMat, RenderPassEvent.AfterRenderingOpaques);

        // 4) 씬에 FOV 오브젝트 보장
        EnsureFOVObject(fovWriteMat);                     // FOV 노드 + 컴포넌트/재질 세팅

        // 5) 모든 카메라에서 FOVMask 레이어를 제외
        ExcludeLayerFromAllCameras("FOVMask");            // 비활성 포함 전체 카메라 처리

        AssetDatabase.SaveAssets();                       // 저장
        EditorUtility.SetDirty(rendererData);             // 더티 플래그
        Debug.Log("✅ FOV 시스템 설치/갱신 완료 (RenderObjects만 추가됨 - Overlay는 Full Screen Pass로 수동 추가하세요)");
    }

    // ───────── Helper: 레이어 보장 ─────────
    private void EnsureLayer(string layerName)            // 사용자 레이어 생성/확인
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layersProp = tagManager.FindProperty("layers");

        bool exists = false;
        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var sp = layersProp.GetArrayElementAtIndex(i);
            if (sp.stringValue == layerName) { exists = true; break; }
        }
        if (exists) return;

        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var sp = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"ℹ️ 레이어 생성: {layerName}");
                return;
            }
        }
        Debug.LogWarning($"⚠️ 레이어 생성 실패: '{layerName}' 넣을 빈 슬롯이 없습니다.");
    }

    // ───────── Helper: 기본 RendererData 찾기 ─────────
    private UniversalRendererData GetDefaultRendererData(UniversalRenderPipelineAsset urpAsset) // URP Asset에서 Default RendererData 가져오기
    {
        var so = new SerializedObject(urpAsset);
        var listProp = so.FindProperty("m_RendererDataList");
        var idxProp  = so.FindProperty("m_DefaultRendererIndex");
        if (listProp == null || idxProp == null || listProp.arraySize == 0) return null;

        int idx = Mathf.Clamp(idxProp.intValue, 0, listProp.arraySize - 1);
        var elem = listProp.GetArrayElementAtIndex(idx);
        return elem.objectReferenceValue as UniversalRendererData;
    }

    // ───────── Helper: 깊이 텍스처 보장 ─────────
    private void ForceEnableDepthTexture(UniversalRenderPipelineAsset urpAsset)               // 카메라 깊이 텍스처 사용 On
    {
        try { urpAsset.supportsCameraDepthTexture = true; } catch { /* 버전차 가드 */ }

        var so = new SerializedObject(urpAsset);
        var depthProp = so.FindProperty("m_RequireDepthTexture");   // 일부 버전 내부 필드
        if (depthProp != null) { depthProp.boolValue = true; so.ApplyModifiedProperties(); }
    }

    // ───────── Helper: RenderObjects Feature 추가 ─────────
    private void AddRenderObjectsFeature(UniversalRendererData rendererData, string featureName, string layer, Material mat, RenderPassEvent evt)
    {
        if (mat == null) { Debug.LogWarning($"{featureName}: 머티리얼이 비어 있습니다."); return; }

        // 동일 이름이 이미 있으면 스킵
        foreach (var f in rendererData.rendererFeatures)
            if (f != null && f.name == featureName) return;

        var feature = ScriptableObject.CreateInstance<RenderObjects>();  // Feature 인스턴스
        feature.name = featureName;

        var so = new SerializedObject(feature);
        so.FindProperty("m_PassEvent").enumValueIndex = (int)evt;

        int layerIndex = LayerMask.NameToLayer(layer);
        if (layerIndex < 0) { Debug.LogWarning($"{featureName}: 레이어 '{layer}' 를 찾을 수 없습니다."); layerIndex = 0; }
        so.FindProperty("m_FilterSettings.m_LayerMask").intValue = 1 << layerIndex;

        so.FindProperty("m_OverrideMaterial").objectReferenceValue = mat; // Override Material 지정
        so.ApplyModifiedProperties();

        rendererData.rendererFeatures.Add(feature);
#if UNITY_6000_0_OR_NEWER
        rendererData.SetDirty();                                           // URP 16+ 내부 더티
#endif
        EditorUtility.SetDirty(rendererData);
    }

    // ───────── Helper: FOV 오브젝트 보장 ─────────
    private void EnsureFOVObject(Material fovWrite)                        // FOV 노드 생성/세팅
    {
        var go = GameObject.Find("FOV");
        if (go == null) go = new GameObject("FOV");

        int layer = LayerMask.NameToLayer("FOVMask");
        if (layer >= 0) go.layer = layer;

        var mf = go.GetComponent<MeshFilter>()   ?? go.AddComponent<MeshFilter>();     // MeshFilter 보장
        var mr = go.GetComponent<MeshRenderer>() ?? go.AddComponent<MeshRenderer>();   // MeshRenderer 보장
        if (fovWrite != null) mr.sharedMaterial = fovWrite;                            // FOV-Write 재질 적용

        if (go.GetComponent<FOVMeshGenerator>() == null) go.AddComponent<FOVMeshGenerator>(); // 메쉬 생성기 보장
    }

    // ───────── Helper: 모든 카메라에서 FOVMask 제외 ─────────
    private void ExcludeLayerFromAllCameras(string layerName)               // 카메라 CullingMask에서 특정 레이어 제외
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) return;

#if UNITY_6000_0_OR_NEWER
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 비활성 포함
#else
        var cams = Object.FindObjectsOfType<Camera>(true);
#endif
        foreach (var cam in cams)
        {
            Undo.RecordObject(cam, "Exclude FOVMask Layer");
            cam.cullingMask &= ~(1 << layer);
            EditorUtility.SetDirty(cam);
        }
    }
}
