using UnityEngine;

/// <summary>
/// [물대포(Watercannon) 충돌 콜라이더 스크립트]
///
/// 해마(Seahorse) 오브젝트의 자식으로 배치되어 물대포 충돌 범위를 담당합니다.
/// 물대포 영역에 플레이어가 진입하면, 플레이어가 있는 방향(위/아래)을 기준으로 콜라이더 바깥쪽으로 밀어냅니다.
///
/// 충돌 처리 방식:
/// - OnTriggerStay2D를 사용하여 플레이어가 영역 안에 있는 동안 지속적으로 힘을 가합니다.
/// - 플레이어의 Y 좌표와 물대포 중심의 Y 좌표를 비교하여 위/아래 방향을 결정합니다.
/// - 플레이어를 콜라이더 경계 바깥으로 강제 이동시켜 물줄기 내부에 끼이지 않도록 합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class Watercannon : MonoBehaviour
{
    // =====================================================================
    // Inspector Fields
    // =====================================================================

    [Header("Watercannon Settings")] 
    [SerializeField] [Tooltip("플레이어를 위/아래로 밀어내는 힘의 크기")]
    private float pushForce = 10f;
    
    // =====================================================================
    // Component References
    // =====================================================================

    private BoxCollider2D waterCannonCollider;
    
    // =====================================================================
    // Unity Lifecycle
    // =====================================================================

    private void Awake()
    {
        waterCannonCollider = GetComponent<BoxCollider2D>();
    }
    
    // =====================================================================
    // Collision
    // =====================================================================

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        Rigidbody2D playerRb = other.attachedRigidbody;
        if (!playerRb) return;

        Vector3 playerPosition = other.transform.position;
        Bounds colliderBounds = waterCannonCollider.bounds;
        
        // 플레이어 Y 좌표와 물대포 콜라이더 중심 Y 좌표를 비교하여 밀어낼 방향 결정
        if (playerPosition.y > transform.position.y)
        {
            // 플레이어가 위에서 진입 → 콜라이더 상단 ㄱ여계 바깥으로 이동
            other.transform.position = new Vector3(playerPosition.x, colliderBounds.max.y, playerPosition.z);
        }
        else
        {
            // 플레이어가 아래에서 진입 → 콜라이더 하단 경계 바깥으로 이동
            other.transform.position = new Vector3(playerPosition.x, colliderBounds.min.y, playerPosition.z);
        }
        
        // 위/아래 방향으로 지속적인 힘을 가하여 플레이어가 물대포를 넘지 못하도록 합니다
        Vector2 pushDir = playerPosition.y > transform.position.y ? Vector2.up : Vector2.down;
        playerRb.AddForce(pushDir * pushForce, ForceMode2D.Force);
    }
}
