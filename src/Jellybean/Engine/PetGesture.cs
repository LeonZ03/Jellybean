using System.Windows;

namespace Jellybean.Engine;

public enum PetGesture
{
    Listen, Sniff, ShiftWeight, Groom, Yawn, DreamTwitch, RollOver, CurlUp,
    TestLedge, PeekAgain, ReachToy, MissedPounce, Nuzzle, Knead
}

public sealed partial class PetEngine
{
    private Point? _cursorPosition;
    private Point _sniffAnchor, _petHead, _pounceTarget;
    private double _sniffDwell, _nextSniff, _nextIdleGesture = 6, _nextLedgeTest, _nextGroom, _nextRestGesture, _nextReach;
    private double _gestureAge, _gestureDuration;
    private Action? _afterGesture;
    private PetGesture? _restPreview;
    private bool _sniffLatched, _poseGesturePlayed, _dreamPlayed, _previewPeek, _seatedGrooming;
    private readonly HashSet<(nint Window, int Side)> _testedLedges = new();

    public PetGesture? Gesture { get; private set; }
    public double GestureProgress => Gesture is null ? 0 : Math.Clamp(_gestureAge / _gestureDuration, 0, 1);
    public int GestureDirection { get; private set; } = -1;

    private int ToyDirection => ToyPosition.X < CenterX ? -1 : 1;
    private Point StandingNose => new(CenterX + Facing * 79 * _settings.Scale, FeetY - 86 * _settings.Scale);

    // At the bottom of the duck, the clip line is y=62 in the peek sprite.
    private double PeekDip => Gesture == PetGesture.PeekAgain
        ? .59375 * Smooth(Math.Clamp(GestureProgress / .27, 0, 1))
                   * Smooth(Math.Clamp((1 - GestureProgress) / .42, 0, 1))
        : 0;

    public void UpdateCursor(Point position, bool freeToInteract)
    {
        ToyPosition = position;
        _cursorPosition = freeToInteract ? position : null;
    }

    public void PreviewGesture(PetGesture gesture) => GroundAction(() =>
    {
        Interact();
        StopToy();
        switch (gesture)
        {
            case PetGesture.Groom:
                BeginGrooming();
                return;
            case PetGesture.DreamTwitch:
            case PetGesture.RollOver:
            case PetGesture.CurlUp:
            case PetGesture.Knead:
                _restOnWindow = gesture == PetGesture.DreamTwitch && _support is { Window: not 0 };
                SetState(PetState.LieDown);
                _restPreview = gesture;
                return;
            case PetGesture.PeekAgain:
                BeginPeek();
                _poseGesturePlayed = true;
                // Bring the head into view before starting the duck.
                _previewPeek = true;
                return;
            case PetGesture.ReachToy:
                SetState(PetState.HuntAim);
                break;
            case PetGesture.MissedPounce:
                SetState(PetState.Catch);
                break;
            case PetGesture.Nuzzle:
                SetState(PetState.Pet, 2.1);
                _poseGesturePlayed = true;
                break;
            default:
                Idle();
                break;
        }
        Action? after = gesture is PetGesture.ReachToy or PetGesture.MissedPounce ? Idle : null;
        StartGesture(gesture, gesture == PetGesture.MissedPounce ? -Facing : Facing, after);
    });

    private void StartGesture(PetGesture gesture, int? direction = null, Action? after = null)
    {
        Gesture = gesture;
        GestureDirection = direction ?? Facing;
        _gestureAge = 0;
        _gestureDuration = gesture switch
        {
            PetGesture.Listen => 1.15,
            PetGesture.Sniff => .95,
            PetGesture.ShiftWeight => 1.1,
            PetGesture.Groom => 2.8,
            PetGesture.Yawn => 1.5,
            PetGesture.DreamTwitch => .75,
            PetGesture.RollOver or PetGesture.CurlUp => 4.5,
            PetGesture.TestLedge => 1.25,
            PetGesture.PeekAgain or PetGesture.Nuzzle => 1.15,
            PetGesture.ReachToy => .62,
            PetGesture.MissedPounce => 1,
            PetGesture.Knead => 2.5,
            _ => throw new ArgumentOutOfRangeException(nameof(gesture))
        };
        _afterGesture = after;
    }

    private void ClearGesture()
    {
        Gesture = null;
        _afterGesture = null;
    }

    private bool TickGestures(double dt)
    {
        if (Gesture is not null)
        {
            _gestureAge += dt;
            if (State == PetState.Peek) UpdatePeekPosition();
            if (_gestureAge < _gestureDuration) return true;
            var after = _afterGesture;
            ClearGesture();
            if (State == PetState.Idle) _nextAction = Math.Max(_nextAction, Age + 1);
            after?.Invoke();
            return true;
        }

        var sniffPose = State == PetState.Idle || State == PetState.Perch && !_seatedGrooming;
        if (!sniffPose || _angle is not null || _toyActive || !_cursorPosition.HasValue)
        {
            _sniffDwell = 0;
            _sniffLatched = false;
        }
        else if (TrySniff(dt)) return true;

        if (State == PetState.Peek && Age >= .55 && _previewPeek)
        {
            _previewPeek = false;
            StartGesture(PetGesture.PeekAgain);
            return true;
        }

        if (State == PetState.Peek && !_poseGesturePlayed && Age >= .55 &&
            CursorNear(new Point(CenterX - 12 * _settings.Scale, FeetY - 90 * _settings.Scale), 47))
        {
            _poseGesturePlayed = true;
            _actionDuration = Math.Max(_actionDuration, Age + 2);
            StartGesture(PetGesture.PeekAgain);
        }
        else if (State == PetState.Pet && Hearts && !_poseGesturePlayed && Age >= .22 &&
                 (CursorNear(_petHead, 39) ||
                  CursorNear(new Point(CenterX - 14 * _settings.Scale, FeetY - 83 * _settings.Scale), 36)))
        {
            _poseGesturePlayed = true;
            var direction = _cursorPosition!.Value.X < CenterX ? -1 : 1;
            _actionDuration = Math.Max(_actionDuration, Age + 1.3);
            StartGesture(PetGesture.Nuzzle, direction);
        }
        else if (State == PetState.Perch && _seatedGrooming && Age >= .55 && !_poseGesturePlayed)
        {
            _poseGesturePlayed = true;
            _nextGroom = AliveTime + _random.Next(45, 75);
            StartGesture(PetGesture.Groom, -1);
        }
        else if (_settings.Autonomous && !_toyActive && _angle is null)
        {
            TickRelaxedGestures();
        }
        return Gesture is not null;
    }

    private bool TrySniff(double dt)
    {
        var position = _cursorPosition!.Value;
        var nose = State == PetState.Perch
            ? new Point(CenterX + 11 * _settings.Scale, FeetY - 88 * _settings.Scale)
            : StandingNose;
        if (!CursorNear(nose, 31))
        {
            _sniffDwell = 0;
            _sniffLatched = false;
            return false;
        }
        if (_sniffLatched || AliveTime < _nextSniff) return false;
        if (_sniffDwell == 0 || (position - _sniffAnchor).Length > 7 * _settings.Scale)
        {
            _sniffAnchor = position;
            _sniffDwell = 0;
        }
        _sniffDwell += dt;
        if (_sniffDwell < .45) return false;
        _sniffLatched = true;
        _sniffDwell = 0;
        _nextSniff = AliveTime + 8;
        StartGesture(PetGesture.Sniff);
        return true;
    }

    private bool CursorNear(Point point, double radius) =>
        _cursorPosition is { } cursor && (cursor - point).Length < radius * _settings.Scale;

    private void TickRelaxedGestures()
    {
        var alertPose = State == PetState.Idle || State == PetState.Perch && !_seatedGrooming;
        if (alertPose && Age >= 2.3 && AliveTime >= _nextIdleGesture)
        {
            _nextIdleGesture = AliveTime + _random.Next(18, 33);
            if (State == PetState.Perch || _random.Next(2) == 0)
                StartGesture(PetGesture.Listen, _random.Next(2) == 0 ? -1 : 1);
            else StartGesture(PetGesture.ShiftWeight);
        }
        else if (State == PetState.Doze)
        {
            if (!_restOnWindow && !_poseGesturePlayed && Age >= 2.4 && AliveTime >= _nextRestGesture)
            {
                _poseGesturePlayed = true;
                _nextRestGesture = AliveTime + _random.Next(26, 45);
                var hour = DateTime.Now.Hour;
                var gesture = (hour >= 22 || hour < 6 || AliveTime - _lastInteraction >= 120) && _random.Next(3) != 0
                    ? PetGesture.CurlUp
                    : _random.Next(2) == 0 ? PetGesture.RollOver : PetGesture.Knead;
                StartGesture(gesture);
            }
            else if (!_dreamPlayed && Age >= 6.5)
            {
                _dreamPlayed = true;
                if (_random.Next(2) == 0) StartGesture(PetGesture.DreamTwitch);
            }
        }
    }

    private void BeginGrooming()
    {
        SetState(PetState.Perch, 4.1);
        _seatedGrooming = true;
    }

    private void TryTestLedge()
    {
        if (!_settings.Autonomous || _toyActive || AliveTime < _nextLedgeTest ||
            _support is not { Window: not 0 } edge) return;
        var distance = Facing < 0 ? CenterX - edge.Left : edge.Right - CenterX;
        if (distance > 100 * _settings.Scale) return;
        if (_testedLedges.Count >= 64) _testedLedges.Clear();
        if (!_testedLedges.Add((edge.Window, Facing))) return;
        _nextLedgeTest = AliveTime + 20;
        StartGesture(PetGesture.TestLedge);
    }

    private bool TryReachToy()
    {
        if (AliveTime < _nextReach) return false;
        var nose = new Point(CenterX + Facing * 36 * _settings.Scale, FeetY - 31 * _settings.Scale);
        if ((ToyPosition - nose).Length > 29 * _settings.Scale) return false;
        _nextReach = AliveTime + 4;
        SetState(PetState.HuntAim);
        StartGesture(PetGesture.ReachToy, Facing, AimAtToy);
        return true;
    }
}
