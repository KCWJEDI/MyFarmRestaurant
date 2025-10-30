using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.ProBuilder.MeshOperations;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class Camera_Zoom : MonoBehaviour
{
    // 메인카메라 스크립트
    private Camera_Main cameraMain;


    // 시작터치거리
    private float startDistance;
    // 카메라 Size
    private float initialSize;

    // 줌 최대최소값
    private float minZoom = 2f;
    private float maxZoom = 5f;


    // 스무딩을 위한 변수
    private float targetZoomSize;
    private bool isPinching = false;


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

        targetZoomSize = cameraMain.MainCamera.orthographicSize;
    }

    // 스크립트 델리게이트 함수 추가
    #region 델리게이트 추가
    public void OnEnable()
    {
        Debug.Log("CameraZoom");
        cameraMain.CameraDel += CameraZoom;
    }
    private void OnDisable()
    {
        Debug.Log("-CameraZoom");
        cameraMain.CameraDel -= CameraZoom;

    }
    #endregion




    public void CameraZoom()
    {
        // 터치 입력이 2개라면
        if (cameraMain.touchCount == 2)
        {
            // 두 터치 모두 방금시작했다면
            if (cameraMain.FirstTouch.phase == TouchPhase.Began || cameraMain.SecondTouch.phase == TouchPhase.Began)
            {
                // 초기거리 저장
                startDistance = Vector2.Distance(cameraMain.FirstTouch.screenPosition, cameraMain.SecondTouch.screenPosition);
                // 카메라 Size 저장
                initialSize = cameraMain.MainCamera.orthographicSize;


                isPinching = true;
                targetZoomSize = initialSize;
            }
            // 두 터치 모두 움직이는 중이라면
            else if (cameraMain.FirstTouch.phase == TouchPhase.Moved || cameraMain.SecondTouch.phase == TouchPhase.Moved)
            {
                isPinching = true;

                // 현재거리 계산
                float currentDistance = Vector2.Distance(cameraMain.FirstTouch.screenPosition, cameraMain.SecondTouch.screenPosition);
                float factor = startDistance / currentDistance;

                // 새로운 Size
                float newSize = initialSize * factor;

                targetZoomSize = Mathf.Clamp(newSize, minZoom, maxZoom);
                cameraMain.MainCamera.orthographicSize = targetZoomSize;

                //cameraMain.MainCamera.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
            }
            // 두 터치중 하나라도 끝났을 때
            else if (cameraMain.FirstTouch.phase == TouchPhase.Ended || cameraMain.FirstTouch.phase == TouchPhase.Canceled ||
                     cameraMain.SecondTouch.phase == TouchPhase.Ended || cameraMain.SecondTouch.phase == TouchPhase.Canceled)
            {
                isPinching = false;
                targetZoomSize = cameraMain.MainCamera.orthographicSize;
            }
        }
        // 터치 입력이 2개 이상에서 1개로 줄거나, 0개가 됐을 때
        else
        {
            if (isPinching)
            {
                isPinching = false;
                targetZoomSize = cameraMain.MainCamera.orthographicSize;
            }
        }
    }
    private void Update()
    {
        if(!isPinching)
        {
            float lerpFactor = 1f - Mathf.Pow(0.01f, Time.deltaTime);
            cameraMain.MainCamera.orthographicSize = Mathf.Lerp(
                cameraMain.MainCamera.orthographicSize,
                targetZoomSize,
                lerpFactor
                );
        }
    }
}
