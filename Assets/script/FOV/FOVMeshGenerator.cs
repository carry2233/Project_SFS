// FOVMeshGenerator.cs
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways] // 에디터/플레이 모두에서 갱신되도록
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))] // 메쉬 필수
public class FOVMeshGenerator : MonoBehaviour
{
    [Header("사용 토글")]
    public bool useCircle = true;      // 원형 시야 사용 여부
    public bool useSector = true;      // 부채꼴 시야 사용 여부

    [Header("원형 시야")]
    public float radiusCircle = 5f;    // 원형 시야 반지름
    [Range(8, 512)]
    public int circleSegments = 128;   // 원형 분할 세그먼트 수

    [Header("부채꼴 시야 (중심축 = 로컬 X+축)")]
    public float sectorRadius = 10f;         // 부채꼴 최대 거리
    [Range(0f, 90f)]
    public float sectorHalfAngleDeg = 30f;   // 절반 각도(30 → 총 60도)
    [Range(8, 512)]
    public int sectorSegments = 128;         // 부채꼴 분할 세그먼트 수

    [Header("기타")]
    public bool autoRegenerateOnValidate = true; // 인스펙터 값 변경 시 자동 재생성
    public bool doubleSided = true;              // 양면 렌더링을 원할 때(노멀 위/아래 모두 포함)
    public float yOffset = 0f;                   // 메쉬 수직 오프셋(깊이 떨림 방지용 미세값)

    private MeshFilter mf;                 // 메쉬필터 캐시
    private Mesh generatedMesh;            // 생성된 메쉬 캐시

    private void OnEnable() // 컴포넌트 활성화 시 메쉬 보장
    {
        EnsureComponents(); // 필수 컴포넌트 확보
        RegenerateMesh();   // 메쉬 생성
    }

    private void OnValidate() // 인스펙터 값 변경 시 자동 재생성
    {
        if (autoRegenerateOnValidate && isActiveAndEnabled)
        {
            EnsureComponents(); // 필수 컴포넌트 확보
            RegenerateMesh();   // 메쉬 생성
        }
    }

    private void EnsureComponents() // 필수 컴포넌트 캐시/초기화
    {
        if (mf == null) mf = GetComponent<MeshFilter>(); // MeshFilter 캐시
        if (mf.sharedMesh == null)                       // 비어있으면 새 메쉬 생성
        {
            generatedMesh = new Mesh { name = "FOVMesh" }; // 새 메쉬 생성
            mf.sharedMesh = generatedMesh;                 // 공유 메쉬 할당
        }
        else
        {
            generatedMesh = mf.sharedMesh; // 기존 메쉬 연결
        }
    }

    public void RegenerateMesh() // 파라미터 기반으로 원+부채꼴 메쉬 재생성
    {
        if (generatedMesh == null)
        {
            generatedMesh = new Mesh { name = "FOVMesh" }; // 안전 장치
            mf.sharedMesh = generatedMesh;                 // 연결
        }

        var verts = new List<Vector3>(); // 정점 리스트
        var tris  = new List<int>();     // 삼각형 인덱스 리스트
        var uvs   = new List<Vector2>(); // UV(필수는 아니지만 기본값 생성)

        // 로컬 X+ 축을 기준으로 XZ 평면에 생성 (Y = yOffset 고정)
        if (useCircle && radiusCircle > 0f && circleSegments >= 8)
            AppendCircleFan(verts, tris, uvs, radiusCircle, circleSegments);

        if (useSector && sectorRadius > 0f && sectorSegments >= 8 && sectorHalfAngleDeg > 0f)
            AppendSectorFan(verts, tris, uvs, sectorRadius, sectorHalfAngleDeg, sectorSegments);

        // 양면 옵션이면 뒷면 삼각형도 추가(간단히 인덱스 뒤집기 방식)
        if (doubleSided)
        {
            int count = tris.Count;
            for (int i = 0; i < count; i += 3)
            {
                tris.Add(tris[i]);       // 기존 삼각형의 역순 추가
                tris.Add(tris[i + 2]);
                tris.Add(tris[i + 1]);
            }
        }

        generatedMesh.Clear();                 // 메쉬 초기화
        generatedMesh.SetVertices(verts);      // 정점 설정
        generatedMesh.SetTriangles(tris, 0);   // 삼각형 설정
        generatedMesh.SetUVs(0, uvs);          // UV 설정

        // 노멀은 위(+Y) 방향으로 고정 (스텐실 전용이라 조명은 의미 없음)
        var normals = new Vector3[verts.Count];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up; // 위쪽 노멀
        generatedMesh.SetNormals(normals);     // 노멀 설정

        generatedMesh.RecalculateBounds();     // 경계 갱신
    }

    private void AppendCircleFan(List<Vector3> verts, List<int> tris, List<Vector2> uvs, float radius, int seg) // 원형 삼각 팬 추가
    {
        int baseIndex = verts.Count;                   // 시작 정점 인덱스
        verts.Add(new Vector3(0f, yOffset, 0f));       // 중심점
        uvs.Add(new Vector2(0.5f, 0.5f));              // 임의 UV

        float step = Mathf.PI * 2f / seg;              // 라디안 스텝
        for (int i = 0; i <= seg; i++)                 // 마지막 점 = 시작점과 동일(폐합)
        {
            float ang = i * step;                      // 현재 각도(라디안)
            float x = Mathf.Cos(ang) * radius;         // X 좌표
            float z = Mathf.Sin(ang) * radius;         // Z 좌표
            verts.Add(new Vector3(x, yOffset, z));     // 둘레 점 추가
            uvs.Add(new Vector2((x / radius + 1f) * .5f, (z / radius + 1f) * .5f)); // 간단 UV
        }

        for (int i = 0; i < seg; i++)                  // 삼각형 팬 구성
        {
            tris.Add(baseIndex);                       // 중심
            tris.Add(baseIndex + i + 1);               // 현재 둘레
            tris.Add(baseIndex + i + 2);               // 다음 둘레
        }
    }

    private void AppendSectorFan(List<Vector3> verts, List<int> tris, List<Vector2> uvs, float radius, float halfDeg, int seg) // 부채꼴 삼각 팬 추가
    {
        int baseIndex = verts.Count;                           // 시작 정점 인덱스
        verts.Add(new Vector3(0f, yOffset, 0f));               // 중심점
        uvs.Add(new Vector2(0.5f, 0.5f));                      // 임의 UV

        // 중심축을 로컬 X+ 로 두기 위해 0도를 X+ 로 하고, -half ~ +half 로 스윕
        float startDeg = -halfDeg;                              // 시작 각(도)
        float endDeg   = +halfDeg;                              // 끝 각(도)
        float stepDeg  = (endDeg - startDeg) / seg;             // 각도 스텝(도)

        for (int i = 0; i <= seg; i++)                          // 부채꼴 경계 점들
        {
            float angDeg = startDeg + stepDeg * i;              // 현재 각(도)
            float angRad = angDeg * Mathf.Deg2Rad;              // 라디안 변환
            float x = Mathf.Cos(angRad) * radius;               // X 좌표(로컬 X+ 기준)
            float z = Mathf.Sin(angRad) * radius;               // Z 좌표
            verts.Add(new Vector3(x, yOffset, z));              // 점 추가
            uvs.Add(new Vector2((x / radius + 1f) * .5f, (z / radius + 1f) * .5f)); // 간단 UV
        }

        for (int i = 0; i < seg; i++)                           // 삼각형 팬 구성
        {
            tris.Add(baseIndex);                                 // 중심
            tris.Add(baseIndex + i + 1);                         // 현재 둘레
            tris.Add(baseIndex + i + 2);                         // 다음 둘레
        }
    }
}
