using UnityEngine;

[ExecuteAlways]
public class PlaneMeshCarveDeformer : MonoBehaviour
{
    public enum PlaneAxes // 평면 기준 축 선택(어떤 평면으로 판정할지)
    {
        XZ, // Unity 기본 Plane (X,Z) 평면, 법선 Y
        XY, // (X,Y) 평면, 법선 Z
        YZ  // (Y,Z) 평면, 법선 X
    }

    [Header("대상")]
    public MeshFilter meshFilter; // 변형/생성에 사용할 MeshFilter

    [Header("자동 Grid Mesh 생성")] // ✅ 추가: Plane 대신 고해상도 격자 메쉬 생성
    public bool generateGridMesh = true; // ✅ true면 스크립트가 Grid Mesh를 직접 생성해서 사용
    public int gridResolutionU = 100; // ✅ U방향 분할 수(가로) (클수록 촘촘)
    public int gridResolutionV = 100; // ✅ V방향 분할 수(세로) (클수록 촘촘)
    public float gridSizeU = 10f; // ✅ U방향 실제 길이(월드가 아니라 로컬 스케일 기준)
    public float gridSizeV = 10f; // ✅ V방향 실제 길이
    public bool gridCenterPivot = true; // ✅ true면 (0,0,0)이 중심이 되도록 생성

    [Header("평면 판정")]
    public PlaneAxes planeAxes = PlaneAxes.XZ; // 메쉬에서 어떤 축을 평면으로 볼지
    public bool useMeshNormalDirection = false; // true면 정점 노멀 방향으로 파임(노멀 불안정 시 권장X)

    [Header("원 파임")]
    public bool enableCircle = true; // 원 파임 사용 여부
    public float circleDiameter = 1.0f; // 원 지름
    public float circleDepth = 0.2f; // 원 파임 깊이
    public AnimationCurve circleFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0); // 원 파임 감쇠(중심=1, 경계=0)

    [Header("부채꼴 파임")]
    public bool enableSector = true; // 부채꼴 파임 사용 여부
    public float sectorRadius = 1.0f; // 부채꼴 반지름
    [Range(0f, 360f)]
    public float sectorAngle = 90f; // 부채꼴 각도(예: 90)
    [Range(-180f, 180f)]
    public float sectorDirectionAngle = 0f; // 부채꼴 중심 방향(도) (0=+U방향, 90=+V방향)
    public float sectorDepth = 0.2f; // 부채꼴 파임 깊이
    public AnimationCurve sectorFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0); // 부채꼴 파임 감쇠(중심=1, 경계=0)

    [Header("적용")]
    public bool applyInPlayMode = true; // 플레이 중 적용 여부
    public bool applyInEditMode = true; // 에디터에서 실시간 반영 여부
    public bool recalcNormals = true; // 변형 후 노멀 재계산 여부

    // 내부 상태
    private Mesh _runtimeMesh; // 런타임/에디터에서 수정할 메쉬 인스턴스(또는 생성 메쉬)
    private Vector3[] _baseVertices; // 원본 정점(로컬)
    private Vector3[] _deformedVertices; // 변형 정점(로컬)
    private Vector3[] _baseNormals; // 원본 노멀(로컬)
    private bool _dirty; // 변형 재계산 필요 플래그
    private int _cachedResU; // ✅ 생성된 Grid의 U 분할 캐시
    private int _cachedResV; // ✅ 생성된 Grid의 V 분할 캐시
    private float _cachedSizeU; // ✅ 생성된 Grid의 U 크기 캐시
    private float _cachedSizeV; // ✅ 생성된 Grid의 V 크기 캐시
    private PlaneAxes _cachedAxes; // ✅ 생성된 Grid의 평면 축 캐시
    private bool _cachedCenterPivot; // ✅ 생성된 Grid의 중심 피벗 캐시

    private void Reset() // 컴포넌트 초기 연결 자동 시도
    {
        meshFilter = GetComponent<MeshFilter>(); // MeshFilter 자동 할당 시도
        MarkDirty(); // 초기 상태에서 변형 갱신 예약
    }

    private void OnEnable() // 활성화 시 메쉬 준비
    {
        PrepareMesh(); // ✅ 메쉬 준비(자동 생성 또는 복제)
        MarkDirty(); // 변형 갱신 예약
    }

    private void OnValidate() // 인스펙터 값 변경 시
    {
        // 값 안전 보정
        circleDiameter = Mathf.Max(0f, circleDiameter); // 지름 음수 방지
        circleDepth = Mathf.Max(0f, circleDepth); // 깊이 음수 방지
        sectorRadius = Mathf.Max(0f, sectorRadius); // 반지름 음수 방지
        sectorDepth = Mathf.Max(0f, sectorDepth); // 깊이 음수 방지

        gridResolutionU = Mathf.Max(1, gridResolutionU); // ✅ 분할 최소 1
        gridResolutionV = Mathf.Max(1, gridResolutionV); // ✅ 분할 최소 1
        gridSizeU = Mathf.Max(0.001f, gridSizeU); // ✅ 크기 최소값
        gridSizeV = Mathf.Max(0.001f, gridSizeV); // ✅ 크기 최소값

        PrepareMesh(); // ✅ 값 변경 시 메쉬 재준비(필요하면 재생성)
        MarkDirty(); // 변형 갱신 예약
    }

    private void Update() // 프레임 갱신
    {
        if (!ShouldApplyNow()) return; // 현재 모드에서 적용 가능한지 확인

        // ✅ 에디터/플레이 중에도 Grid 파라미터가 바뀌면 즉시 재생성되도록
        if (generateGridMesh && IsGridSettingChanged()) // ✅ Grid 설정 변경 감지
        {
            PrepareMesh(); // ✅ Grid 메쉬 재생성
            MarkDirty(); // 변형 갱신 예약
        }

        if (!_dirty) return; // 변경 없으면 스킵

        ApplyDeformation(); // 실제 변형 적용
        _dirty = false; // 갱신 완료
    }

    public void MarkDirty() // 외부/내부에서 재계산 요청용
    {
        _dirty = true; // 재계산 필요 표시
    }

    private bool ShouldApplyNow() // 적용 조건 체크
    {
        if (Application.isPlaying) return applyInPlayMode; // 플레이 중이면 설정 확인
        return applyInEditMode; // 에디터면 설정 확인
    }

    private void PrepareMesh() // ✅ 메쉬 준비(자동 생성 또는 복제)
    {
        if (meshFilter == null) return; // 대상이 없으면 중단

        if (generateGridMesh) // ✅ Grid Mesh를 직접 생성해서 사용
        {
            CreateOrReplaceGridMesh(); // ✅ Grid 생성/교체
            CacheMeshArrays(); // ✅ 정점/노멀 캐싱
            CacheGridSettings(); // ✅ Grid 설정 캐싱
        }
        else // 기존 방식: 공유 메쉬 복제 후 변형
        {
            PrepareRuntimeMeshFromShared(); // 공유 메쉬 복제
        }
    }

    private void PrepareRuntimeMeshFromShared() // 공유 메쉬를 복제해 인스턴스로 만드는 기존 방식
    {
        Mesh shared = meshFilter.sharedMesh; // 현재 공유 메쉬
        if (shared == null) return; // 메쉬가 없으면 중단

        if (_runtimeMesh != null && meshFilter.sharedMesh == _runtimeMesh && _baseVertices != null) return; // 이미 준비됨

        _runtimeMesh = Instantiate(shared); // 메쉬 복제
        _runtimeMesh.name = shared.name + "_RuntimeDeformed"; // 이름 구분
        meshFilter.sharedMesh = _runtimeMesh; // MeshFilter에 인스턴스 적용

        CacheMeshArrays(); // ✅ 정점/노멀 캐싱
    }

    private void CacheMeshArrays() // ✅ 메쉬 배열 캐싱(정점/노멀/변형버퍼)
    {
        if (meshFilter == null) return; // 안전 체크
        if (meshFilter.sharedMesh == null) return; // 안전 체크

        _runtimeMesh = meshFilter.sharedMesh; // 현재 사용 메쉬로 갱신
        _baseVertices = _runtimeMesh.vertices; // 원본 정점 저장
        _deformedVertices = new Vector3[_baseVertices.Length]; // 변형 배열 준비
        _baseNormals = _runtimeMesh.normals; // 원본 노멀 저장
    }

    private bool IsGridSettingChanged() // ✅ Grid 설정 변경 감지
    {
        if (_runtimeMesh == null) return true; // 메쉬가 없으면 재생성 필요

        if (_cachedResU != gridResolutionU) return true; // 분할 U 변경
        if (_cachedResV != gridResolutionV) return true; // 분할 V 변경
        if (!Mathf.Approximately(_cachedSizeU, gridSizeU)) return true; // 크기 U 변경
        if (!Mathf.Approximately(_cachedSizeV, gridSizeV)) return true; // 크기 V 변경
        if (_cachedAxes != planeAxes) return true; // 평면 축 변경
        if (_cachedCenterPivot != gridCenterPivot) return true; // 중심 피벗 변경

        return false; // 변경 없음
    }

    private void CacheGridSettings() // ✅ 현재 Grid 설정을 캐시에 저장
    {
        _cachedResU = gridResolutionU; // U 분할 캐싱
        _cachedResV = gridResolutionV; // V 분할 캐싱
        _cachedSizeU = gridSizeU; // U 크기 캐싱
        _cachedSizeV = gridSizeV; // V 크기 캐싱
        _cachedAxes = planeAxes; // 축 캐싱
        _cachedCenterPivot = gridCenterPivot; // 중심 피벗 캐싱
    }

    private void CreateOrReplaceGridMesh() // ✅ Grid Mesh 생성/교체
    {
        // 새 메쉬 생성
        Mesh gridMesh = BuildGridMesh(gridResolutionU, gridResolutionV, gridSizeU, gridSizeV, planeAxes, gridCenterPivot); // ✅ Grid 생성

        // 기존 runtimeMesh를 직접 파괴할 필요는 없음(에디터/플레이 안정성)
        // MeshFilter에 새 메쉬를 바로 할당
        meshFilter.sharedMesh = gridMesh; // ✅ 생성 메쉬 적용
    }

    private Mesh BuildGridMesh(int resU, int resV, float sizeU, float sizeV, PlaneAxes axes, bool centerPivot) // ✅ 격자 메쉬 생성
    {
        int vertU = resU + 1; // 정점 U 개수
        int vertV = resV + 1; // 정점 V 개수
        int vertexCount = vertU * vertV; // 총 정점 수

        Vector3[] vertices = new Vector3[vertexCount]; // 정점 배열
        Vector2[] uvs = new Vector2[vertexCount]; // UV 배열
        int[] triangles = new int[resU * resV * 6]; // 삼각형 인덱스 배열(쿼드 1칸=삼각형2개=인덱스6개)

        float startU = centerPivot ? -sizeU * 0.5f : 0f; // 시작 U 위치
        float startV = centerPivot ? -sizeV * 0.5f : 0f; // 시작 V 위치

        float stepU = sizeU / resU; // U 한 칸 길이
        float stepV = sizeV / resV; // V 한 칸 길이

        // 축 결정(로컬 기준)
        GetAxes(axes, out Vector3 axisU, out Vector3 axisV, out Vector3 axisN); // 축 벡터 획득

        // 정점/UV 생성
        int index = 0; // 정점 인덱스
        for (int v = 0; v < vertV; v++) // V 방향 정점 반복
        {
            for (int u = 0; u < vertU; u++) // U 방향 정점 반복
            {
                float posU = startU + (u * stepU); // U 위치
                float posV = startV + (v * stepV); // V 위치

                vertices[index] = (axisU * posU) + (axisV * posV); // 로컬 정점 위치(선택 축 평면에 배치)
                uvs[index] = new Vector2((float)u / resU, (float)v / resV); // UV(0~1)
                index++; // 다음 정점
            }
        }

        // 삼각형 인덱스 생성(정점 배열은 (u + v*vertU))
        int t = 0; // triangles 인덱스
        for (int v = 0; v < resV; v++) // 칸 V 반복
        {
            for (int u = 0; u < resU; u++) // 칸 U 반복
            {
                int i0 = (u) + (v * vertU); // 좌하
                int i1 = (u + 1) + (v * vertU); // 우하
                int i2 = (u) + ((v + 1) * vertU); // 좌상
                int i3 = (u + 1) + ((v + 1) * vertU); // 우상

                // 삼각형 1: i0, i2, i1
                triangles[t++] = i0; // 인덱스
                triangles[t++] = i2; // 인덱스
                triangles[t++] = i1; // 인덱스

                // 삼각형 2: i1, i2, i3
                triangles[t++] = i1; // 인덱스
                triangles[t++] = i2; // 인덱스
                triangles[t++] = i3; // 인덱스
            }
        }

        Mesh m = new Mesh(); // 새 메쉬
        m.name = $"GridMesh_{axes}_{resU}x{resV}"; // 메쉬 이름
        m.vertices = vertices; // 정점 적용
        m.uv = uvs; // UV 적용
        m.triangles = triangles; // 삼각형 적용

        // 노멀/바운드 계산
        m.RecalculateNormals(); // 노멀 계산
        m.RecalculateBounds(); // 바운드 계산

        return m; // 생성 메쉬 반환
    }

    private void ApplyDeformation() // 원/부채꼴 기준으로 정점 변형
    {
        if (_runtimeMesh == null || _baseVertices == null) return; // 준비 안됐으면 중단

        float circleRadius = circleDiameter * 0.5f; // 원 반지름(지름->반지름)
        float halfSectorAngle = sectorAngle * 0.5f; // 부채꼴 반각

        // 평면에서 사용할 두 축(U,V)과 법선(N) 결정(로컬 기준)
        GetAxes(planeAxes, out Vector3 axisU, out Vector3 axisV, out Vector3 axisN); // 축 결정

        // 중심은 메쉬 로컬 원점(생성 Grid는 중심 피벗 옵션에 따라 달라질 수 있지만, 파임 기준은 요구대로 "중심" 사용)
        Vector3 center = gridCenterPivot ? Vector3.zero : (axisU * (gridSizeU * 0.5f) + axisV * (gridSizeV * 0.5f)); // ✅ 중심 기준점

        for (int i = 0; i < _baseVertices.Length; i++) // 모든 정점 순회
        {
            Vector3 v = _baseVertices[i]; // 현재 정점(로컬)

            // 평면 좌표(U,V)로 투영
            Vector3 fromCenter = v - center; // 중심 기준 벡터
            float u = Vector3.Dot(fromCenter, axisU); // U 좌표
            float w = Vector3.Dot(fromCenter, axisV); // V 좌표
            float dist = Mathf.Sqrt(u * u + w * w); // 중심으로부터 거리(평면상)

            float circleInfluence = 0f; // 원 파임 영향도
            float sectorInfluence = 0f; // 부채꼴 파임 영향도

            // 원 판정/영향도
            if (enableCircle && circleRadius > 0f && dist <= circleRadius) // 원 내부면
            {
                float t01 = Mathf.Clamp01(dist / circleRadius); // 0(중심)~1(경계)
                circleInfluence = Mathf.Clamp01(circleFalloff.Evaluate(t01)); // 커브로 감쇠
            }

            // 부채꼴 판정/영향도
            if (enableSector && sectorRadius > 0f && dist <= sectorRadius) // 반지름 내부면
            {
                float angle = Mathf.Atan2(w, u) * Mathf.Rad2Deg; // -180~180 (U=0도, V=90도 기준)
                float delta = Mathf.DeltaAngle(sectorDirectionAngle, angle); // 중심 방향 대비 각도차

                if (Mathf.Abs(delta) <= halfSectorAngle) // 각도 범위 안이면 부채꼴 내부
                {
                    float t01 = Mathf.Clamp01(dist / sectorRadius); // 0(중심)~1(경계)
                    sectorInfluence = Mathf.Clamp01(sectorFalloff.Evaluate(t01)); // 커브로 감쇠
                }
            }

            // Union: 두 영향 중 더 강한(더 깊게 파임) 값을 사용
            float circleDepthValue = circleInfluence * circleDepth; // 원 깊이 결과
            float sectorDepthValue = sectorInfluence * sectorDepth; // 부채꼴 깊이 결과
            float depth = Mathf.Max(circleDepthValue, sectorDepthValue); // 최종 파임 깊이

            // 변형 방향 결정
            Vector3 deformDir; // 변형 방향
            if (useMeshNormalDirection && _baseNormals != null && _baseNormals.Length == _baseVertices.Length) // 노멀 기반 옵션
            {
                deformDir = -_baseNormals[i].normalized; // 노멀 반대 방향으로 파임
            }
            else
            {
                deformDir = -axisN.normalized; // 평면 법선 반대 방향으로 파임
            }

            _deformedVertices[i] = v + deformDir * depth; // 파임 깊이만큼 이동
        }

        _runtimeMesh.vertices = _deformedVertices; // 정점 갱신

        if (recalcNormals) _runtimeMesh.RecalculateNormals(); // 노멀 재계산
        _runtimeMesh.RecalculateBounds(); // 바운드 재계산

        // ✅ 다음 프레임에도 기준 정점이 "원본"이어야 하므로 baseVertices는 그대로 유지
        // (파라미터 변경 시마다 원본에서 다시 계산하는 구조)
    }

    private void GetAxes(PlaneAxes axes, out Vector3 axisU, out Vector3 axisV, out Vector3 axisN) // ✅ 축 계산(로컬)
    {
        switch (axes) // 선택된 평면 기준으로 축 결정
        {
            case PlaneAxes.XY:
                axisU = Vector3.right; // U = X
                axisV = Vector3.up; // V = Y
                axisN = Vector3.forward; // N = Z
                break;

            case PlaneAxes.YZ:
                axisU = Vector3.up; // U = Y
                axisV = Vector3.forward; // V = Z
                axisN = Vector3.right; // N = X
                break;

            default: // XZ
                axisU = Vector3.right; // U = X
                axisV = Vector3.forward; // V = Z
                axisN = Vector3.up; // N = Y
                break;
        }
    }
}
