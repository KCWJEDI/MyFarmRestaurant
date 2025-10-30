using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class Camera_Main : MonoBehaviour
{
    // 델리게이트 정의
    public delegate void CameraDelegate();
    public event CameraDelegate CameraDel;



    // 자신 오브젝트
    [SerializeField] private GameObject _obj_Camera;
    public GameObject OBJ_Camera => _obj_Camera; 
    // 카메라 고정 대상 오브젝트
    [SerializeField] private GameObject _obj_Target;
    public GameObject OBJ_Target => _obj_Target;


    // 자신 오브젝트 카메라
    [SerializeField] private Camera _mainCamera;
    public Camera MainCamera => _mainCamera;

    public bool isFirstTouchAfterZoom = false;



    


    // 공통 변수

    // 터치중인 손가락 개수
    public int touchCount;
    // 터치값
    public Touch FirstTouch, SecondTouch, ThridTouch, ForthTouch;




    private void Awake()
    {

        // 카메라 오브젝트 검사
        if (_obj_Camera == null)
        {
            _obj_Camera = this.gameObject;
            if (_obj_Camera == null)
            {
                Debug.LogError("카메라 오브젝트가 없습니다");
            }
        }
        // 타겟 오브젝트 검사
        if (_obj_Target == null)
        {
            Debug.LogError("타겟 오브젝트가 없습니다");
        }
        // 카메라 컴포넌트 검사
        if (_mainCamera == null)
        {
            _mainCamera = GetComponent<Camera>();
            if (_mainCamera == null)
            {
                Debug.LogError("카메라 컴포넌트가 없습니다");
            }
        }
        // 카메라 Orthographic유형 검사
        if (_mainCamera != null && !_mainCamera.orthographic)
        {
            Debug.LogError("카메라가 Orthographic이 아닙니다");
        }
    }

    private void Start()
    {
        // 카메라 기본 위치 세팅
        OBJ_Camera.transform.position = this.gameObject.transform.position;
        OBJ_Camera.transform.eulerAngles = new Vector3(30, -45, 0);

        // 카메라위치를 Plane위치에 맞게 조절
        OBJ_Camera.transform.position = OBJ_Target.transform.position + new Vector3(
            OBJ_Target.transform.localScale.x,
            OBJ_Target.transform.localScale.y,
            -OBJ_Target.transform.localScale.z);
    }


    private void Update()
    {
        // 터치입력이 있을때 터치개수 저장
        if (Touch.activeTouches.Count > 0)
        {
            // 현재 터치개수 저장
            touchCount = Touch.activeTouches.Count;


            // 터치개수만큼 변수에 등록
            for (int i = 0; i < touchCount; i++)
            {
                Touch touch = Touch.activeTouches[i];

                ProcessTouch(touch, i);
            }


            // 델리게이트 실행(카메라 입력관련)
            CameraDel();



        }
    }



    private void ProcessTouch(Touch touch, int touchIndex)
    {
        if (touchIndex == 0)
            FirstTouch = touch;
        if (touchIndex == 1)
            SecondTouch = touch;
        //if (touchIndex == 2)
        //    ThridTouch = touch;
        //if (touchIndex == 3)
        //    ForthTouch = touch;
    }


}
