using System.Windows;
using Jellybean.Services;

namespace Jellybean.Engine;

public enum PetState { Idle, Walk, LieDown, Doze, Rise, Perch, Pet, Stretch, Fall, Land, Drag, HuntAim, Stalk, Pounce, Catch, ApproachJump, Jump, Peek }

public sealed class PetEngine
{
    public const double WalkCycleSeconds = .96;
    private const double TakeoffSeconds = .13;
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
    private double _launchX, _approachSpeed, _pounceDuration, _pounceHeight, _huntDistance;
    private Point _targetOrigin, _dragStart, _dragTarget;
    private bool _toyActive, _restOnWindow, _fallLoop, _caughtToy;
    private double _toyLifetime, _peekGround, _effectRemaining;
    private string? _angle;
    private string _fallAnimation = "idle-left";
    private double _fallAge;
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
    public bool ToyCaught => _caughtToy && State is PetState.Pounce or PetState.Catch;
    public double DragSway { get; private set; }
    public string? Bubble { get; private set; }
    public double EffectRemaining => _effectRemaining;
    public bool Hearts { get; private set; }
    public double PeekFraction => State == PetState.Peek ? Smooth(Math.Min(1, Age / .5)) : 0;
    public Surface? Support => _support;
    public double BodyHeight => 128 * _settings.Scale;
    public Rect WorkArea => _surfaces.Desktop.WorkAreaAt(CenterX, FeetY);
    public bool Moving => State is PetState.Walk or PetState.Stalk or PetState.ApproachJump or PetState.Fall or PetState.Drag or PetState.Pounce or PetState.Jump;
    public bool LoopAnimation => State == PetState.Fall && Age < .08 ? _fallLoop :
        State is PetState.Idle or PetState.Walk or PetState.Stalk or PetState.ApproachJump or PetState.Doze or PetState.Perch or PetState.Peek or PetState.Drag;
    public double RenderAge => State switch
    {
        PetState.Walk or PetState.ApproachJump => _walkDistance / WalkStride * WalkCycleSeconds,
        PetState.Stalk => _huntDistance / WalkStride * WalkCycleSeconds,
        PetState.Rise => Age + _riseOffset,
        PetState.Fall when Age < .08 => _fallAge,
        PetState.Jump when Age >= TakeoffSeconds => (Age - TakeoffSeconds) / _jumpDuration * .32,
        PetState.Pounce => Age < .1 ? Age : .1 + (Age - .1) / _pounceDuration * .28,
        _ => Age
    };

    public PetEngine(SurfaceProvider surfaces, UserSettings settings, double walkStride=64, double? rightStride=null)
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
        if (State == PetState.Drag)
        {
            var previousX=CenterX;
            var lift=Smooth(Math.Clamp(Age/.16,0,1));
            CenterX=_dragStart.X+(_dragTarget.X-_dragStart.X)*lift;
            FeetY=_dragStart.Y+(_dragTarget.Y-_dragStart.Y)*lift;
            var lean=Math.Clamp(-(CenterX-previousX)/Math.Max(dt,.001)*.018,-13,13);
            DragSway+=(lean-DragSway)*(1-Math.Exp(-dt*10));
            return;
        }
        if (_support is null && State is not (PetState.Fall or PetState.Jump or PetState.Pounce)) Drop();
        if (State is not (PetState.Fall or PetState.Jump)) FollowSupport();
        if (State == PetState.Fall)
        {
            var previous = FeetY; _velocityY += 1250 * dt; FeetY += _velocityY * dt;
            if (Platforms.TryLand(_surfaces.Surfaces, CenterX, previous, FeetY, out var top)) LandAt(top, NeedsCushion(top-_fallStartY));
            else if (FeetY > WorkArea.Bottom) LandAt(WorkArea.Bottom, NeedsCushion(WorkArea.Bottom-_fallStartY));
            return;
        }
        if (State == PetState.ApproachJump) { TickJumpApproach(dt); return; }
        if (State == PetState.Jump) { TickJump(); return; }
        if (_toyActive)
        {
            _toyLifetime -= dt;
            if (_toyLifetime <= 0) StopToy();
            else if (State == PetState.Idle) AimAtToy();
        }
        if (State == PetState.Walk) TickWalk();
        else if (State is PetState.HuntAim or PetState.Stalk or PetState.Pounce or PetState.Catch) TickHunt(dt);
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
        if (!FollowJumpTarget()) return;
        if (Age < TakeoffSeconds) return;
        var flight = Math.Min(_jumpDuration, Age - TakeoffSeconds); var progress = flight / _jumpDuration;
        var distance=_jumpEnd.X-_jumpStart.X;
        var tangent=Math.Clamp(_approachSpeed*_settings.Scale*_jumpDuration/Math.Max(1,Math.Abs(distance)),.15,1.5);
        var travel=Smooth(progress)+tangent*progress*(1-progress)*(1-progress)+.2*progress*progress*(progress-1);
        CenterX = _jumpStart.X + distance * travel;
        FeetY = _jumpStart.Y + _jumpVelocity * flight + 900 * flight * flight + (_jumpEnd.Y - _originalJumpEnd.Y) * Smooth(progress);
        if (progress >= 1)
        {
            CenterX = _jumpEnd.X; FeetY = _jumpEnd.Y; _surfaces.Refresh();
            var drop=FeetY-(_jumpStart.Y-_jumpVelocity*_jumpVelocity/3600);
            if (_surfaces.Surfaces.Any(s => s.Window == _jumpTarget.Window && s.ContainsX(CenterX) && Math.Abs(s.Top - FeetY) < 5)) LandAt(FeetY, NeedsCushion(drop));
            else Drop();
        }
    }

    private bool FollowJumpTarget()
    {
        if (!_surfaces.TryGetWindowOrigin(_jumpTarget.Window,out var origin)) { _surfaces.Refresh(); Drop(); return false; }
        _jumpEnd+=origin-_targetOrigin; _targetOrigin=origin;
        return true;
    }

    private void TickJumpApproach(double dt)
    {
        if (!FollowJumpTarget() || State!=PetState.ApproachJump) return;
        var remaining=Facing*(_launchX-CenterX)/_settings.Scale;
        if (remaining<=.5) { StartJumpFlight(); return; }
        var speed=WalkStride/WalkCycleSeconds*(remaining<70?1.55:1);
        _approachSpeed+=(speed-_approachSpeed)*(1-Math.Exp(-dt*7));
        var distance=Math.Min(remaining,_approachSpeed*dt);
        CenterX+=Facing*distance*_settings.Scale; _walkDistance+=distance;
        if (remaining-distance<.5) StartJumpFlight();
    }

    private void StartJumpFlight()
    {
        if (!_surfaces.Surfaces.Any(s=>s.Window==_jumpTarget.Window && s.ContainsX(_jumpEnd.X))) { Idle(); return; }
        _jumpStart=new Point(CenterX,FeetY); _originalJumpEnd=_jumpEnd;
        if(Math.Abs(_jumpEnd.X-CenterX)>8*_settings.Scale) Facing=_jumpEnd.X<CenterX?-1:1;
        var peak=44*_settings.Scale+Math.Max(0,FeetY-_jumpEnd.Y);
        _jumpVelocity=-Math.Sqrt(3600*peak);
        _jumpDuration=-_jumpVelocity/1800+Math.Sqrt(2*(_jumpEnd.Y-(FeetY-peak))/1800);
        _support=null; SetState(PetState.Jump);
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
            if (State == PetState.ApproachJump) _launchX+=shift.X;
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
        Interact(); FinishPeek(); StopToy(); _afterRise = null; _support = null; _velocityY = 0; SetState(PetState.Drag);
        _dragStart=_dragTarget=new Point(CenterX,FeetY); DragSway=0;
    }
    public void DragTo(double x, double feet)
    {
        Interact(); _dragTarget=new Point(x,feet);
    }
    public void EndDrag()
    {
        CenterX=_dragTarget.X; FeetY=_dragTarget.Y;
        BringIntoView();
        _surfaces.Refresh();
        var snap = _surfaces.Surfaces.Where(s => s.ContainsX(CenterX) && Math.Abs(s.Top - FeetY) <= 36 * _settings.Scale)
            .OrderBy(s => Math.Abs(s.Top - FeetY)).Select(s => (Surface?)s).FirstOrDefault();
        if (snap is { } surface) { FeetY = surface.Top; Attach(surface); Idle(); }
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
        if (cushion) SetState(PetState.Land, .28); else { var action=_afterRise; _afterRise=null; Idle(); action?.Invoke(); }
    }
    private bool NeedsCushion(double height) => height>=110*_settings.Scale;
    private void Attach(Surface support)
    { _support = support; if (support.Window != 0) _surfaces.TryGetWindowOrigin(support.Window, out _windowOrigin); }
    public void ToggleToy()
    {
        Interact(); if (_toyActive) StopToy();
        else GroundAction(() => { _toyActive = true; _toyLifetime = 20; AimAtToy(); });
    }
    public void StopToy()
    {
        _toyActive=false; _caughtToy=false;
        if (State is PetState.HuntAim or PetState.Stalk or PetState.Catch) Idle();
    }
    private void AimAtToy()
    {
        Facing=ToyPosition.X<CenterX?-1:1; _caughtToy=false;
        SetState(PetState.HuntAim,.18+_random.NextDouble()*.12);
    }
    private void TickHunt(double dt)
    {
        if (State==PetState.HuntAim)
        {
            Facing=ToyPosition.X<CenterX?-1:1;
            if (Age>=_actionDuration) { _huntDistance=0; SetState(PetState.Stalk); }
        }
        else if (State==PetState.Stalk)
        {
            var distance=ToyPosition.X-CenterX-Facing*38*_settings.Scale;
            if (Facing*distance<-45*_settings.Scale) { AimAtToy(); return; }
            if (Math.Abs(distance)<155*_settings.Scale && Age>.18) { BeginPounce(); return; }
            var next=CenterX+Facing*Math.Min(Math.Abs(distance),42*_settings.Scale*dt);
            if (_support is { } edge) next=Math.Clamp(next,edge.Left+32*_settings.Scale,Math.Max(edge.Left+32*_settings.Scale,edge.Right-32*_settings.Scale));
            _huntDistance+=Math.Abs(next-CenterX)/_settings.Scale; CenterX=next;
        }
        else if (State==PetState.Pounce)
        {
            var t=Math.Clamp((Age-.1)/_pounceDuration,0,1);
            var travel=1-Math.Pow(1-t,1.3);
            CenterX=_jumpStart.X+(_jumpEnd.X-_jumpStart.X)*travel;
            FeetY=_jumpStart.Y-Math.Sin(t*Math.PI)*_pounceHeight;
            var paws=new Point(CenterX+Facing*38*_settings.Scale,FeetY-12*_settings.Scale);
            if ((paws-ToyPosition).Length<38*_settings.Scale) _caughtToy=true;
            if (t>=1)
            {
                FeetY=_jumpStart.Y;
                if (_toyActive) SetState(PetState.Catch,.18);
                else LandAt(FeetY,false);
            }
        }
        else if (State==PetState.Catch && Age>=_actionDuration)
        {
            var action=_afterRise; _afterRise=null;
            if (action is not null) { Idle(); action(); }
            else if (_toyActive) AimAtToy(); else Idle();
        }
    }
    private void BeginPounce()
    {
        Facing = ToyPosition.X < CenterX ? -1 : 1; _jumpStart = new Point(CenterX, FeetY);
        if(Math.Abs(ToyPosition.X-CenterX)<48*_settings.Scale && Math.Abs(ToyPosition.Y-FeetY)<75*_settings.Scale)
        { _caughtToy=true; SetState(PetState.Catch,.18); return; }
        var desired=ToyPosition.X-Facing*38*_settings.Scale;
        desired=CenterX+Math.Clamp(desired-CenterX,-190*_settings.Scale,190*_settings.Scale);
        if (_support is { } edge) desired = Math.Clamp(desired, edge.Left + 32 * _settings.Scale, Math.Max(edge.Left + 32 * _settings.Scale, edge.Right - 32 * _settings.Scale));
        if(Facing*(desired-CenterX)<0) desired=CenterX;
        var length=Math.Abs(desired-CenterX)/_settings.Scale;
        _pounceDuration=Math.Clamp(.19+length/1050,.20,.34);
        _pounceHeight=Math.Clamp(18+length*.12+(FeetY-ToyPosition.Y)*.18,18,65)*_settings.Scale;
        _jumpEnd = new Point(desired, FeetY); _caughtToy=false; SetState(PetState.Pounce);
    }
    public bool JumpToWindow(bool showNotice = true)
    {
        if (State is PetState.Doze or PetState.LieDown or PetState.Rise)
        { _afterRise = () => JumpToWindow(showNotice); if (State != PetState.Rise) Rise(); return true; }
        if (State is PetState.Fall or PetState.Jump or PetState.Pounce or PetState.Land)
        { _afterRise=()=>JumpToWindow(showNotice); return true; }
        FinishPeek(); StopToy(); _surfaces.Refresh();
        var candidates = _surfaces.Surfaces.Where(s => s.Window != 0 && s.Width >= 150 * _settings.Scale && s.Window != _support?.Window &&
            Math.Abs(s.Top - FeetY) < Math.Max(600, WorkArea.Height * .95) && Math.Abs(Math.Clamp(CenterX, s.Left, s.Right) - CenterX) < 850)
            .GroupBy(s => s.Window).Select(g => g.OrderByDescending(s => s.Width).First()).ToArray();
        if (candidates.Length == 0) { if (showNotice) Say("附近没有合适的窗沿，再挪开一点窗口吧。" ); Idle(); return false; }
        var unvisited = candidates.Where(s => s.Window != _lastVisitedWindow).ToArray();
        var choices = unvisited.Length > 0 ? unvisited : candidates; var target = choices[_random.Next(choices.Length)];
        _lastVisitedWindow = _support?.Window ?? 0; _jumpTarget = target;
        var margin = 68 * _settings.Scale; var x = target.Left + margin + _random.NextDouble() * (target.Width - 2 * margin);
        _jumpEnd = new Point(x, target.Top); Facing = x < CenterX ? -1 : 1;
        var support=_support!.Value;
        _launchX=support.Window!=0?(Facing<0?support.Left+34*_settings.Scale:support.Right-34*_settings.Scale):
            Math.Clamp(CenterX+Facing*Math.Min(90*_settings.Scale,Math.Abs(x-CenterX)*.4),support.Left+40*_settings.Scale,support.Right-40*_settings.Scale);
        _approachSpeed=0; _walkDistance=0;
        _surfaces.TryGetWindowOrigin(target.Window,out _targetOrigin);
        SetState(PetState.ApproachJump); return true;
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
        PetState.Walk or PetState.ApproachJump => Facing < 0 ? "walk-left" : "walk-right",
        PetState.Stalk => Facing < 0 ? "stalk-left" : "stalk-right",
        PetState.HuntAim => Facing < 0 ? "crouch-left" : "crouch-right",
        PetState.Catch => Facing < 0 ? "catch-left" : "catch-right",
        PetState.LieDown => _restOnWindow ? "window-lie" : "lie-left",
        PetState.Doze => _restOnWindow ? "window-rest" : "doze",
        PetState.Rise => _restOnWindow ? "window-rise" : "rise-left",
        PetState.Perch => "perch", PetState.Pet => "pet", PetState.Stretch => "stretch",
        PetState.Pounce => Facing < 0 ? "pounce-left" : "pounce-right",
        PetState.Land => Facing < 0 ? "land-left" : "land-right",
        PetState.Jump when Age < TakeoffSeconds => Facing < 0 ? "takeoff-left" : "takeoff-right",
        PetState.Jump => Facing < 0 ? "air-left" : "air-right",
        PetState.Fall when Age < .08 => _fallAnimation,
        PetState.Fall => Facing < 0 ? "fall-left" : "fall-right",
        PetState.Peek => "pet", PetState.Drag => Facing<0?"held-left":"held-right",
        _ => Facing < 0 ? "idle-left" : "idle-right"
    });
}
