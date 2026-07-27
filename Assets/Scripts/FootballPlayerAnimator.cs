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

    [SerializeField] private FootballPlayerController _controller;
    [SerializeField] private FootballBallKicker _kicker;
    [SerializeField] private FootballBallBicycleKicker _bicycleKicker;
    [SerializeField] private FootballBallHeader _header;
    [SerializeField] private Animator _animator;
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

        return false;
    }
}
