using UnityEngine;
using System.Collections.Generic;

public class DoorSwitchTrigger2D : MonoBehaviour
{
    [Header("키 입력")]
    public KeyCode interactKey = KeyCode.F;              // 인식 키

    [Header("플레이어의 신호 방출 트리거 (Trigger ON)")]
    public Collider2D signalTrigger;                     // 플레이어의 트리거 범위

    private List<DoorSystem2D> detectedDoors = new List<DoorSystem2D>();  // 탐지된 문들

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 플레이어 트리거 영역에 어떤 콜라이더가 들어오면
        // 그것이 문 시스템의 detectionCollider인지 검사
        DoorSystem2D door = collision.GetComponentInParent<DoorSystem2D>();

        if (door != null && door.detectionCollider == collision)          // ★ 수정 핵심
        {
            if (!detectedDoors.Contains(door))
                detectedDoors.Add(door);                                  // 해당 문 탐지
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        DoorSystem2D door = collision.GetComponentInParent<DoorSystem2D>();

        if (door != null && door.detectionCollider == collision)
        {
            if (detectedDoors.Contains(door))
                detectedDoors.Remove(door);                               // 문 제거
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(interactKey))                                // 인식 키 입력
        {
            foreach (var door in detectedDoors)                           // 탐지된 문들만 열기
            {
                door.ChangeState();
            }
        }
    }
}
