using System.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class Camera_SlideMove : MonoBehaviour
{
    // 메인카메라 스크립트
    private Camera_Main cameraMain;

    // 인스팩터에서 조정 가능한 드래그 속도
    [SerializeField]
    private float dragSpeed = 0.01f;
    // 카메라 읻동 제한 범위 (X, Y, Z축)
    private Vector2 _moveLimitX, _moveLimitY, _moveLimitZ;
    // 마지막 터치 위치 (이전 프레임의 터치 좌표)
    private Vector2 lastTouchPosition;

    // 이동 제한 값(XZ 평면 및 Y축)
    private float MaxMinMoveValueXZ = 5f;
    private float MaxMinMoveValueY = 3f;

    // 드래그 중인 터치 ID (멀티터치에서 특정 터치를 추적하기 위함)
    private int lastTouchID = -1;



    // 스무딩 효과 변수
    // 카메라가 최종적으로 도달할 목표 위치
    private Vector3 targetPosition;

    // V3ctor3.SmoothDamp 함수 내부에서 사용할 현재 속도
    private Vector3 currentVelocity;

    // 드래그 종료 후 목표 위치까지 이동하는 데 걸리는 시간 (클수록 스무딩 강도 강해짐)
    [SerializeField] private float dragSmoothTime = 0.15f;

    // 현재 카메라를 드래그 중인지 여부
    private bool isDragging = false;

    // 마지막으로 발생한 월드 좌표계 이동량 (관성 계산에 사용)
    private Vector3 lastInputDelta = Vector3.zero;
    
    // 이전 프레임의 카메라 위치 (관성 계산에 사용)
    private Vector3 previousCameraPosition;

    // 관성 강도 (값이 클수록 손가락을 떼 후 더 멀리 미끄러짐)
    [SerializeField] private float inertiaMultiplier = 0.5f;


    private void Awake()
    {
        // 메인카메라 스크립트 검사
        if (cameraMain == null)
        {
            cameraMain = this.gameObject.GetComponent<Camera_Main>();
            if (cameraMain == null)
            {
                Debug.LogError("메인카메라 스크립트가 없습니다");
            }
        }
        // Awake 시점에 targetPosition을 현재 카메라 위치로 초기화 (SmoothDamp가 튀는 것을 방지)
        if (cameraMain != null && cameraMain.transform != null)
        {
            targetPosition = cameraMain.transform.position;
        }

    }

    // 스크립트 델리게이트 함수 추가
    #region 델리게이트 추가
    public void OnEnable()
    {
        Debug.Log("OneTouchMove");
        if (cameraMain != null)
            cameraMain.CameraDel += OneTouchMove;
        else
            Debug.LogError("Camera_Main 참조가 null입니다. OnEnable에서 델리게이트 등록 실패.");
    }
    private void OnDisable()
    {
        Debug.Log("-OneTouchMove");
        if (cameraMain != null)
            cameraMain.CameraDel -= OneTouchMove;
    }
    #endregion

    private void Start()
    {
        // 카메라를 타겟 오브젝트 위치로 초기 설정
        if (cameraMain != null && cameraMain.OBJ_Camera != null && cameraMain.OBJ_Target != null)
            cameraMain.OBJ_Camera.transform.position = cameraMain.OBJ_Target.transform.position;
        else
            Debug.LogError("카메라 초기 위치 설정을 위한 필수 참조(OBJ_Camera 또는 OBJ_Target)가 없습니다.");

        // 초기 카메라 위치를 기준으로 이동 제한 범위 설정
        float initialCameraX = cameraMain.OBJ_Camera.transform.position.x;
        float initialCameraY = cameraMain.OBJ_Camera.transform.position.y;
        float initialCameraZ = cameraMain.OBJ_Camera.transform.position.z;

        _moveLimitX = new Vector2(initialCameraX - MaxMinMoveValueXZ, initialCameraX + MaxMinMoveValueXZ);
        _moveLimitY = new Vector2(initialCameraY - MaxMinMoveValueY, initialCameraY + MaxMinMoveValueY);
        _moveLimitZ = new Vector2(initialCameraZ - MaxMinMoveValueXZ, initialCameraZ + MaxMinMoveValueXZ);

        // Start 시점에 targetPosition도 현재 카메라 위치로 설정
        targetPosition = cameraMain.transform.position;
    }

    // 터치 입력을 처리하여 카메라를 이동시키는 메서드
    public void OneTouchMove()
    {
        // 카메라 메인 참조가 없거나 터치가 없으면 처리하지 않음.

        // 터치 입력이 1개 이상일 때 드래그 처리 (두 손가락이어도 첫 번째 손가락으로 드래그)
        if (cameraMain.touchCount >= 1)
        {
            // 현재 활성 터치 정보 가져오기
            Touch currentAcctiveTouch = cameraMain.FirstTouch;

            // 터치 페이즈가 'Began'이거나, 줌에서 드래그로 전환되었을 때 (새로운 드래그 시작으로 간주)
            if (cameraMain.FirstTouch.phase == TouchPhase.Began ||
                cameraMain.isFirstTouchAfterZoom ||
                lastTouchID != currentAcctiveTouch.touchId)
            {
                lastTouchPosition = cameraMain.FirstTouch.startScreenPosition;
                lastTouchID = currentAcctiveTouch.touchId;
                isDragging = true;                              // 드래그 시작 플래그 활성화
                currentVelocity = Vector3.zero;                 // SmoothDamp의 속도 초기화
                targetPosition = cameraMain.transform.position; // 목표 위치를 현재 위치로 재설정
                cameraMain.isFirstTouchAfterZoom = false;       // 줌에서 전환 플래그 초기화
                return; // Began 또는 초기화 페이즈에서는 이동 처리하지 않고 리턴

            }
            // 터치후 움직이는 중이라면 (실제 드래그 이동 발생)
            else if (cameraMain.FirstTouch.phase == TouchPhase.Moved)
            {
                // 드래그 중임을 명확히
                isDragging = true;
                // 현재 터치위치 저장
                Vector2 currentTouchPosition = cameraMain.FirstTouch.screenPosition;

                // 스크린 델타를 계산 (이전 터치 위치 대비 현재 터치 위치의 변화량)
                Vector2 screenDelta = lastTouchPosition - currentTouchPosition;

                // 좌우 이동 (X와 Z 축을 더한 벡터) (아이소매트릭 뷰에서 대각선 이동 효과)
                Vector3 horizontalMove = new Vector3(1, 0, 1).normalized * (screenDelta.x);

                // 상하 이동 (Y축만)
                Vector3 verticalMove = new Vector3(0, 1, 0) * (screenDelta.y);


                // 이전 카메라 위치 저장 (관성 계산용)
                previousCameraPosition = cameraMain.transform.position;
                
                // 최종 이동 벡터
                Vector3 worldDelta = (horizontalMove + verticalMove) * dragSpeed * 0.01f;
                
                // 마지막 이동 벡터 저장 (관성 계산용)
                lastInputDelta = worldDelta;

                // 이동 적용
                Vector3 newPosition = cameraMain.transform.position + worldDelta;

                // 이동 제한 적용
                newPosition.x = Mathf.Clamp(newPosition.x, _moveLimitX.x, _moveLimitX.y);
                newPosition.y = Mathf.Clamp(newPosition.y, _moveLimitY.x, _moveLimitY.y);
                newPosition.z = Mathf.Clamp(newPosition.z, _moveLimitZ.x, _moveLimitZ.y);

                // 카메라의 실제 위치를 업데이트 (즉시 이동)
                cameraMain.transform.position = newPosition;

                // 드래그 중에는 targetPosition도 현재 카메라 위치로 계속 업데이트
                // 이렇게 해야 손가락을 뗄 때 SmoothDamp가 현재 위치에서 감속 시작
                targetPosition = newPosition;

                // 마지막 이동량 저장 (관성 계산용)
                lastTouchPosition = currentTouchPosition;
            }
            // 터치가 끝났을 때 또는 취소되었을 때 (손가락을 뗐을 때)
            else if (currentAcctiveTouch.phase == TouchPhase.Ended || currentAcctiveTouch.phase == TouchPhase.Canceled)
            {
                // 단일 터치 종료 시 드래그 상태를 false로 변경하고 관성 적용 준비
                // 그러나 멀티터치 상황을 고려하여 isDragging 해제는 touchCount == 0 에서 하는 것이 더 정확함.
                // 여기서는 관성 타겟 포지션을 설정만 해 둔다.
                if (cameraMain.touchCount == 1)
                {
                    // 관성 효과: 마지막 속도 방향으로 더 나아간 위치를 타겟으로 설정
                    Vector3 projectedPosition = cameraMain.transform.position + lastInputDelta * (1f / Time.deltaTime) * inertiaMultiplier;

                    // 예상 위치에 이동 제한 적용
                    projectedPosition.x = Mathf.Clamp(projectedPosition.x, _moveLimitX.x, _moveLimitX.y);
                    projectedPosition.y = Mathf.Clamp(projectedPosition.y, _moveLimitY.x, _moveLimitY.y);
                    projectedPosition.z = Mathf.Clamp(projectedPosition.z, _moveLimitZ.x, _moveLimitZ.y);

                    // 이 위치를 targetPosition으로 설정 (Update에서 SmoothDamp로 여기까지 부드럽게 이동)
                    targetPosition = projectedPosition;
                    isDragging = false; // 드래그 종료
                    lastTouchID = -1; // 터치 ID 초기화
                }
            }
        }
        // 터치 입력이 완전히 없을 때 (모든 손가락을 뗀 상태)
        else if (cameraMain.touchCount == 0)
        {
            if (isDragging) // 드래그 중이었다면
            {
                isDragging = false; // 드래그 끝!
                lastTouchID = -1;   // 터치 ID 초기화
                cameraMain.isFirstTouchAfterZoom = false; // 줌 후 드래그 플래그 초기화

                // 현재 프레임과 이전 프레임의 위치 차이로 속도 계산
                Vector3 velocity = (cameraMain.transform.position - previousCameraPosition) / Time.deltaTime;
                
                // 속도에 관성 계수를 적용하여 예상 이동 거리 계산
                Vector3 projectedMovement = velocity * inertiaMultiplier;
                
                // 관성 효과: 현재 위치에서 예상 이동 거리만큼 더 나아간 위치를 목표로 설정
                Vector3 projectedPosition = cameraMain.transform.position + projectedMovement;

                // 예상 위치에 이동 제한 적용
                projectedPosition.x = Mathf.Clamp(projectedPosition.x, _moveLimitX.x, _moveLimitX.y);
                projectedPosition.y = Mathf.Clamp(projectedPosition.y, _moveLimitY.x, _moveLimitY.y);
                projectedPosition.z = Mathf.Clamp(projectedPosition.z, _moveLimitZ.x, _moveLimitZ.y);

                targetPosition = projectedPosition; // 현재 위치를 기반으로 관성 목표 설정
                
                // 관성 적용 후 초기화
                lastInputDelta = Vector3.zero;
                

            }
        }
    }
    // Unity의 Update 생명주기 메서드 (매 프레임 호출)
    private void Update()
    {
        if (cameraMain == null || cameraMain.transform == null) return;

        if (!isDragging) // <--- 이 조건이 true여야만 스무딩 실행됨
        {
            float currentCameraDistance = Vector3.Distance(cameraMain.transform.position, targetPosition);
            if (currentCameraDistance > 0.01f) // 미세한 떨림 방지 조건
            {
                cameraMain.transform.position = Vector3.SmoothDamp(
                    cameraMain.transform.position,
                    targetPosition,
                    ref currentVelocity,
                    dragSmoothTime
                );
            }
            else // 목표에 거의 도달했을 때
            {
                cameraMain.transform.position = targetPosition; // 정확히 목표로 설정
                currentVelocity = Vector3.zero; // 속도 초기화
            }
        }
        else
        {
            // 드래그 중일 때는 스무딩을 건너뜀
        }
    }
}
