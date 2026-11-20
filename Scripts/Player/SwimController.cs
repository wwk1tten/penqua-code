using UnityEngine;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem;
#endif

public class ReadOnlyAttribute : PropertyAttribute { }

public class SwimController : MonoBehaviour
{  
    private float _speed;
    private float _animationBlend;
    private float swimTargetRotation = 0.0f;
    private float swimRotationVelocity;
    private float swimVerticalVelocity;
    private float swimAcceleration = 2.0f;
    private float nextPlayTime = 0f;
    private int _animIDIsSwimming;
    private int _animIDSpeed;
    private int _animIDMotionSpeed;

    private Animator _animator;
    private GameObject _mainCamera;
    private StarterAssetsInputs _input;
    private CharacterController _cc;
    private bool _canReleaseAnimal = false;
    private bool _hasAnimator;
    [SerializeField, ReadOnly] private bool _isSwimming;
    public bool IsSwimming => _isSwimming;

    public float swimSpeed = 6.5f;
    public float swimGravity = -3.0f;  // -15보다 약함
    [Range(0.0f, 0.3f)]
    public float SwimRotationSmoothTime = 0.12f;
    public float SwimAudioVolume = 0.5f;
    public float SwimAudioCooldown = 0.2f;
    public float maximumFallSpeed = -2.0f; // 수영 중 최대 낙하 속도


    public AudioClip[] SwimAudioClips; 
    public ParticleSystem bubbleTrailEffect;
    

    private void Awake()
    {
        if (_mainCamera == null)
        {
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }
        
    }

    private void Start()
    {
        _hasAnimator = TryGetComponent(out _animator);
        _cc = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        _input = GetComponent<StarterAssetsInputs>();
        _animIDIsSwimming = Animator.StringToHash("isSwimming");
        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");

    }
    private void Update()
    {
        
        if (_isSwimming)
        {
            SwimJumpAndGravity();
            HandleBubbleTrail();
            PlaySwimSound();
            SwimMove();
        }
    }

    public void SwimMove()
    {
        float targetSpeed;
        float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

        // A. 속도 설정: 펭귄이니까 걷기보다 훨씬 빠르게 (예: 1.5배 ~ 2배)
        float currentSwimSpeed = swimSpeed; // Inspector에서 SprintSpeed보다 높게 설정하세요.
        
        // 입력이 없어도 바로 0이 되지 않도록 처리 (관성)
        if (_input.move == Vector2.zero) targetSpeed = 0.0f;
        else targetSpeed = currentSwimSpeed;

        _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * swimAcceleration);
        if (_animationBlend < 0.01f) _animationBlend = 0f;
        
        // 실제 이동 속도에 반영
        _speed = _animationBlend; 

        // C. 회전 로직
        Vector3 inputDir = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;
        if (_input.move != Vector2.zero)
        {
            swimTargetRotation = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
            // 물속에서는 회전이 지상보다 조금 더 느리고 부드럽게 (RotationSmoothTime을 늘림)
            float swimRotationSmoothTime = SwimRotationSmoothTime * 1.5f; 
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, swimTargetRotation, ref swimRotationVelocity, swimRotationSmoothTime);
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
        }

        Vector3 targetDirection = Quaternion.Euler(0.0f, swimTargetRotation, 0.0f) * Vector3.forward;

        if(_input.move != Vector2.zero || _speed > 0.1f)
        {
            // 입력이 있을 땐 입력 방향, 없을 땐 현재 캐릭터가 보는 방향으로 관성 이동
            Vector3 moveDir = (_input.move != Vector2.zero) ? targetDirection.normalized : transform.forward;
            _cc.Move(moveDir * (_speed * Time.deltaTime) + new Vector3(0.0f, swimVerticalVelocity, 0.0f) * Time.deltaTime);
        }
        // 수영 상태 Animator 업데이트
        if (_hasAnimator){
            _animator.SetFloat(_animIDSpeed, _animationBlend);
            _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
        }

        /*if (_canReleaseAnimal && Input.GetKeyDown(KeyCode.Q)) {
            //ActivateCapsule(transform.position);
        }*/
    }

    public void SwimJumpAndGravity()
    {
        if(_cc.isGrounded)
        {
            swimVerticalVelocity = -2f; // 물속에서 바닥에 닿으면 수직 속도 초기화
        }

        // 수영 중 위로 올라가기
        if (_input.jump)
        {
            swimVerticalVelocity = 2f; // 작은 상승력
            _input.jump = false; // 점프 입력 초기화
        }
        else
        {
            swimVerticalVelocity += swimGravity * Time.deltaTime; // 중력 적용
        }
        swimVerticalVelocity = Mathf.Max(swimVerticalVelocity, maximumFallSpeed);


        
        
    }

    public void SetSwimming(bool state)
    {
        // 현재 상태와 동일한 값이 들어오면 불필요한 연산을 막습니다.
        if (_isSwimming == state) return;
        _isSwimming = state;

        Debug.Log($"수영 상태 변경: {state}");
        if (_hasAnimator)
        {
            _animator.SetBool(_animIDIsSwimming, _isSwimming);
        }

        if (!_isSwimming)
        {
            if (bubbleTrailEffect != null)
            {
                bubbleTrailEffect.Stop(); // 수영 종료 시 버블 효과 정지
                swimVerticalVelocity = 0f;
                _speed = 0f;
                _animationBlend = 0f;
            }
        }
    }

    private void PlaySwimSound()
        {
            if (SwimAudioClips.Length == 0 || Time.time < nextPlayTime) return;

            
            if(_input.move != Vector2.zero)
            {
                nextPlayTime = Time.time + SwimAudioCooldown; 
                var index = Random.Range(0, SwimAudioClips.Length);
                // 플레이어 위치에서 물소리 재생
                AudioSource.PlayClipAtPoint(SwimAudioClips[index], transform.position, SwimAudioVolume);

            }
            
        }
        

    private void HandleBubbleTrail(){
        // 움직임 입력이 있을 때
        if (_isSwimming && _input.move != Vector2.zero)
        {
            if (bubbleTrailEffect != null)
            {
                if (!bubbleTrailEffect.isPlaying) bubbleTrailEffect.Play();

                // ★ 핵심: 질주(Sprint) 중이면 버블을 많이, 아니면 적게
            
                var emission = bubbleTrailEffect.emission;
                
                // RateOverTime을 속도에 따라 변경
                // 예: 달리기면 20개, 걷기면 5개
                emission.rateOverTime = _input.sprint ? 20f : 5f; 
            }

        }
        else
        {
            if (bubbleTrailEffect != null && bubbleTrailEffect.isPlaying) bubbleTrailEffect.Stop();

        }
        
    }


}