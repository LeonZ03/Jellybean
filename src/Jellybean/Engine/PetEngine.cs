using System.Windows;
using Jellybean.Services;

namespace Jellybean.Engine;

public enum PetState { Idle, Walk, LieDown, Doze, Rise, Perch, Pet, Stretch, Fall, Land, Drag, Pounce, Jump, Peek }

public sealed class PetEngine
{
    public const double WalkCycleSeconds = 1.2;
    private readonly double _leftStride, _rightStride;
    public double WalkStride => Facing < 0 ? _leftStride : _rightStride;
    private readonly Random _random = new();
    private readonly SurfaceProvider _surfaces;
    private readonly UserSettings _settings;
    private Surface? _support;
    private Point _windowOrigin;
    private double _velocityY, _fallStartY, _nextAction = 3, _refreshIn;
    private Point _jumpStart, _jumpEnd, _originalJumpEnd;
    private Surface _jumpTarget;
    private double _jumpDuration, _jumpVelocity, _actionDuration, _walkLength, _walkDuration, _walkDistance;
    private bool _toyActive, _restOnWindow, _dragLoop, _fallLoop;
    private double _toyLifetime, _peekGround, _effectRemaining;
    private string? _angle;
    private string _dragAnimation = "idle-left", _fallAnimation = "idle-left";
    private double _dragAge, _fallAge;
    private double _riseOffset;
    private Action? _afterRise;
    private double _lastInteraction, _nextAttention = 180;
    private nint _lastVisitedWindow;
    public double CenterX { get; private set; }
    public double FeetY { get; private set; }
    public PetState State { get; private set; } = PetState.Idle;
    public double Age { get; private set; }
    public double AliveTime { get; private set; }
    public int Facing { get; private set; } = -1;
    public Point ToyPosition { get; set; }
    public bool ToyActive => _toyActive;
    public string? Bubble { get; private set; }
    public double EffectRemaining => _effectRemaining;
    public bool Hearts { get; private set; }
    public double PeekFraction => State == PetState.Peek ? Smooth(Math.Min(1, Age / .5)) : 0;
    public Surface? Support => _support;
    public double BodyHeight => 128 * _settings.Scale;
    public Rect WorkArea => _surfaces.Desktop.WorkAreaAt(CenterX, FeetY);
    public bool Moving => State is PetState.Walk or PetState.Fall or PetState.Drag or PetState.Pounce or PetState.Jump;
    public bool LoopAnimation => State == PetState.Drag ? _dragLoop : State == PetState.Fall && Age < .14 ? _fallLoop :
        State is PetState.Idle or PetState.Walk or PetState.Doze or PetState.Perch or PetState.Peek;
    public double RenderAge => State switch
    {
        PetState.Walk => _walkDistance / WalkStride * WalkCycleSeconds,
        PetState.Drag => _dragAge,
        PetState.Rise => Age + _riseOffset,
        PetState.Fall when Age < .14 => _fallAge,
        PetState.Jump when Age >= .26 => 0,
        _ => Age
    };

    public PetEngine(SurfaceProvider surfaces, UserSettings settings, double walkStride=90, double? rightStride=null)
    {
        _surfaces = surfaces; _settings = settings; _leftStride=walkStride; _rightStride=rightStride??walkStride;
        var area = surfaces.Desktop.WorkAreas[0];
        CenterX = settings.CenterX ?? area.Right - 220; FeetY = settings.FeetY ?? area.Bottom;
        BringIntoView(); Drop();
    }

    public void Tick(double dt)
    {
        AliveTime += dt; Age += dt; _refreshIn -= dt;
        _effectRemaining = Math.Max(0, _effectRemaining - dt);
        if (_angle is not null && Age > 6) _angle = null;
        if (_effectRemaining == 0) { Bubble = null; Hearts = false; }
        if (_refreshIn <= 0) { _surfaces.Refresh(); _refreshIn = .4; }
        if (State == PetState.Drag) return;
        if (_support is null && State is not (PetState.Fall or PetState.Jump or PetState.Pounce)) Drop();
        if (State is not (PetState.Fall or PetState.Jump)) FollowSupport();
        if (State == PetState.Fall)
        {
            var previous = FeetY; _velocityY += 1250 * dt; FeetY += _velocityY * dt;
            if (Platforms.TryLand(_surfaces.Surfaces, CenterX, previous, FeetY, out var top)) LandAt(top, FeetY - _fallStartY > 12 || _velocityY > 180);
            else if (FeetY > WorkArea.Bottom) LandAt(WorkArea.Bottom, true);
            return;
        }
        if (State == PetState.Jump) { TickJump(); return; }
        if (_toyActive)
        {
            _toyLifetime -= dt;
            if (_toyLifetime <= 0) StopToy();
            else if (State == PetState.Idle && Age > .8) BeginPounce();
        }
        if (State == PetState.Walk) TickWalk();
        else if (State == PetState.Pounce)
        {
            var t = Math.Clamp((Age - .55) / .48, 0, 1);
            CenterX = _jumpStart.X + (_jumpEnd.X - _jumpStart.X) * t;
            FeetY = _jumpStart.Y - Math.Sin(t * Math.PI) * 36 * _settings.Scale;
            if (Age >= 1.03) { FeetY = _jumpStart.Y; LandAt(FeetY, true); }
        }
        else if (State == PetState.Peek)
        {
            FeetY = _peekGround + BodyHeight * .5 * PeekFraction;
            if (Age > 4.5) { FeetY = _peekGround; Idle(); }
        }
        else if (State == PetState.LieDown && Age >= .86) SetState(PetState.Doze, _random.Next(12, 24));
        else if (State == PetState.Doze && Age >= _actionDuration) Rise();
        else if (State == PetState.Rise && Age >= _actionDuration)
        { var action = _afterRise; _afterRise = null; Idle(); action?.Invoke(); }
        else if (State is PetState.Pet or PetState.Stretch or PetState.Perch or PetState.Land)
        {
            if (Age >= _actionDuration)
            {
                var action=State==PetState.Land?_afterRise:null;
                if(State==PetState.Land) _afterRise=null;
                Idle(); action?.Invoke();
            }
        }
        else if (State == PetState.Idle && _settings.Autonomous && !_toyActive && Age >= _nextAction) ChooseAction();
        if (AliveTime - _lastInteraction >= 180 && AliveTime >= _nextAttention && !_toyActive &&
            _effectRemaining == 0 && State is PetState.Idle or PetState.Doze or PetState.Perch)
        {
            _nextAttention = AliveTime + 300;
            GroundAction(() => { SetState(PetState.Pet, 2.1); Say("陪我玩一会儿，好不好？"); });
        }
    }

    private void TickWalk()
    {
        var distance = WalkDistanceAt(Age);
        var delta = (distance - _walkDistance) * _settings.Scale;
        if (_support is { } edge)
        {
            var limit = Facing < 0 ? edge.Left + 62 * _settings.Scale : edge.Right - 62 * _settings.Scale;
            var remaining = Facing * (limit - CenterX);
            if (remaining < delta) { CenterX += Facing * Math.Max(0, remaining); Idle(); return; }
        }
        CenterX += Facing * delta; _walkDistance = distance;
        if (Age >= _walkDuration) Idle();
    }

    // The root and the feet use the same travelled distance, including acceleration and braking.
    private double WalkDistanceAt(double seconds)
    {
        const double ramp = .24;
        var t = Math.Clamp(seconds, 0, _walkDuration);
        var speed = _walkLength / (_walkDuration - ramp);
        if (t < ramp) return speed * t * t / (2 * ramp);
        if (t > _walkDuration - ramp)
        { var left = _walkDuration - t; return _walkLength - speed * left * left / (2 * ramp); }
        return speed * (t - ramp / 2);
    }

    private void TickJump()
    {
        if (_jumpTarget.Window != 0)
        {
            if (!_surfaces.TryGetWindowOrigin(_jumpTarget.Window, out var origin)) { _surfaces.Refresh(); Drop(); return; }
            _jumpEnd += origin - _windowOrigin; _windowOrigin = origin;
        }
        if (Age < .26) return;
        var flight = Math.Min(_jumpDuration, Age - .26); var progress = flight / _jumpDuration;
        CenterX = _jumpStart.X + (_jumpEnd.X - _jumpStart.X) * Smooth(progress);
        FeetY = _jumpStart.Y + _jumpVelocity * flight + 900 * flight * flight + (_jumpEnd.Y - _originalJumpEnd.Y) * Smooth(progress);
        if (progress >= 1)
        {
            CenterX = _jumpEnd.X; FeetY = _jumpEnd.Y; _surfaces.Refresh();
            if (_surfaces.Surfaces.Any(s => s.Window == _jumpTarget.Window && s.ContainsX(CenterX) && Math.Abs(s.Top - FeetY) < 5)) LandAt(FeetY, true);
            else Drop();
        }
    }

    private void FollowSupport()
    {
        if (_support is not { } support) return;
        if (support.Window != 0)
        {
            if (!_surfaces.TryGetWindowOrigin(support.Window, out var origin)) { _surfaces.Refresh(); Drop(); return; }
            var shift = origin - _windowOrigin;
            if (shift.Length > .5) _surfaces.Refresh();
            CenterX += shift.X; FeetY += shift.Y;
            if (State == PetState.Pounce) { _jumpStart += shift; _jumpEnd += shift; }
            if (State == PetState.Peek) _peekGround += shift.Y;
            _windowOrigin = origin;
            var visible = _surfaces.Surfaces.FirstOrDefault(s => s.Window == support.Window && s.ContainsX(CenterX));
            if (visible.Width > 0) _support = visible with { Top = origin.Y };
            else { Drop(); return; }
        }
        else
        {
            var area = WorkArea; _support = new Surface(area.Left, area.Right, area.Bottom);
            if (State is not (PetState.Pounce or PetState.Peek)) FeetY = area.Bottom;
        }
        if (State is PetState.Pounce or PetState.Peek) return;
        if (_support is { } edge && !edge.ContainsX(CenterX)) Drop();
    }

    private void ChooseAction()
    {
        switch (_random.Next(7))
        {
            case 0: case 1: Walk(); break;
            case 2: Doze(); break;
            case 3: Stretch(); break;
            case 4: Perch(); break;
            case 5: if (_settings.AutoJump) JumpToWindow(false); else Idle(); break;
            default: if (_support is { Window: not 0 }) Peek(); else Idle(); break;
        }
    }
    public void Interact() { _lastInteraction = AliveTime; _nextAttention = AliveTime + 180; }
    public void Idle() { FinishPeek(); SetState(PetState.Idle); _nextAction = _random.Next(3, 8); }
    public void Walk() => GroundAction(() =>
    {
        Facing = _random.Next(2) == 0 ? -1 : 1;
        if (_support is { } surface && Facing * ((Facing < 0 ? surface.Left + 70 * _settings.Scale : surface.Right - 70 * _settings.Scale) - CenterX) < WalkStride * _settings.Scale) Facing *= -1;
        var room = _support is { } e ? (Facing < 0 ? CenterX - e.Left : e.Right - CenterX) / _settings.Scale - 65 : 200;
        var cycles = Math.Max(.5, Math.Min(_random.Next(2, 5), Math.Floor(room / WalkStride * 2) / 2));
        _walkLength = cycles * WalkStride; _walkDistance = 0; _walkDuration = cycles * WalkCycleSeconds + .24;
        SetState(PetState.Walk);
    });
    public void Doze() => GroundAction(() => { StopToy(); _restOnWindow = _support is { Window: not 0 }; SetState(PetState.LieDown); });
    public void Perch() => GroundAction(() => SetState(PetState.Perch, 10));
    public void Pet() => GroundAction(() => { SetState(PetState.Pet, 2.1); Hearts = true; Bubble = null; _effectRemaining = 2.1; });
    public void Stretch() => GroundAction(() => SetState(PetState.Stretch, 2.8));
    public void Remind() => GroundAction(() => { SetState(PetState.Stretch, 2.8); Say("该休息一下啦，陪糖豆伸个懒腰。" ); });
    public void Say(string text) { Bubble = text; Hearts = false; _effectRemaining = 8; }
    public void ShowAngle(string clip) => GroundAction(() => { StopToy(); Idle(); _angle = clip; });
    private void GroundAction(Action action)
    {
        FinishPeek();
        if(State is PetState.Fall or PetState.Jump or PetState.Pounce or PetState.Land) _afterRise=action;
        else if (State is PetState.Doze or PetState.LieDown or PetState.Rise) { _afterRise = action; if (State != PetState.Rise) Rise(); }
        else action();
    }
    private void Rise()
    {
        _riseOffset=State==PetState.LieDown?.78*(1-Math.Clamp(Age/.86,0,1)):0;
        SetState(PetState.Rise,.78-_riseOffset);
    }
    private void SetState(PetState state, double duration = 0) { State = state; _angle = null; Age = 0; _actionDuration = duration; }
    public void BeginDrag()
    {
        _dragAnimation = Animation; _dragAge = RenderAge; _dragLoop = LoopAnimation;
        Interact(); FinishPeek(); StopToy(); _afterRise = null; _support = null; _velocityY = 0; SetState(PetState.Drag);
    }
    public void DragTo(double x, double feet) { Interact(); CenterX = x; FeetY = feet; }
    public void EndDrag()
    {
        _surfaces.Refresh();
        var snap = _surfaces.Surfaces.Where(s => s.ContainsX(CenterX) && Math.Abs(s.Top - FeetY) <= 36 * _settings.Scale)
            .OrderBy(s => Math.Abs(s.Top - FeetY)).Select(s => (Surface?)s).FirstOrDefault();
        if (snap is { } surface) { FeetY = surface.Top; Attach(surface); SetState(PetState.Land, .5); }
        else Drop();
    }
    public void Drop()
    {
        _fallAnimation = Animation; _fallAge = RenderAge; _fallLoop = LoopAnimation;
        FinishPeek(); _afterRise = null; _support = null; _velocityY = 0; _fallStartY = FeetY; SetState(PetState.Fall);
    }
    private void LandAt(double top, bool cushion)
    {
        FeetY = top; _velocityY = 0;
        Attach(_surfaces.Surfaces.First(s => s.ContainsX(CenterX) && Math.Abs(s.Top - top) < 4));
        if (cushion) SetState(PetState.Land, .5); else Idle();
    }
    private void Attach(Surface support)
    { _support = support; if (support.Window != 0) _surfaces.TryGetWindowOrigin(support.Window, out _windowOrigin); }
    public void ToggleToy()
    {
        Interact(); if (_toyActive) StopToy();
        else GroundAction(() => { _toyActive = true; _toyLifetime = 20; Idle(); });
    }
    public void StopToy() => _toyActive = false;
    private void BeginPounce()
    {
        Facing = ToyPosition.X < CenterX ? -1 : 1; _jumpStart = new Point(CenterX, FeetY);
        var desired = CenterX + Math.Clamp(ToyPosition.X - CenterX, -100 * _settings.Scale, 100 * _settings.Scale);
        if (_support is { } edge) desired = Math.Clamp(desired, edge.Left + 35 * _settings.Scale, Math.Max(edge.Left + 35 * _settings.Scale, edge.Right - 35 * _settings.Scale));
        _jumpEnd = new Point(desired, FeetY); SetState(PetState.Pounce);
    }
    public bool JumpToWindow(bool showNotice = true)
    {
        if (State is PetState.Doze or PetState.LieDown or PetState.Rise)
        { _afterRise = () => JumpToWindow(showNotice); if (State != PetState.Rise) Rise(); return true; }
        FinishPeek(); StopToy(); _surfaces.Refresh();
        var candidates = _surfaces.Surfaces.Where(s => s.Window != 0 && s.Width >= 150 * _settings.Scale && s.Window != _support?.Window &&
            Math.Abs(s.Top - FeetY) < Math.Max(600, WorkArea.Height * .95) && Math.Abs(Math.Clamp(CenterX, s.Left, s.Right) - CenterX) < 850)
            .GroupBy(s => s.Window).Select(g => g.OrderByDescending(s => s.Width).First()).ToArray();
        if (candidates.Length == 0) { if (showNotice) Say("附近没有合适的窗沿，再挪开一点窗口吧。" ); Idle(); return false; }
        var unvisited = candidates.Where(s => s.Window != _lastVisitedWindow).ToArray();
        var choices = unvisited.Length > 0 ? unvisited : candidates; var target = choices[_random.Next(choices.Length)];
        _lastVisitedWindow = _support?.Window ?? 0; _jumpTarget = target; _jumpStart = new Point(CenterX, FeetY);
        var margin = 68 * _settings.Scale; var x = target.Left + margin + _random.NextDouble() * (target.Width - 2 * margin);
        _jumpEnd = _originalJumpEnd = new Point(x, target.Top); Facing = x < CenterX ? -1 : 1;
        var peak = 90 * _settings.Scale + Math.Max(0, FeetY - target.Top);
        _jumpVelocity = -Math.Sqrt(3600 * peak);
        _jumpDuration = -_jumpVelocity / 1800 + Math.Sqrt(2 * (target.Top - (FeetY - peak)) / 1800);
        _support = null; _surfaces.TryGetWindowOrigin(target.Window, out _windowOrigin); SetState(PetState.Jump); return true;
    }
    public void Peek()
    {
        if (State is PetState.Doze or PetState.LieDown or PetState.Rise) { _afterRise = Peek; if (State != PetState.Rise) Rise(); return; }
        StopToy(); if (State == PetState.Peek) { FinishPeek(); Idle(); return; }
        if (_support is not { Window: not 0 }) { Say("把我放到窗沿，我就探头看你。" ); return; }
        _peekGround = FeetY; SetState(PetState.Peek);
    }
    private void FinishPeek() { if (State == PetState.Peek) FeetY = _peekGround; }
    public void BringIntoView()
    {
        var area = WorkArea;
        CenterX = Math.Clamp(CenterX, area.Left + 100 * _settings.Scale, area.Right - 100 * _settings.Scale);
        FeetY = Math.Clamp(FeetY, area.Top + BodyHeight + 10, area.Bottom);
    }
    public void ResetPosition()
    { Interact(); StopToy(); var area = _surfaces.Desktop.WorkAreas[0]; CenterX = area.Right - 220; FeetY = area.Bottom; Drop(); }
    private static double Smooth(double t) => t * t * (3 - 2 * t);
    public string Animation => _angle ?? (State switch
    {
        PetState.Walk => Facing < 0 ? "walk-left" : "walk-right",
        PetState.LieDown => _restOnWindow ? "window-lie" : "lie-left",
        PetState.Doze => _restOnWindow ? "window-rest" : "doze",
        PetState.Rise => _restOnWindow ? "window-rise" : "rise-left",
        PetState.Perch => "perch", PetState.Pet => "pet", PetState.Stretch => "stretch",
        PetState.Pounce => Facing < 0 ? "pounce-left" : "pounce-right",
        PetState.Land => Facing < 0 ? "land-left" : "land-right",
        PetState.Jump when Age < .26 => Facing < 0 ? "takeoff-left" : "takeoff-right",
        PetState.Jump => Facing < 0 ? "fall-left" : "fall-right",
        PetState.Fall when Age < .14 => _fallAnimation,
        PetState.Fall => Facing < 0 ? "fall-left" : "fall-right",
        PetState.Peek => "pet", PetState.Drag => _dragAnimation,
        _ => Facing < 0 ? "idle-left" : "idle-right"
    });
}
