using System;
using System.Collections.Generic;
using UnityEngine;

public enum FootballBotState
{
    Idle,
    Guard,
    Hold,
    Pressure,
    GoalLineRecovery,
    Intercept,
    AttackSetup,
    AerialContest,
    EmergencyClear,
    Recover
}

public enum FootballBotAerialAction
{
    None,
    BodyBlock,
    Header,
    BicycleKick
}

[DisallowMultipleComponent]
[RequireComponent(typeof(FootballPlayerController))]
[RequireComponent(typeof(Rigidbody))]
public sealed class FootballBotBrain : MonoBehaviour
{
    private const int PredictionCapacity = 40;
    private const float DefaultLeftGoalX = -8.8f;
    private const float DefaultRightGoalX = 8.8f;

    [Header("Skill")]
    [SerializeField, Min(0.02f)] private float _decisionInterval = 0.11f;
    [SerializeField, Min(0f)] private float _reactionDelay = 0.22f;
    [SerializeField, Min(0f)] private float _positionError = 0.16f;
    [SerializeField, Min(0f)] private float _actionTimingError = 0.08f;
    [SerializeField, Range(0f, 1f)] private float _doubleJumpChance = 0.38f;
    [SerializeField, Range(0f, 1f)] private float _bodyBlockChance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _bicycleKickChance = 0.2f;

    [Header("Prediction")]
    [SerializeField, Range(0.5f, 2.5f)] private float _predictionHorizon = 1.35f;
    [SerializeField, Range(0.02f, 0.12f)] private float _predictionStep = 0.05f;
    [SerializeField, Min(1f)] private float _assumedMoveSpeed = 7f;
    [SerializeField, Min(0f)] private float _standingInteractionHeight = 2.65f;
    [SerializeField, Min(0.1f)] private float _bodyBlockMinimumHeight = 1.85f;
    [SerializeField, Min(0.1f)] private float _bodyBlockMaximumHeight = 3.25f;
    [SerializeField, Range(0.2f, 1.2f)] private float _bodyBlockMaximumLeadTime = 0.72f;

    [Header("Positioning")]
    [SerializeField, Min(0.1f)] private float _attackSetupDistance = 0.8f;
    [SerializeField, Min(0.05f)] private float _targetDeadZone = 0.28f;
    [SerializeField, Min(0.05f)] private float _turnThreshold = 0.52f;
    [SerializeField, Min(0.1f)] private float _overheadHoldWidth = 0.58f;
    [SerializeField, Min(0.1f)] private float _overheadHoldHeight = 1.45f;
    [SerializeField, Min(0f)] private float _stateCommitTime = 0.52f;

    [Header("Safety")]
    [SerializeField, Min(0.5f)] private float _ownGoalDangerDistance = 3.1f;
    [SerializeField, Min(0.1f)] private float _goalSideSetupDistance = 0.82f;
    [SerializeField, Min(0.1f)] private float _obstacleJumpDistance = 1.45f;
    [SerializeField, Min(0.1f)] private float _minimumObstacleJumpDistance = 0.28f;

    [Header("Tactics")]
    [SerializeField, Min(0.1f)] private float _attackFollowUpDuration = 2.1f;
    [SerializeField, Min(0.1f)] private float _pressureCommitDuration = 3.8f;
    [SerializeField, Min(0.1f)] private float _stalledBallPressDelay = 1.15f;

    private readonly Queue<Observation> _observations = new Queue<Observation>(8);
    private readonly PredictionPoint[] _prediction = new PredictionPoint[PredictionCapacity];

    private FootballPlayerController _controller;
    private FootballBallKicker _kicker;
    private FootballBallHeader _header;
    private FootballBallBicycleKicker _bicycleKicker;
    private FootballBall _ball;
    private FootballPlayerController _opponent;
    private FootballMatchController _matchController;
    private Rigidbody _body;
    private SphereCollider _ballCollider;
    private System.Random _random;

    private FootballTeamSide _side;
    private FootballBotState _state = FootballBotState.Idle;
    private FootballBotAerialAction _plannedAerialAction;
    private Observation _perceivedWorld;
    private bool _hasPerceivedWorld;
    private bool _configured;
    private int _attackDirection = -1;
    private int _committedMoveDirection;
    private int _predictionCount;
    private int _airJumpCommands;
    private float _ownGoalX = DefaultRightGoalX;
    private float _opponentGoalX = DefaultLeftGoalX;
    private float _targetX;
    private float _targetNoise;
    private float _nextSenseTime;
    private float _nextDecisionTime;
    private float _nextTargetNoiseTime;
    private float _nextTacticalRefreshTime;
    private float _nextSituationalJumpTime;
    private float _situationalJumpQueuedUntil;
    private float _attackFollowUpUntil;
    private float _pressureCommittedUntil;
    private float _stationaryContestSince = float.PositiveInfinity;
    private float _bodyBlockDecisionLockedUntil;
    private float _stateCommittedUntil;
    private float _nextActionTime;
    private float _nextJumpTime;
    private float _lastJumpCommandTime = float.NegativeInfinity;
    private Vector3 _interceptPoint;
    private float _interceptTime;
    private float _tacticalAggression = 0.5f;
    private float _tacticalPatience = 0.5f;
    private bool _aerialActionResolved;
    private bool _interceptRequiresJump;
    private bool _needsSafeBallCrossing;
    private bool _situationalJumpQueued;
    private bool _bodyBlockOpportunityActive;
    private bool _bodyBlockCommitted;

    public FootballBotState State => _state;
    public FootballBotAerialAction PlannedAerialAction => _plannedAerialAction;
    public Vector3 InterceptPoint => _interceptPoint;

    public void Configure(FootballTeamSide side, FootballBall ball, FootballPlayerController opponent)
    {
        _side = side;
        _attackDirection = side == FootballTeamSide.Left ? 1 : -1;
        _ball = ball;
        _opponent = opponent;
        ResolveReferences();
        ResolveGoalPositions();
        _configured = _controller != null && _body != null && _ball != null;
    }

    private void Awake()
    {
        _random = CreateRandom();
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResetThinking();
    }

    private void OnDisable()
    {
        StopMoving();
        _observations.Clear();
    }

    private void FixedUpdate()
    {
        if (!_configured)
        {
            ResolveFallbackConfiguration();

            if (!_configured)
                return;
        }

        if (!CanPlay())
        {
            if (_state != FootballBotState.Idle || _observations.Count > 0)
                ResetThinking();

            StopMoving();
            return;
        }

        UpdatePerception();

        if (!_hasPerceivedWorld)
        {
            StopMoving();
            return;
        }

        if (Time.time >= _nextDecisionTime)
        {
            _nextDecisionTime = Time.time + _decisionInterval;
            EvaluateDecision();
        }

        DriveTowardsTarget();
    }

    private bool CanPlay()
    {
        return _matchController == null || _matchController.State == FootballMatchState.Running;
    }

    private void UpdatePerception()
    {
        if (Time.time >= _nextSenseTime)
        {
            _nextSenseTime = Time.time + _decisionInterval;
            _observations.Enqueue(new Observation(
                Time.time + _reactionDelay,
                _ball.transform.position,
                _ball.LinearVelocity,
                _opponent != null ? _opponent.transform.position : Vector3.zero
            ));

            while (_observations.Count > 8)
                _observations.Dequeue();
        }

        while (_observations.Count > 0 && _observations.Peek().AvailableAt <= Time.time)
        {
            _perceivedWorld = AddPerceptionError(_observations.Dequeue());
            _hasPerceivedWorld = true;
        }
    }

    private Observation AddPerceptionError(Observation observation)
    {
        Vector3 positionNoise = new Vector3(
            NextRange(-_positionError, _positionError),
            NextRange(-_positionError * 0.65f, _positionError * 0.65f),
            0f
        );
        Vector3 velocityNoise = new Vector3(
            NextRange(-_positionError, _positionError),
            NextRange(-_positionError, _positionError),
            0f
        );

        return new Observation(
            observation.AvailableAt,
            observation.BallPosition + positionNoise,
            observation.BallVelocity + velocityNoise,
            observation.OpponentPosition
        );
    }

    private void EvaluateDecision()
    {
        RefreshTacticalProfile();
        BuildBallPrediction();

        bool hasIntercept = TryFindIntercept(out Vector3 interceptPoint, out float interceptTime, out bool requiresJump);
        bool hasBodyBlockOpportunity = TryFindBodyBlockOpportunity(
            out Vector3 bodyBlockPoint,
            out float bodyBlockTime
        );

        bool bodyBlockSelected = UpdateBodyBlockDecision(hasBodyBlockOpportunity);

        if (bodyBlockSelected)
        {
            hasIntercept = true;
            interceptPoint = bodyBlockPoint;
            interceptTime = bodyBlockTime;
            requiresJump = true;
        }

        _interceptPoint = hasIntercept ? interceptPoint : _perceivedWorld.BallPosition;
        _interceptTime = hasIntercept ? interceptTime : _predictionHorizon;
        _interceptRequiresJump = hasIntercept && requiresJump;

        float selfAttackX = ToAttackSpace(transform.position.x);
        float ballAttackX = ToAttackSpace(_perceivedWorld.BallPosition.x);
        float ballAttackVelocity = _perceivedWorld.BallVelocity.x * _attackDirection;
        float ownGoalAttackX = ToAttackSpace(_ownGoalX);
        float opponentDistance = _opponent != null
            ? Mathf.Abs(_perceivedWorld.BallPosition.x - _perceivedWorld.OpponentPosition.x)
            : float.MaxValue;
        float opponentArrivalTime = opponentDistance / _assumedMoveSpeed;
        bool opponentClearlyCloser = opponentArrivalTime + 0.14f < _interceptTime;
        bool onOwnSide = ballAttackX < -0.5f;
        bool movingTowardsOwnGoal = ballAttackVelocity < -1.25f;
        UpdateStationaryContest(opponentClearlyCloser);
        bool continueAttack = Time.time < _attackFollowUpUntil;
        bool pressureIsCommitted = Time.time < _pressureCommittedUntil;
        bool stalledContest = !float.IsPositiveInfinity(_stationaryContestSince) &&
            Time.time - _stationaryContestSince >= _stalledBallPressDelay;
        bool forcePressure = continueAttack || pressureIsCommitted || stalledContest;
        bool ballNearOwnGoal = ballAttackX <= ownGoalAttackX + _ownGoalDangerDistance;
        bool botIsGoalSideOfBall = selfAttackX <= ballAttackX - 0.32f;
        _needsSafeBallCrossing = ballNearOwnGoal && !botIsGoalSideOfBall;
        bool ballIsHigh = _perceivedWorld.BallPosition.y - transform.position.y >
            _standingInteractionHeight - 0.35f;
        bool safeToWaitForHighBall = ballIsHigh &&
            !movingTowardsOwnGoal &&
            ballAttackX > ownGoalAttackX + 3.5f;
        bool waitForDrop = safeToWaitForHighBall &&
            hasIntercept &&
            _interceptTime > 0.36f &&
            Mathf.Abs(_interceptPoint.x - transform.position.x) < 1.55f &&
            _tacticalPatience >= 0.52f;
        bool guardSpace = opponentClearlyCloser &&
            !forcePressure &&
            (onOwnSide || _tacticalAggression < 0.57f);
        bool pressureOpponent = opponentClearlyCloser &&
            hasIntercept &&
            !requiresJump &&
            (forcePressure || _tacticalAggression >= 0.57f);
        bool emergency = ballNearOwnGoal;

        FootballBotState requestedState;
        float requestedTargetAttackX;

        if (emergency)
        {
            if (_needsSafeBallCrossing)
            {
                requestedState = FootballBotState.GoalLineRecovery;
                requestedTargetAttackX = Mathf.Max(
                    ownGoalAttackX + 0.45f,
                    ballAttackX - _goalSideSetupDistance
                );
            }
            else
            {
                requestedState = FootballBotState.EmergencyClear;
                requestedTargetAttackX = ToAttackSpace(_interceptPoint.x);
            }
        }
        else if (bodyBlockSelected && hasIntercept)
        {
            requestedState = FootballBotState.AerialContest;
            requestedTargetAttackX = ToAttackSpace(interceptPoint.x);
        }
        else if (guardSpace)
        {
            requestedState = FootballBotState.Guard;
            float predictedBallAttackX = ToAttackSpace(_interceptPoint.x);
            requestedTargetAttackX = Mathf.Clamp(
                Mathf.Lerp(ownGoalAttackX + 0.9f, predictedBallAttackX, 0.42f),
                ownGoalAttackX + 0.75f,
                -0.25f
            );
        }
        else if (waitForDrop)
        {
            requestedState = FootballBotState.Hold;
            float predictedBallAttackX = ToAttackSpace(_interceptPoint.x);
            bool alreadyUnderBall = Mathf.Abs(_perceivedWorld.BallPosition.x - transform.position.x) <=
                _overheadHoldWidth * 1.4f;
            requestedTargetAttackX = alreadyUnderBall
                ? selfAttackX
                : predictedBallAttackX - 0.22f;
        }
        else if (pressureOpponent)
        {
            requestedState = FootballBotState.Pressure;
            requestedTargetAttackX = ToAttackSpace(interceptPoint.x);
        }
        else if (hasIntercept && requiresJump)
        {
            requestedState = FootballBotState.AerialContest;
            requestedTargetAttackX = ToAttackSpace(interceptPoint.x);
        }
        else if (selfAttackX > ballAttackX - 0.35f)
        {
            requestedState = FootballBotState.AttackSetup;
            requestedTargetAttackX = ballAttackX - _attackSetupDistance;
        }
        else if (hasIntercept)
        {
            requestedState = FootballBotState.Intercept;
            requestedTargetAttackX = ToAttackSpace(interceptPoint.x);
        }
        else
        {
            requestedState = FootballBotState.Recover;
            requestedTargetAttackX = Mathf.Clamp(
                Mathf.Lerp(ownGoalAttackX + 1.1f, ballAttackX, 0.48f),
                ownGoalAttackX + 0.8f,
                0f
            );
        }

        FootballBotState previousState = _state;
        ApplyIntent(requestedState, FromAttackSpace(requestedTargetAttackX), emergency);
        UpdateAerialActionPlan(previousState, bodyBlockSelected);
        PlanSituationalJump();
        PlanJump();
        TryBallAction(emergency);
    }

    private void UpdateStationaryContest(bool opponentClearlyCloser)
    {
        const float StationaryBallSpeed = 0.8f;
        bool ballIsNearlyStationary = _perceivedWorld.BallVelocity.sqrMagnitude <=
            StationaryBallSpeed * StationaryBallSpeed;

        if (opponentClearlyCloser && ballIsNearlyStationary)
        {
            if (float.IsPositiveInfinity(_stationaryContestSince))
                _stationaryContestSince = Time.time;

            return;
        }

        _stationaryContestSince = float.PositiveInfinity;
    }

    private bool TryFindBodyBlockOpportunity(out Vector3 point, out float time)
    {
        point = _perceivedWorld.BallPosition;
        time = _bodyBlockMaximumLeadTime;

        for (int i = 1; i < _predictionCount; i++)
        {
            PredictionPoint candidate = _prediction[i];

            if (candidate.Time > _bodyBlockMaximumLeadTime)
                break;

            float relativeHeight = candidate.Position.y - transform.position.y;

            if (relativeHeight < _bodyBlockMinimumHeight ||
                relativeHeight > _bodyBlockMaximumHeight)
            {
                continue;
            }

            float horizontalDistance = Mathf.Abs(candidate.Position.x - transform.position.x);
            float reachableDistance = 0.3f + _assumedMoveSpeed * candidate.Time;

            if (horizontalDistance > reachableDistance)
                continue;

            point = candidate.Position;
            time = candidate.Time;
            return true;
        }

        return false;
    }

    private bool UpdateBodyBlockDecision(bool hasOpportunity)
    {
        if (hasOpportunity)
        {
            if (!_bodyBlockOpportunityActive && Time.time >= _bodyBlockDecisionLockedUntil)
            {
                _bodyBlockOpportunityActive = true;
                _bodyBlockCommitted = Next01() <= _bodyBlockChance;
                _bodyBlockDecisionLockedUntil = Time.time + _bodyBlockMaximumLeadTime + 0.45f;
            }

            return _bodyBlockCommitted;
        }

        if (Time.time >= _bodyBlockDecisionLockedUntil)
        {
            _bodyBlockOpportunityActive = false;
            _bodyBlockCommitted = false;
        }

        return false;
    }

    private void PlanSituationalJump()
    {
        if (!_controller.IsGrounded ||
            Time.time < _nextSituationalJumpTime ||
            _state == FootballBotState.AerialContest)
        {
            return;
        }

        bool shouldCrossBallSafely = _state == FootballBotState.GoalLineRecovery &&
            _needsSafeBallCrossing &&
            IsObstacleInPath(_perceivedWorld.BallPosition, 0.08f, _obstacleJumpDistance + 0.2f);
        bool shouldJumpOpponent = _opponent != null &&
            IsObstacleInPath(
                _perceivedWorld.OpponentPosition,
                _minimumObstacleJumpDistance,
                _obstacleJumpDistance
            );

        if (!shouldCrossBallSafely && !shouldJumpOpponent)
            return;

        _controller.QueueExternalJump();
        _airJumpCommands = 1;
        _lastJumpCommandTime = Time.time;
        _nextJumpTime = Time.time + 0.24f;
        _nextSituationalJumpTime = Time.time + 0.85f;
        _situationalJumpQueuedUntil = Time.time + 0.32f;
        _situationalJumpQueued = true;
    }

    private bool IsObstacleInPath(Vector3 obstaclePosition, float minimumDistance, float maximumDistance)
    {
        Vector3 selfPosition = transform.position;
        float targetDelta = _targetX - selfPosition.x;
        float obstacleDelta = obstaclePosition.x - selfPosition.x;
        float horizontalDistance = Mathf.Abs(obstacleDelta);
        float verticalDistance = Mathf.Abs(obstaclePosition.y - selfPosition.y);

        if (Mathf.Abs(targetDelta) < _targetDeadZone ||
            horizontalDistance < minimumDistance ||
            horizontalDistance > maximumDistance ||
            verticalDistance > 1.25f)
        {
            return false;
        }

        bool sameDirection = Mathf.Sign(targetDelta) == Mathf.Sign(obstacleDelta);
        bool obstacleBeforeTarget = horizontalDistance <= Mathf.Abs(targetDelta) + 0.12f;
        return sameDirection && obstacleBeforeTarget;
    }

    private void RefreshTacticalProfile()
    {
        if (Time.time < _nextTacticalRefreshTime)
            return;

        _nextTacticalRefreshTime = Time.time + NextRange(2.4f, 3.5f);
        _tacticalAggression = NextRange(0.28f, 0.82f);
        _tacticalPatience = NextRange(0.3f, 0.86f);
    }

    private void ApplyIntent(FootballBotState requestedState, float requestedTargetX, bool forceTransition)
    {
        bool canChangeState = forceTransition || requestedState == _state || Time.time >= _stateCommittedUntil;

        if (!canChangeState)
            return;

        if (requestedState != _state)
        {
            _state = requestedState;
            _stateCommittedUntil = Time.time + _stateCommitTime;

            if (requestedState == FootballBotState.Pressure)
            {
                _pressureCommittedUntil = Mathf.Max(
                    _pressureCommittedUntil,
                    Time.time + _pressureCommitDuration
                );
            }
        }

        if (Time.time >= _nextTargetNoiseTime)
        {
            _nextTargetNoiseTime = Time.time + 0.55f;
            _targetNoise = NextRange(-_positionError * 0.5f, _positionError * 0.5f);
        }

        float minimumX = Mathf.Min(_ownGoalX, _opponentGoalX) + 0.45f;
        float maximumX = Mathf.Max(_ownGoalX, _opponentGoalX) - 0.45f;
        _targetX = Mathf.Clamp(requestedTargetX + _targetNoise, minimumX, maximumX);
    }

    private void DriveTowardsTarget()
    {
        if (_situationalJumpQueued)
        {
            if (!_controller.IsGrounded || Time.time >= _situationalJumpQueuedUntil)
            {
                _situationalJumpQueued = false;
            }
            else
            {
                SetMoveInput(0f);
                return;
            }
        }

        if (TryDrivePlannedAerialAction(out float aerialInput))
        {
            SetMoveInput(aerialInput);
            return;
        }

        if (IsBallDirectlyOverhead())
        {
            SetMoveInput(0f);
            return;
        }

        float distance = _targetX - transform.position.x;
        float velocity = _body.linearVelocity.x;
        float input;

        if (Mathf.Abs(distance) <= _targetDeadZone)
        {
            input = 0f;
        }
        else
        {
            int desiredDirection = distance > 0f ? 1 : -1;

            if (_committedMoveDirection == 0 ||
                desiredDirection == _committedMoveDirection ||
                Mathf.Abs(distance) >= _turnThreshold)
            {
                _committedMoveDirection = desiredDirection;
            }

            if (desiredDirection != _committedMoveDirection)
            {
                input = 0f;
            }
            else
            {
                float direction = _committedMoveDirection;
                float stoppingDistance = velocity * velocity / (2f * 65f);
                bool movingTowardsTarget = Mathf.Abs(velocity) > 0.05f && Mathf.Sign(velocity) == direction;
                input = movingTowardsTarget && stoppingDistance + _targetDeadZone >= Mathf.Abs(distance)
                    ? 0f
                    : direction;
            }
        }

        SetMoveInput(input);
    }

    private bool TryDrivePlannedAerialAction(out float input)
    {
        input = 0f;

        if (_state != FootballBotState.AerialContest)
            return false;

        if (_plannedAerialAction == FootballBotAerialAction.BicycleKick)
        {
            if (!_controller.IsGrounded)
            {
                input = _attackDirection;
                return true;
            }

            if (_controller.FacingDirection != -_attackDirection)
            {
                input = -_attackDirection;
                return true;
            }

            input = _interceptTime <= 0.58f
                ? _attackDirection
                : 0f;
            return true;
        }

        if (_plannedAerialAction == FootballBotAerialAction.Header &&
            IsBallDirectlyOverhead() &&
            _controller.IsGrounded &&
            _controller.FacingDirection != _attackDirection)
        {
            input = _attackDirection;
            return true;
        }

        return false;
    }

    private bool IsBallDirectlyOverhead()
    {
        if (!_hasPerceivedWorld)
            return false;

        Vector3 relativeBall = _perceivedWorld.BallPosition - transform.position;
        return relativeBall.y >= _overheadHoldHeight &&
            Mathf.Abs(relativeBall.x) <= _overheadHoldWidth;
    }

    private void SetMoveInput(float input)
    {
        _controller.SetExternalMoveInput(new Vector2(input, 0f));
    }

    private void UpdateAerialActionPlan(FootballBotState previousState, bool bodyBlockSelected)
    {
        if (_state != FootballBotState.AerialContest)
        {
            _plannedAerialAction = FootballBotAerialAction.None;
            _aerialActionResolved = false;
            return;
        }

        if (_aerialActionResolved)
            return;

        if (previousState == FootballBotState.AerialContest &&
            _plannedAerialAction != FootballBotAerialAction.None)
        {
            return;
        }

        if (bodyBlockSelected)
        {
            _plannedAerialAction = FootballBotAerialAction.BodyBlock;
            return;
        }

        bool bicycleKickHasTime = _interceptTime >= 0.2f && _interceptTime <= 0.9f;
        bool canPlanBicycleKick = _bicycleKicker != null &&
            bicycleKickHasTime &&
            Next01() <= _bicycleKickChance;

        _plannedAerialAction = canPlanBicycleKick
            ? FootballBotAerialAction.BicycleKick
            : FootballBotAerialAction.Header;
    }

    private void PlanJump()
    {
        bool stateAllowsJump = _state == FootballBotState.AerialContest ||
            _state == FootballBotState.EmergencyClear ||
            _state == FootballBotState.GoalLineRecovery;

        if (!stateAllowsJump || !_interceptRequiresJump || Time.time < _nextJumpTime)
            return;

        if (_plannedAerialAction == FootballBotAerialAction.BicycleKick &&
            _controller.IsGrounded &&
            _controller.FacingDirection != -_attackDirection)
        {
            return;
        }

        float relativeHeight = _interceptPoint.y - transform.position.y;

        if (_controller.IsGrounded && _interceptTime <= 0.58f)
        {
            _controller.QueueExternalJump();
            _airJumpCommands = 1;
            _lastJumpCommandTime = Time.time;
            _nextJumpTime = Time.time + 0.22f;
            return;
        }

        if (_controller.IsGrounded && Time.time - _lastJumpCommandTime > 0.35f)
            _airJumpCommands = 0;

        if (_controller.IsGrounded || _airJumpCommands != 1 || _interceptTime > 0.28f || relativeHeight < 3.6f)
            return;

        _airJumpCommands = 2;
        _nextJumpTime = Time.time + 0.3f;

        if (Next01() <= _doubleJumpChance)
        {
            _controller.QueueExternalJump();
            _lastJumpCommandTime = Time.time;
        }
    }

    private void TryBallAction(bool emergency)
    {
        if (Time.time < _nextActionTime || _ball == null)
            return;

        Vector3 ballPosition = _ball.transform.position;
        bool facingAttack = _controller.FacingDirection == _attackDirection;
        bool ballNotBehind = (ballPosition.x - transform.position.x) * _attackDirection >= -0.2f;
        bool acted = false;
        bool bicycleKickPlanned = _state == FootballBotState.AerialContest &&
            _plannedAerialAction == FootballBotAerialAction.BicycleKick;
        bool headerPlanned = _state == FootballBotState.AerialContest &&
            _plannedAerialAction == FootballBotAerialAction.Header;
        bool bodyBlockPlanned = _state == FootballBotState.AerialContest &&
            _plannedAerialAction == FootballBotAerialAction.BodyBlock;

        if (bicycleKickPlanned &&
            _bicycleKicker != null &&
            _controller.FacingDirection == -_attackDirection &&
            _bicycleKicker.CanAttemptBicycleKick())
        {
            acted = _bicycleKicker.TryBicycleKick();
        }

        if (!bicycleKickPlanned &&
            !acted &&
            facingAttack &&
            ballNotBehind &&
            _header != null &&
            (headerPlanned || _plannedAerialAction == FootballBotAerialAction.None))
        {
            Vector3 headerPoint = _ball.GetClosestInteractionPoint(_header.ZoneOrigin);
            bool inHeaderZone = Vector3.Distance(headerPoint, _header.ZoneOrigin) <= _header.ZoneRange + 0.08f;
            float kickZoneY = _kicker != null ? _kicker.ZoneOrigin.y : transform.position.y + 0.6f;
            bool aboveKickZone = ballPosition.y > kickZoneY + 0.45f;

            if (inHeaderZone && aboveKickZone)
                acted = _header.TryHeader();
        }

        if (!bicycleKickPlanned &&
            !bodyBlockPlanned &&
            !headerPlanned &&
            !acted &&
            facingAttack &&
            ballNotBehind &&
            _kicker != null)
        {
            Vector3 kickPoint = _ball.GetClosestInteractionPoint(_kicker.ZoneOrigin);
            bool inKickZone = Vector3.Distance(kickPoint, _kicker.ZoneOrigin) <= _kicker.ZoneRange + 0.08f;

            if (inKickZone)
                acted = _kicker.TryKick();
        }

        if (acted && _plannedAerialAction != FootballBotAerialAction.None)
        {
            _plannedAerialAction = FootballBotAerialAction.None;
            _aerialActionResolved = true;
        }

        if (acted && !emergency)
        {
            _attackFollowUpUntil = Time.time + _attackFollowUpDuration;
            _pressureCommittedUntil = Mathf.Max(
                _pressureCommittedUntil,
                Time.time + _pressureCommitDuration
            );
        }

        float baseDelay = acted ? 0.24f : emergency ? 0.07f : 0.1f;
        _nextActionTime = Time.time + Mathf.Max(0.04f, baseDelay + NextRange(-_actionTimingError, _actionTimingError));
    }

    private void BuildBallPrediction()
    {
        Vector3 position = _perceivedWorld.BallPosition;
        Vector3 velocity = _perceivedWorld.BallVelocity;
        Vector3 gravityDirection = Physics.gravity.sqrMagnitude > Mathf.Epsilon
            ? Physics.gravity.normalized
            : Vector3.down;
        Vector3 gravity = gravityDirection * GameParameterSessionValues.GetValue(GameParameterId.BallGravity);
        float bounce = Mathf.Clamp01(GameParameterSessionValues.GetValue(GameParameterId.BallBounce));
        float radius = GetBallRadius();
        int collisionMask = ~((1 << gameObject.layer) | (1 << _ball.gameObject.layer));
        PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();

        _predictionCount = 0;
        AddPredictionPoint(position, 0f);

        for (float time = _predictionStep; time <= _predictionHorizon + 0.001f && _predictionCount < PredictionCapacity; time += _predictionStep)
        {
            velocity += gravity * _predictionStep;
            Vector3 displacement = velocity * _predictionStep;
            float distance = displacement.magnitude;

            if (distance > 0.0001f && physicsScene.SphereCast(
                position,
                radius,
                displacement / distance,
                out RaycastHit hit,
                distance + 0.01f,
                collisionMask,
                QueryTriggerInteraction.Ignore))
            {
                position += displacement.normalized * Mathf.Max(0f, hit.distance - 0.01f);
                Vector3 normalVelocity = Vector3.Project(velocity, hit.normal);
                Vector3 tangentVelocity = velocity - normalVelocity;

                if (Vector3.Dot(velocity, hit.normal) < 0f)
                    velocity = tangentVelocity * 0.98f - normalVelocity * bounce;

                position += hit.normal * 0.012f;
            }
            else
            {
                position += displacement;
            }

            position.z = 0f;
            velocity.z = 0f;
            AddPredictionPoint(position, time);
        }
    }

    private bool TryFindIntercept(out Vector3 point, out float time, out bool requiresJump)
    {
        point = _perceivedWorld.BallPosition;
        time = _predictionHorizon;
        requiresJump = false;

        float bestScore = float.MaxValue;
        float playerGravity = Mathf.Max(0.1f, GameParameterSessionValues.GetValue(GameParameterId.PlayerGravity));
        float jumpSpeed = Mathf.Max(0f, GameParameterSessionValues.GetValue(GameParameterId.PlayerJump));

        for (int i = 1; i < _predictionCount; i++)
        {
            PredictionPoint candidate = _prediction[i];
            float horizontalDistance = Mathf.Abs(candidate.Position.x - transform.position.x);
            float reachableDistance = 0.35f + _assumedMoveSpeed * candidate.Time;

            if (horizontalDistance > reachableDistance)
                continue;

            float jumpLead = Mathf.Min(candidate.Time, jumpSpeed / playerGravity);
            float jumpElevation = jumpSpeed * jumpLead - 0.5f * playerGravity * jumpLead * jumpLead;
            float relativeHeight = candidate.Position.y - transform.position.y;
            bool candidateRequiresJump = relativeHeight > _standingInteractionHeight;
            float reachableHeight = _standingInteractionHeight + Mathf.Max(0f, jumpElevation);

            if (relativeHeight < -0.65f || relativeHeight > reachableHeight)
                continue;

            float score = candidate.Time + (candidateRequiresJump ? 0.24f : 0f);

            if (score >= bestScore)
                continue;

            bestScore = score;
            point = candidate.Position;
            time = candidate.Time;
            requiresJump = candidateRequiresJump;
        }

        return bestScore < float.MaxValue;
    }

    private void ResolveFallbackConfiguration()
    {
        ResolveReferences();

        if (_ball == null)
            _ball = FindAnyObjectByType<FootballBall>();

        if (_opponent == null)
        {
            FootballPlayerController[] players = FindObjectsByType<FootballPlayerController>(FindObjectsInactive.Exclude);

            foreach (FootballPlayerController player in players)
            {
                if (player != _controller)
                {
                    _opponent = player;
                    break;
                }
            }
        }

        _side = transform.position.x < 0f ? FootballTeamSide.Left : FootballTeamSide.Right;
        _attackDirection = _side == FootballTeamSide.Left ? 1 : -1;
        ResolveReferences();
        ResolveGoalPositions();
        _configured = _controller != null && _body != null && _ball != null;
    }

    private void ResolveReferences()
    {
        if (_controller == null)
            _controller = GetComponent<FootballPlayerController>();

        if (_body == null)
            _body = GetComponent<Rigidbody>();

        if (_kicker == null)
            _kicker = GetComponent<FootballBallKicker>();

        if (_header == null)
            _header = GetComponent<FootballBallHeader>();

        if (_bicycleKicker == null)
            _bicycleKicker = GetComponent<FootballBallBicycleKicker>();

        if (_ball != null && _ballCollider == null)
            _ballCollider = _ball.GetComponent<SphereCollider>();

        if (_matchController == null)
            _matchController = FindAnyObjectByType<FootballMatchController>();

        if (_random == null)
            _random = CreateRandom();
    }

    private void ResolveGoalPositions()
    {
        _ownGoalX = _side == FootballTeamSide.Left ? DefaultLeftGoalX : DefaultRightGoalX;
        _opponentGoalX = _side == FootballTeamSide.Left ? DefaultRightGoalX : DefaultLeftGoalX;

        FootballGoalZone[] goalZones = FindObjectsByType<FootballGoalZone>();

        foreach (FootballGoalZone goalZone in goalZones)
        {
            if (goalZone == null)
                continue;

            Collider goalCollider = goalZone.GetComponent<Collider>();
            float goalX = goalCollider != null ? goalCollider.bounds.center.x : goalZone.transform.position.x;

            if (goalZone.DefendingSide == _side)
                _ownGoalX = goalX;
            else
                _opponentGoalX = goalX;
        }
    }

    private void ResetThinking()
    {
        _state = FootballBotState.Idle;
        _targetX = transform.position.x;
        _hasPerceivedWorld = false;
        _observations.Clear();
        _nextSenseTime = Time.time;
        _nextDecisionTime = Time.time;
        _stateCommittedUntil = Time.time;
        _airJumpCommands = 0;
        _committedMoveDirection = 0;
        _plannedAerialAction = FootballBotAerialAction.None;
        _aerialActionResolved = false;
        _needsSafeBallCrossing = false;
        _situationalJumpQueued = false;
        _attackFollowUpUntil = Time.time;
        _pressureCommittedUntil = Time.time;
        _stationaryContestSince = float.PositiveInfinity;
        _bodyBlockOpportunityActive = false;
        _bodyBlockCommitted = false;
        _bodyBlockDecisionLockedUntil = Time.time;
        _nextTacticalRefreshTime = Time.time;
    }

    private void StopMoving()
    {
        _controller?.SetExternalMoveInput(Vector2.zero);
    }

    private float GetBallRadius()
    {
        if (_ballCollider == null)
            return 0.25f;

        Vector3 scale = _ballCollider.transform.lossyScale;
        return _ballCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
    }

    private void AddPredictionPoint(Vector3 position, float time)
    {
        if (_predictionCount >= _prediction.Length)
            return;

        _prediction[_predictionCount++] = new PredictionPoint(position, time);
    }

    private float ToAttackSpace(float worldX)
    {
        return worldX * _attackDirection;
    }

    private float FromAttackSpace(float attackX)
    {
        return attackX * _attackDirection;
    }

    private float Next01()
    {
        return (float)_random.NextDouble();
    }

    private System.Random CreateRandom()
    {
        int nameHash = string.IsNullOrEmpty(name) ? 0 : name.GetHashCode();
        return new System.Random(unchecked(Environment.TickCount ^ nameHash));
    }

    private float NextRange(float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, maximum, Next01());
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(new Vector3(_targetX, transform.position.y + 0.15f, 0f), 0.12f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_interceptPoint, 0.18f);
    }

    private readonly struct Observation
    {
        public Observation(float availableAt, Vector3 ballPosition, Vector3 ballVelocity, Vector3 opponentPosition)
        {
            AvailableAt = availableAt;
            BallPosition = ballPosition;
            BallVelocity = ballVelocity;
            OpponentPosition = opponentPosition;
        }

        public float AvailableAt { get; }
        public Vector3 BallPosition { get; }
        public Vector3 BallVelocity { get; }
        public Vector3 OpponentPosition { get; }
    }

    private readonly struct PredictionPoint
    {
        public PredictionPoint(Vector3 position, float time)
        {
            Position = position;
            Time = time;
        }

        public Vector3 Position { get; }
        public float Time { get; }
    }
}
