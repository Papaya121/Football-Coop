using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FootballPlayerController))]
public sealed class FootballPlayerAnimator : MonoBehaviour
{
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");

    [SerializeField] private FootballPlayerController _controller;
    [SerializeField] private Animator _animator;

    private RuntimeAnimatorController _cachedAnimatorController;
    private bool _hasIsRunningParameter;
    private bool _hasIsJumpingParameter;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
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

    private void ResolveReferences()
    {
        if (_controller == null)
            _controller = GetComponent<FootballPlayerController>();

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

        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Bool)
                continue;

            if (parameter.nameHash == IsRunningHash)
                _hasIsRunningParameter = true;
            else if (parameter.nameHash == IsJumpingHash)
                _hasIsJumpingParameter = true;
        }
    }
}
