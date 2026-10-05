using UnityEngine;

public enum FootballBallActionAnimationTriggerMode
{
    OnlyOnSuccessfulAction,
    AlwaysOnInput
}

[DisallowMultipleComponent]
[RequireComponent(typeof(FootballPlayerController))]
public sealed class FootballPlayerAnimator : MonoBehaviour
{
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int KickHash = Animator.StringToHash("Kick");
    private static readonly int BicycleKickHash = Animator.StringToHash("BicycleKick");
    private static readonly int HeaderHash = Animator.StringToHash("Header");
    private static readonly int DoubleJumpHash = Animator.StringToHash("DoubleJump");
    private static readonly int RotateHash = Animator.StringToHash("Rotate");

    [SerializeField] private FootballPlayerController _controller;
    [SerializeField] private FootballBallKicker _kicker;
    [SerializeField] private FootballBallBicycleKicker _bicycleKicker;
    [SerializeField] private FootballBallHeader _header;
    [SerializeField] private Animator _animator;
    [SerializeField] private string _rotateStatePath = "Base Layer.Rotate";
    [SerializeField, Min(0)] private int _rotateLayer;
    [SerializeField, Min(0.1f)] private float _rotateEntryTimeout = 1f;
    [SerializeField, Min(0f)] private float _rotatePoseBlendDuration = 0.25f;
    [SerializeField] private FootballBallActionAnimationTriggerMode _kickAnimationTriggerMode = FootballBallActionAnimationTriggerMode.OnlyOnSuccessfulAction;
    [SerializeField] private FootballBallActionAnimationTriggerMode _bicycleKickAnimationTriggerMode = FootballBallActionAnimationTriggerMode.OnlyOnSuccessfulAction;
    [SerializeField] private FootballBallActionAnimationTriggerMode _headerAnimationTriggerMode = FootballBallActionAnimationTriggerMode.OnlyOnSuccessfulAction;

    private RuntimeAnimatorController _cachedAnimatorController;
    private bool _hasIsRunningParameter;
    private bool _hasIsJumpingParameter;
    private bool _hasKickParameter;
    private bool _hasBicycleKickParameter;
    private bool _hasHeaderParameter;
    private bool _hasDoubleJumpParameter;
    private bool _hasRotateParameter;
    private bool _rotationPending;
    private bool _enteredRotate;
    private int _rotateStateHash;
    private float _rotateEntryWait;
    private Avatar _poseAvatar;
    private Transform[] _poseBones;
    private Quaternion[] _lastTurnRotations;
    private Quaternion[] _blendStartRotations;
    private Vector3[] _lastTurnPositions;
    private Vector3[] _blendStartPositions;
    private bool _hasTurnPose;
    private bool _poseBlending;
    private float _poseBlendElapsed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (_controller != null)
            _controller.DoubleJumped += OnDoubleJumped;

        if (_kicker != null)
        {
            _kicker.KickAttempted += OnKickAttempted;
            _kicker.Kicked += OnKicked;
        }

        if (_bicycleKicker != null)
        {
            _bicycleKicker.BicycleKickAttempted += OnBicycleKickAttempted;
            _bicycleKicker.BicycleKicked += OnBicycleKicked;
        }

        if (_header != null)
        {
            _header.HeaderAttempted += OnHeaderAttempted;
            _header.Headed += OnHeaded;
        }
    }

    private void OnDisable()
    {
        CompleteFacingRotation();
        if (_controller != null)
            _controller.DoubleJumped -= OnDoubleJumped;

        if (_kicker != null)
        {
            _kicker.KickAttempted -= OnKickAttempted;
            _kicker.Kicked -= OnKicked;
        }

        if (_bicycleKicker != null)
        {
            _bicycleKicker.BicycleKickAttempted -= OnBicycleKickAttempted;
            _bicycleKicker.BicycleKicked -= OnBicycleKicked;
        }

        if (_header != null)
        {
            _header.HeaderAttempted -= OnHeaderAttempted;
            _header.Headed -= OnHeaded;
        }
    }

    private void Update()
    {
        ResolveReferences();

        if (_animator == null || _controller == null)
            return;

        RefreshParameterCache();

        if (_hasIsRunningParameter)
            _animator.SetBool(IsRunningHash, _controller.IsRunning);

        if (_hasIsJumpingParameter)
            _animator.SetBool(IsJumpingHash, _controller.IsJumping);
    }

    public void TriggerKickAnimation()
    {
        TriggerActionAnimation(KickHash);
    }

    public void TriggerBicycleKickAnimation()
    {
        TriggerActionAnimation(BicycleKickHash);
    }

    public void TriggerHeaderAnimation()
    {
        TriggerActionAnimation(HeaderHash);
    }

    public void TriggerDoubleJumpAnimation()
    {
        TriggerActionAnimation(DoubleJumpHash);
    }

    public void TriggerRotateAnimation()
    {
        TriggerActionAnimation(RotateHash);
    }

    public bool TryBeginFacingRotation()
    {
        if (!isActiveAndEnabled)
            return false;

        // Direction changes during a turn use the latest requested direction,
        // without restarting the clip or queuing extra Rotate triggers.
        if (_rotationPending)
            return true;

        ResolveReferences();
        if (_animator == null || !_animator.isActiveAndEnabled || !_animator.isInitialized ||
            _rotateLayer >= _animator.layerCount)
            return false;

        RefreshParameterCache();
        _rotateStateHash = Animator.StringToHash(_rotateStatePath);
        if (!_hasRotateParameter || !_animator.HasState(_rotateLayer, _rotateStateHash))
            return false;

        _rotationPending = true;
        _enteredRotate = false;
        _rotateEntryWait = 0f;
        _hasTurnPose = false;
        _poseBlending = false;
        TriggerRotateAnimation();
        return true;
    }

    private void LateUpdate()
    {
        if (!_rotationPending)
        {
            BlendFacingPose();
            return;
        }

        if (_animator == null || !_animator.isActiveAndEnabled ||
            _cachedAnimatorController != _animator.runtimeAnimatorController ||
            _rotateLayer >= _animator.layerCount)
        {
            CompleteFacingRotation();
            return;
        }

        AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(_rotateLayer);
        bool isCurrentRotate = current.fullPathHash == _rotateStateHash;
        bool isNextRotate = _animator.IsInTransition(_rotateLayer) &&
            _animator.GetNextAnimatorStateInfo(_rotateLayer).fullPathHash == _rotateStateHash;

        if (isCurrentRotate || isNextRotate)
            _enteredRotate = true;

        if (isCurrentRotate && EnsurePoseBones())
        {
            for (int i = 0; i < _poseBones.Length; i++)
                if (_poseBones[i] != null)
                {
                    _lastTurnRotations[i] = _poseBones[i].localRotation;
                    _lastTurnPositions[i] = _poseBones[i].localPosition;
                }
            _hasTurnPose = true;
        }

        // Even at normalizedTime >= 1 the outgoing turn can still affect the
        // blended pose. Mirror only after the destination pose has taken over.
        if (_enteredRotate && !isCurrentRotate && !isNextRotate)
        {
            CompleteFacingRotation();
            return;
        }

        // A blocked/missing transition must not leave the model facing backwards.
        // Once the state starts, its actual playback controls completion, not a timer.
        if (!_enteredRotate && _animator.speed != 0f)
        {
            _rotateEntryWait += _animator.updateMode == AnimatorUpdateMode.UnscaledTime
                ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_rotateEntryWait >= _rotateEntryTimeout)
                CompleteFacingRotation();
        }
    }

    public void CancelFacingRotation()
    {
        _rotationPending = false;
        _enteredRotate = false;
        _hasTurnPose = false;
        _poseBlending = false;
        if (_animator != null && _animator.isInitialized && _hasRotateParameter)
            _animator.ResetTrigger(RotateHash);
    }

    private void CompleteFacingRotation()
    {
        if (!_rotationPending)
            return;

        bool blendPose = _hasTurnPose && _rotatePoseBlendDuration > 0f &&
            isActiveAndEnabled && _animator != null && _animator.isActiveAndEnabled && EnsurePoseBones();
        if (blendPose)
        {
            System.Array.Copy(_lastTurnRotations, _blendStartRotations, _poseBones.Length);
            System.Array.Copy(_lastTurnPositions, _blendStartPositions, _poseBones.Length);
        }

        CancelFacingRotation();
        _controller?.ApplyFacingVisual();
        if (blendPose)
        {
            // Work in the rig's own coordinates: world-space humanoid poses do
            // not represent reflected parents consistently in both directions.
            // Remove the clip's turn heading before mirroring its posture.
            int hipsIndex = (int)HumanBodyBones.Hips;
            Transform hips = _poseBones[hipsIndex];
            if (hips != null)
            {
                Quaternion parentRotation = Quaternion.Inverse(_animator.transform.rotation) * hips.parent.rotation;
                Quaternion turnRotation = parentRotation * _blendStartRotations[hipsIndex];
                Quaternion targetRotation = parentRotation * hips.localRotation;
                Quaternion rebase = GetPoseHeading(targetRotation) * Quaternion.Inverse(GetPoseHeading(turnRotation));
                _blendStartRotations[hipsIndex] = Quaternion.Inverse(parentRotation) * rebase * turnRotation;
            }
            _poseBlending = true;
            _poseBlendElapsed = 0f;
            BlendFacingPose(false);
        }
    }

    private bool EnsurePoseBones()
    {
        if (!_animator.isHuman || _animator.avatar == null || !_animator.avatar.isValid)
            return false;
        if (_poseBones != null && _poseAvatar == _animator.avatar)
            return true;

        _poseAvatar = _animator.avatar;
        _poseBones = new Transform[(int)HumanBodyBones.LastBone];
        _lastTurnRotations = new Quaternion[_poseBones.Length];
        _blendStartRotations = new Quaternion[_poseBones.Length];
        _lastTurnPositions = new Vector3[_poseBones.Length];
        _blendStartPositions = new Vector3[_poseBones.Length];
        for (int i = 0; i < _poseBones.Length; i++)
            _poseBones[i] = _animator.GetBoneTransform((HumanBodyBones)i);
        return true;
    }

    private static Quaternion GetPoseHeading(Quaternion rotation)
    {
        Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
        return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
    }

    private void BlendFacingPose(bool advanceTime = true)
    {
        if (!_poseBlending)
            return;
        if (_animator == null || !_animator.isActiveAndEnabled || _poseAvatar != _animator.avatar ||
            _rotatePoseBlendDuration <= 0f)
        {
            _poseBlending = false;
            return;
        }

        if (advanceTime)
        {
            float deltaTime = _animator.updateMode == AnimatorUpdateMode.UnscaledTime
                ? Time.unscaledDeltaTime : Time.deltaTime;
            _poseBlendElapsed += deltaTime * Mathf.Abs(_animator.speed);
        }

        float progress = Mathf.Clamp01(_poseBlendElapsed / _rotatePoseBlendDuration);
        if (progress >= 1f)
        {
            _poseBlending = false;
            return;
        }

        // Animator supplies the destination pose every frame. Only posture is
        // smoothed here; the previous turn's yaw cannot rotate the player back.
        progress = Mathf.SmoothStep(0f, 1f, progress);
        for (int i = 0; i < _poseBones.Length; i++)
            if (_poseBones[i] != null)
            {
                _poseBones[i].localRotation = Quaternion.Slerp(
                    _blendStartRotations[i], _poseBones[i].localRotation, progress);
                // Blend the clip's actual local offsets too, particularly the
                // hips height. Never write the player or Animator root position.
                _poseBones[i].localPosition = Vector3.Lerp(
                    _blendStartPositions[i], _poseBones[i].localPosition, progress);
            }
    }

    private void ResolveReferences()
    {
        if (_controller == null)
            _controller = GetComponent<FootballPlayerController>();

        if (_kicker == null)
            _kicker = GetComponent<FootballBallKicker>();

        if (_bicycleKicker == null)
            _bicycleKicker = GetComponent<FootballBallBicycleKicker>();

        if (_header == null)
            _header = GetComponent<FootballBallHeader>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);
    }

    private void RefreshParameterCache()
    {
        if (_cachedAnimatorController == _animator.runtimeAnimatorController)
            return;

        _cachedAnimatorController = _animator.runtimeAnimatorController;
        _hasIsRunningParameter = false;
        _hasIsJumpingParameter = false;
        _hasKickParameter = false;
        _hasBicycleKickParameter = false;
        _hasHeaderParameter = false;
        _hasDoubleJumpParameter = false;
        _hasRotateParameter = false;

        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == IsRunningHash)
                _hasIsRunningParameter = true;
            else if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == IsJumpingHash)
                _hasIsJumpingParameter = true;
            else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == KickHash)
                _hasKickParameter = true;
            else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == BicycleKickHash)
                _hasBicycleKickParameter = true;
            else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == HeaderHash)
                _hasHeaderParameter = true;
            else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == DoubleJumpHash)
                _hasDoubleJumpParameter = true;
            else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == RotateHash)
                _hasRotateParameter = true;
        }
    }

    private void OnDoubleJumped()
    {
        TriggerActionAnimation(DoubleJumpHash);
    }

    private void OnKicked()
    {
        if (_kickAnimationTriggerMode != FootballBallActionAnimationTriggerMode.OnlyOnSuccessfulAction)
            return;

        TriggerActionAnimation(KickHash);
    }

    private void OnKickAttempted()
    {
        if (_kickAnimationTriggerMode != FootballBallActionAnimationTriggerMode.AlwaysOnInput)
            return;

        TriggerActionAnimation(KickHash);
    }

    private void OnBicycleKicked()
    {
        if (_bicycleKickAnimationTriggerMode != FootballBallActionAnimationTriggerMode.OnlyOnSuccessfulAction)
            return;

        TriggerActionAnimation(BicycleKickHash);
    }

    private void OnBicycleKickAttempted()
    {
        if (_bicycleKickAnimationTriggerMode != FootballBallActionAnimationTriggerMode.AlwaysOnInput)
            return;

        TriggerActionAnimation(BicycleKickHash);
    }

    private void OnHeaded()
    {
        if (_headerAnimationTriggerMode != FootballBallActionAnimationTriggerMode.OnlyOnSuccessfulAction)
            return;

        TriggerActionAnimation(HeaderHash);
    }

    private void OnHeaderAttempted()
    {
        if (_headerAnimationTriggerMode != FootballBallActionAnimationTriggerMode.AlwaysOnInput)
            return;

        TriggerActionAnimation(HeaderHash);
    }

    private void TriggerActionAnimation(int parameterHash)
    {
        ResolveReferences();

        if (_animator == null)
            return;

        RefreshParameterCache();

        if (HasTriggerParameter(parameterHash))
            _animator.SetTrigger(parameterHash);
    }

    private bool HasTriggerParameter(int parameterHash)
    {
        if (parameterHash == KickHash)
            return _hasKickParameter;

        if (parameterHash == BicycleKickHash)
            return _hasBicycleKickParameter;

        if (parameterHash == HeaderHash)
            return _hasHeaderParameter;

        if (parameterHash == DoubleJumpHash)
            return _hasDoubleJumpParameter;

        if (parameterHash == RotateHash)
            return _hasRotateParameter;

        return false;
    }
}
