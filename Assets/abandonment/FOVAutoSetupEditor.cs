// Assets/Editor/FOVAutoSetupEditor.cs
#if UNITY_EDITOR
using System.IO; // 파일/폴더 경로 유틸
using UnityEditor; // 에디터 API
using UnityEngine; // 공용 타입
using UnityEngine.Rendering; // 파이프라인 공통
using UnityEngine.Rendering.Universal; // URP API

public class FOVAutoSetupEditor : EditorWindow
{
    // ====== 사용자 입력 ======
    private string baseFolder = "Assets/script/FOV";                // 생성 기준 폴더
    private string rendererAssetPath = "Assets/Settings/PC_Renderer.asset"; // PC_Renderer 경로
    private string obstacleLayerName = "Obstacle";                  // 장애물 레이어명
    private string maskPropertyName = "_MaskTex";                   // FOV 머티리얼의 마스크 텍스처 프로퍼티명

    private Material fovOverlayMaterial;                            // FOV 오버레이에 쓰는 머티리얼(사용자 지정)
    private UniversalRendererData targetRendererData;               // PC_Renderer (Universal Renderer Data)

    // 카메라 기본 값
    private bool usePerspective = true;                             // 원근/정사영 선택
    private float cameraFOV = 60f;                                  // 원근일 때 시야각
    private float cameraOrthoSize = 10f;                            // 정사영일 때 크기
    private float cameraNear = 0.05f;                               // 근평면
    private float cameraFar = 200f;                                 // 원평면
    private bool parentToSelection = true;                          // 현재 선택 Transform을 부모로 연결

    // 결과물 캐시
    private RenderTexture rtMask;                                   // 생성된 RT(캐시)
    private Material matWhite;                                      // 생성된 흰색 Unlit 머티리얼(캐시)

    [MenuItem("도구/FOV/마스크 세팅 자동 생성")]
    private static void OpenWindow() // 메뉴에서 창 열기
    {
        var win = GetWindow<FOVAutoSetupEditor>("FOV Auto Setup");
        win.minSize = new Vector2(420, 520);
        win.Show();
    }

    private void OnGUI() // 에디터 윈도우 UI
    {
        GUILayout.Label("FOV 마스크 자동 세팅 (URP)", EditorStyles.boldLabel);

        // 기본 경로/렌더러
        EditorGUILayout.Space();
        baseFolder = EditorGUILayout.TextField("생성 폴더", baseFolder);                   // 생성될 에셋 폴더
        rendererAssetPath = EditorGUILayout.TextField("Renderer 경로", rendererAssetPath); // PC_Renderer 자산 경로
        targetRendererData = (UniversalRendererData)EditorGUILayout.ObjectField("PC_Renderer", targetRendererData, typeof(UniversalRendererData), false); // PC_Renderer 직접 드래그 허용

        EditorGUILayout.Space();
        obstacleLayerName = EditorGUILayout.TextField("Obstacle 레이어명", obstacleLayerName); // Obstacle 레이어명
        fovOverlayMaterial = (Material)EditorGUILayout.ObjectField("FOV 오버레이 머티리얼", fovOverlayMaterial, typeof(Material), false); // 마스크를 넣을 FOV용 머티리얼
        maskPropertyName = EditorGUILayout.TextField("마스크 프로퍼티명", maskPropertyName);     // 셰이더의 텍스처 프로퍼티명(_MaskTex 등)

        EditorGUILayout.Space();
        GUILayout.Label("보조 카메라 설정", EditorStyles.boldLabel);
        usePerspective = EditorGUILayout.Toggle("원근 프로젝션", usePerspective); // 원근/정사영
        if (usePerspective)
            cameraFOV = EditorGUILayout.Slider("FOV (deg)", cameraFOV, 1f, 179f); // 시야각
        else
            cameraOrthoSize = EditorGUILayout.FloatField("정사영 Size", cameraOrthoSize); // 정사영 크기

        cameraNear = EditorGUILayout.FloatField("Near", cameraNear); // Near
        cameraFar = EditorGUILayout.FloatField("Far", cameraFar);    // Far
        parentToSelection = EditorGUILayout.Toggle("선택 오브젝트에 자식으로", parentToSelection); // 선택 대상에 자식화

        EditorGUILayout.Space();
        if (GUILayout.Button("실행 / 자동 세팅"))
        {
            RunSetup(); // 메인 실행
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "필수:\n" +
            "1) PC_Renderer(UniversalRendererData)를 지정하거나 경로가 올바른지 확인\n" +
            "2) FOV 오버레이 머티리얼을 지정하고, 마스크 프로퍼티명을 정확히 입력\n" +
            "완료 후 Scene에 'Cam_FOV_Mask'가 생성되고, 'Assets/script/FOV/'에 RT/머티리얼이 만들어집니다.",
            MessageType.Info);
    }

    private void RunSetup() // 전체 자동 세팅 실행
    {
        try
        {
            // 1) 폴더 보장
            EnsureFolder(baseFolder);                                             // 기본 폴더 생성

            // 2) Obstacle 레이어 보장
            EnsureLayer(obstacleLayerName);                                       // 레이어 생성/보장

            // 3) RenderTexture 생성/로드
            rtMask = CreateOrLoadRenderTexture(Path.Combine(baseFolder, "RT_FOV_Mask.renderTexture")); // RT 생성/로드

            // 4) 흰색 Unlit 머티리얼 생성/로드
            matWhite = CreateOrLoadWhiteUnlitMaterial(Path.Combine(baseFolder, "Mat_FOV_MaskWhite.mat")); // Unlit 흰색 머티리얼 생성/로드

            // 5) PC_Renderer 로드/지정
            if (!targetRendererData)
            {
                targetRendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererAssetPath); // 경로로 로드
            }
            if (!targetRendererData)
                throw new System.Exception("PC_Renderer를 찾지 못했습니다. 경로/할당을 확인하세요.");

            // 6) Renderer Feature (Render Objects) 추가/설정
            AddOrConfigureRenderObjectsFeature(targetRendererData, "FOV Mask Pass (Render Objects)", obstacleLayerName, matWhite); // RenderObjects 추가

            // 7) 보조 카메라 생성/설정
            var cam = CreateOrConfigureMaskCamera("Cam_FOV_Mask", rtMask, obstacleLayerName, usePerspective, cameraFOV, cameraOrthoSize, cameraNear, cameraFar); // 카메라 생성/설정

            // 8) 카메라에 PC_Renderer 지정(가능하면)
            TryAssignRendererToCamera(cam, targetRendererData); // 카메라의 Renderer를 PC_Renderer로 설정

            // 9) FOV 오버레이 머티리얼에 마스크 텍스처 연결
            if (!fovOverlayMaterial)
                throw new System.Exception("FOV 오버레이 머티리얼이 지정되지 않았습니다.");
            if (!fovOverlayMaterial.HasProperty(maskPropertyName))
                throw new System.Exception($"머티리얼에 '{maskPropertyName}' 프로퍼티가 없습니다. 셰이더의 텍스처 프로퍼티명을 확인하세요.");

            Undo.RecordObject(fovOverlayMaterial, "Assign RT_FOV_Mask");
            fovOverlayMaterial.SetTexture(maskPropertyName, rtMask); // 마스크 텍스처 연결
            // 권장: 투명/큐 설정(셰이더가 지원할 때만)
            TrySetCommonTransparentFlags(fovOverlayMaterial);       // 투명 플래그 권장치 적용
            EditorUtility.SetDirty(fovOverlayMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("완료", "FOV 마스크 세팅이 완료되었습니다.\nScene에 'Cam_FOV_Mask' 확인, RT 미리보기로 Obstacle=흰/배경=검 확인하세요.", "확인");
        }
        catch (System.Exception ex)
        {
            Debug.LogError(ex);
            EditorUtility.DisplayDialog("오류", ex.Message, "확인");
        }
    }

    private static void EnsureFolder(string folderPath) // 폴더 생성 보장
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            var parent = "Assets";
            var rest = folderPath.Substring("Assets/".Length);
            var parts = rest.Split('/');
            foreach (var p in parts)
            {
                var check = parent + "/" + p;
                if (!AssetDatabase.IsValidFolder(check))
                {
                    AssetDatabase.CreateFolder(parent, p);
                }
                parent = check;
            }
        }
    }

    private static void EnsureLayer(string layerName) // Obstacle 레이어 보장
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]); // TagManager 로드
        var layersProp = tagManager.FindProperty("layers"); // layers 배열
        bool exists = false;
        for (int i = 8; i < layersProp.arraySize; i++) // User 레이어 영역
        {
            var sp = layersProp.GetArrayElementAtIndex(i);
            if (sp != null && sp.stringValue == layerName)
            {
                exists = true; break;
            }
        }
        if (!exists)
        {
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var sp = layersProp.GetArrayElementAtIndex(i);
                if (sp != null && string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName; // 빈 칸에 등록
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }
            throw new System.Exception("사용 가능한 사용자 레이어 슬롯이 없습니다. Project Settings > Tags and Layers에서 여유 슬롯을 확보하세요.");
        }
    }

    private static RenderTexture CreateOrLoadRenderTexture(string assetPath) // RT 생성/로드
    {
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(assetPath);
        if (rt) return rt;

        var desc = new RenderTextureDescriptor(1024, 1024, RenderTextureFormat.R8) // 흑백(R8) 포맷
        {
            sRGB = false,            // sRGB Off
            depthBufferBits = 0,     // Depth 없음
            msaaSamples = 1,         // MSAA Off
            mipCount = 1,            // Mip Off
            useMipMap = false
        };
        var created = new RenderTexture(desc) { name = "RT_FOV_Mask" }; // 이름 지정
        AssetDatabase.CreateAsset(created, assetPath);                   // 에셋 저장
        return created;
    }

    private static Material CreateOrLoadWhiteUnlitMaterial(string assetPath) // Unlit 흰색 머티리얼 생성/로드
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (m) return m;

        var shader = Shader.Find("Universal Render Pipeline/Unlit"); // URP/Unlit 셰이더
        if (!shader) throw new System.Exception("URP/Unlit 셰이더를 찾을 수 없습니다. URP가 활성인지 확인하세요.");

        var mat = new Material(shader) { name = "Mat_FOV_MaskWhite" }; // 머티리얼 생성
        mat.SetColor("_BaseColor", Color.white);                        // 흰색(알파 1)
        AssetDatabase.CreateAsset(mat, assetPath);                      // 에셋 저장
        return mat;
    }

    private static void AddOrConfigureRenderObjectsFeature(UniversalRendererData rendererData, string featureName, string layerName, Material overrideMat) // RenderObjects 추가/설정
    {
        // 기존 동일 이름 Feature가 있는지 검사
        foreach (var f in rendererData.rendererFeatures)
        {
            if (f && f.name == featureName)
            {
                ConfigureRenderObjectsFeatureSerialized(f, layerName, overrideMat); // 설정만 갱신
                EditorUtility.SetDirty(rendererData);
                return;
            }
        }

        // 새 Feature 생성
        var feature = ScriptableObject.CreateInstance<RenderObjects>(); // RenderObjects 인스턴스
        feature.name = featureName;                                     // 인스펙터 표시명
        rendererData.rendererFeatures.Add(feature);                     // 리스트 추가
#if UNITY_6000_0_OR_NEWER
        rendererData.SetDirty();                                        // Unity 6에서 변경 플래그
#else
        EditorUtility.SetDirty(rendererData);
#endif
        AssetDatabase.SaveAssets();

        // 생성 후 직렬화로 내부 필드 설정
        ConfigureRenderObjectsFeatureSerialized(feature, layerName, overrideMat); // 세부 설정
    }

    private static void ConfigureRenderObjectsFeatureSerialized(ScriptableRendererFeature feature, string layerName, Material overrideMat) // SerializedObject로 내부 필드 설정
    {
        var so = new SerializedObject(feature); // 직렬화 객체
        // 공통적으로 존재하는 내부 필드들 접근(버전에 따라 이름이 조금 달 수 있음)
        // m_FilterSettings.m_LayerMask, m_Settings.overrideMaterial, m_Event 등

        // LayerMask 설정
        var filterProp = so.FindProperty("m_FilterSettings");
        if (filterProp != null)
        {
            var layerMaskProp = filterProp.FindPropertyRelative("m_LayerMask");
            if (layerMaskProp != null)
            {
                int layer = LayerMask.NameToLayer(layerName);
                int mask = 1 << layer;
                layerMaskProp.intValue = mask; // Obstacle 레이어만
            }
        }

        // 이벤트(패스 시점) 설정: BeforeRenderingOpaques 권장
        var evtProp = so.FindProperty("m_Event");
        if (evtProp != null)
        {
            evtProp.enumValueIndex = (int)RenderPassEvent.BeforeRenderingOpaques; // 적당한 시점
        }

        // Override Material 설정
        var settingsProp = so.FindProperty("m_Settings");
        if (settingsProp != null)
        {
            var ovMatProp = settingsProp.FindPropertyRelative("overrideMaterial");
            if (ovMatProp != null)
            {
                ovMatProp.objectReferenceValue = overrideMat; // 흰색 Unlit 머티리얼
            }
            var ovPassIdxProp = settingsProp.FindPropertyRelative("overrideMaterialPassIndex");
            if (ovPassIdxProp != null) ovPassIdxProp.intValue = 0; // 기본 패스
        }

        so.ApplyModifiedProperties(); // 변경 적용
        EditorUtility.SetDirty(feature);
        AssetDatabase.SaveAssets();
    }

    private static Camera CreateOrConfigureMaskCamera(string name, RenderTexture targetRT, string layerName, bool perspective, float fov, float orthoSize, float near, float far) // 보조 카메라 생성/설정
    {
        var go = GameObject.Find(name);                    // 기존 검색
        if (!go) go = new GameObject(name);               // 없으면 생성
        Undo.RegisterCreatedObjectUndo(go, "Create Cam_FOV_Mask");

        var cam = go.GetComponent<Camera>();
        if (!cam) cam = go.AddComponent<Camera>();        // 카메라 보장

        // 기본 트랜스폼(선택 오브젝트를 부모로 하는 것은 호출측에서 처리)
        // 카메라 설정
        cam.clearFlags = CameraClearFlags.SolidColor;     // 배경 단색
        cam.backgroundColor = Color.black;                // 배경 검정
        cam.targetTexture = targetRT;                     // 출력 RT
        cam.nearClipPlane = near;                         // 근평면
        cam.farClipPlane = far;                           // 원평면

        if (perspective)
        {
            cam.orthographic = false;                     // 원근
            cam.fieldOfView = fov;                        // FOV
        }
        else
        {
            cam.orthographic = true;                      // 정사영
            cam.orthographicSize = orthoSize;             // Size
        }

        // CullingMask: Obstacle만
        int layer = LayerMask.NameToLayer(layerName);
        cam.cullingMask = 1 << layer;                     // 해당 레이어만 렌더

        return cam;
    }

    private void TryAssignRendererToCamera(Camera cam, UniversalRendererData rendererData) // 카메라에 특정 RendererData 지정
    {
        var add = cam.GetComponent<UniversalAdditionalCameraData>(); // URP 카메라 추가 데이터
        if (!add) add = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();

        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; // 현재 URP 에셋
        if (!urpAsset) return;

        int rendererIndex = FindRendererIndex(urpAsset, rendererData); // 인덱스 찾기
        if (rendererIndex >= 0)
        {
            add.SetRenderer(rendererIndex); // 카메라에 Renderer 인덱스 지정
        }

        // 선택 오브젝트에 자식화 옵션
        if (parentToSelection && Selection.activeTransform)
        {
            cam.transform.SetParent(Selection.activeTransform, false); // 선택 Transform의 자식으로
            cam.transform.localPosition = Vector3.zero;               // 위치/회전 초기화
            cam.transform.localRotation = Quaternion.identity;
        }
    }

    private static int FindRendererIndex(UniversalRenderPipelineAsset asset, UniversalRendererData target) // URP 에셋에서 RendererData 인덱스 찾기
    {
        var so = new SerializedObject(asset);                                 // URP 에셋 직렬화
        var listProp = so.FindProperty("m_RendererDataList");                 // RendererData 배열
        if (listProp != null && listProp.isArray)
        {
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var elem = listProp.GetArrayElementAtIndex(i);
                if (elem != null && elem.objectReferenceValue == target)
                    return i; // 일치 인덱스
            }
        }
        // 기본 렌더러가 target인지도 확인
        var defaultProp = so.FindProperty("m_DefaultRendererIndex");
        if (defaultProp != null && defaultProp.intValue >= 0)
        {
            var idx = defaultProp.intValue;
            if (listProp != null && idx < listProp.arraySize)
            {
                var elem = listProp.GetArrayElementAtIndex(idx);
                if (elem != null && elem.objectReferenceValue == target)
                    return idx;
            }
        }
        return -1; // 못 찾음
    }

    private static void TrySetCommonTransparentFlags(Material mat) // 투명 렌더 권장값 적용(셰이더가 지원 시)
    {
        // 셰이더별 속성명이 다를 수 있으므로 존재할 때만 설정
        // 대표적으로 _Surface(0=Opaque,1=Transparent), _ZWrite, _QueueOffset 등

        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);   // ZWrite Off

        // Render Queue: 투명 3000 근처
        if (mat.renderQueue < 3000) mat.renderQueue = 3000;            // Queue 올리기
    }
}
#endif
