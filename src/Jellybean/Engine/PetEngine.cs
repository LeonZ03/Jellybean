using System.Windows;
using Jellybean.Services;

namespace Jellybean.Engine;

public enum PetState { Idle, Walk, Doze, Perch, Pet, Stretch, Fall, Drag, Pounce, Jump, Peek }

public sealed class PetEngine
{
    private readonly Random _random = new();
    private readonly SurfaceProvider _surfaces;
    private readonly UserSettings _settings;
    private Surface? _support;
    private Point _windowOrigin;
    private double _velocityY, _nextAction = 5, _refreshIn;
    private Point _jumpStart, _jumpEnd;
    private Surface _jumpTarget;
    private double _jumpDuration;
    private double _actionDuration;
    private bool _toyActive;
    private double _toyLifetime;
    private double _peekGround;
    private double _effectRemaining;
    private string? _angle;
    public double CenterX { get; private set; }
    public double FeetY { get; private set; }
    public PetState State { get; private set; } = PetState.Idle;
    public double Age { get; private set; }
    public int Facing { get; private set; } = -1;
    public Point ToyPosition { get; set; }
    public bool ToyActive => _toyActive;
    public string? Bubble { get; private set; }
    public double EffectRemaining => _effectRemaining;
    public bool Hearts { get; private set; }
    public string? Notice { get; private set; }
    public double PeekFraction => State == PetState.Peek ? Math.Min(1, Age / 0.45) : 0;
    public Surface? Support => _support;
    public double BodyHeight => 128 * _settings.Scale;
    public Rect WorkArea => _surfaces.Desktop.WorkAreaAt(CenterX, FeetY);
    public bool Moving => State is PetState.Walk or PetState.Fall or PetState.Drag or PetState.Pounce or PetState.Jump;

    public PetEngine(SurfaceProvider surfaces, UserSettings settings)
    {
        _surfaces = surfaces;
        _settings = settings;
        var area = surfaces.Desktop.WorkAreas[0];
        CenterX = settings.CenterX ?? area.Right - 220;
        FeetY = settings.FeetY ?? area.Bottom;
        BringIntoView();
        Drop();
    }

    public void Tick(double dt)
    {
        Age += dt;
        _refreshIn -= dt;
        _effectRemaining = Math.Max(0, _effectRemaining - dt);
        if (_angle is not null && Age > 6) _angle = null;
        if (_effectRemaining == 0) { Bubble = null; Hearts = false; }
        if (_refreshIn <= 0)
        {
            _surfaces.Refresh();
            _refreshIn = 0.45;
        }

        if (State == PetState.Drag) return;
        if (_support is null && State is not (PetState.Fall or PetState.Jump or PetState.Pounce)) Drop();
        if (State is not (PetState.Fall or PetState.Jump)) FollowSupport();

        if (State == PetState.Fall)
        {
            var previous = FeetY;
            _velocityY += 1100 * dt;
            FeetY += _velocityY * dt;
            if (Platforms.TryLand(_surfaces.Surfaces, CenterX, previous, FeetY, out var top)) LandAt(top);
            else if (FeetY > _surfaces.Desktop.WorkAreaAt(CenterX, FeetY).Bottom)
                LandAt(_surfaces.Desktop.WorkAreaAt(CenterX, FeetY).Bottom);
            return;
        }

        if (State == PetState.Jump)
        {
            if (_jumpTarget.Window != 0 && _surfaces.TryGetWindowOrigin(_jumpTarget.Window, out var origin))
                _jumpEnd = new Point(_jumpEnd.X + origin.X - _windowOrigin.X, origin.Y);
            if (_jumpTarget.Window != 0 && _surfaces.TryGetWindowOrigin(_jumpTarget.Window, out var current)) _windowOrigin = current;
            var progress = Math.Min(1, Age / _jumpDuration);
            CenterX = _jumpStart.X + (_jumpEnd.X - _jumpStart.X) * progress;
            FeetY = _jumpStart.Y + (_jumpEnd.Y - _jumpStart.Y) * progress - Math.Sin(progress * Math.PI) * 100 * _settings.Scale;
            if (progress >= 1)
            {
                _surfaces.Refresh();
                if (_surfaces.Surfaces.Any(s => s.Window == _jumpTarget.Window && s.ContainsX(CenterX) && Math.Abs(s.Top - FeetY) < 5)) LandAt(FeetY);
                else Drop();
            }
            return;
        }

        if (_toyActive)
        {
            _toyLifetime -= dt;
            if (_toyLifetime <= 0) StopToy();
            else if (State is PetState.Idle or PetState.Walk or PetState.Doze or PetState.Perch)
            {
                Facing = ToyPosition.X < CenterX ? -1 : 1;
                if (Age > 0.7) BeginPounce();
            }
        }

        if (State == PetState.Walk)
        {
            var radius = 60 * _settings.Scale;
            if (_support is { } edge && (CenterX + Facing * radius < edge.Left || CenterX + Facing * radius > edge.Right)) Facing *= -1;
            CenterX += Facing * 30 * _settings.Scale * dt;
            if (Age >= _actionDuration) Idle();
        }
        else if (State == PetState.Pounce)
        {
            // The anticipation stays on the ground; only the airborne frame moves the cat.
            var t = Math.Clamp((Age - 0.78) / 0.55, 0, 1);
            CenterX = _jumpStart.X + (_jumpEnd.X - _jumpStart.X) * t;
            FeetY = _jumpStart.Y - Math.Sin(t * Math.PI) * 35 * _settings.Scale;
            if (Age >= 1.7) { FeetY = _jumpStart.Y; Drop(); }
        }
        else if (State == PetState.Peek)
        {
            FeetY = _peekGround + BodyHeight * 0.5 * PeekFraction;
            if (Age > 5) { FeetY = _peekGround; Idle(); }
        }
        else if (State is PetState.Pet or PetState.Stretch)
        {
            if (Age >= _actionDuration) Idle();
        }
        else if (State == PetState.Doze && Age >= _actionDuration) Idle();
        else if (State == PetState.Perch && Age >= _actionDuration) Idle();
        else if (State == PetState.Idle && _settings.Autonomous && !_toyActive && Age >= _nextAction) ChooseAction();
    }

    private void FollowSupport()
    {
        if (_support is not { } support) return;
        if (support.Window != 0)
        {
            if (!_surfaces.TryGetWindowOrigin(support.Window, out var origin)) { Drop(); return; }
            var dx = origin.X - _windowOrigin.X;
            var dy = origin.Y - _windowOrigin.Y;
            CenterX += dx;
            FeetY += dy;
            if (State == PetState.Pounce)
            {
                _jumpStart = new Point(_jumpStart.X + dx, _jumpStart.Y + dy);
                _jumpEnd = new Point(_jumpEnd.X + dx, _jumpEnd.Y + dy);
            }
            if (State == PetState.Peek) _peekGround += origin.Y - _windowOrigin.Y;
            _support = support with { Left = support.Left + origin.X - _windowOrigin.X, Right = support.Right + origin.X - _windowOrigin.X, Top = origin.Y };
            _windowOrigin = origin;
            if (State is PetState.Pounce) return;
            var visible = _surfaces.Surfaces.FirstOrDefault(s => s.Window == support.Window && s.ContainsX(CenterX));
            if (visible.Width > 0)
                _support = visible with { Top = origin.Y };
        }
        if (State is PetState.Pounce or PetState.Peek) return;
        if (_support is { } edge && !edge.ContainsX(CenterX)) Drop();
    }

    private void ChooseAction()
    {
        switch (_random.Next(7))
        {
            case 0: case 1: Walk(); break;
            case 2: SetState(PetState.Doze, _random.Next(12, 25)); break;
            case 3: Stretch(); break;
            case 4: SetState(PetState.Perch, _random.Next(6, 14)); break;
            case 5: if (_settings.AutoJump) JumpToWindow(false); else Idle(); break;
            default: Pet(); break;
        }
    }

    public void Idle()
    {
        if (State == PetState.Peek) FeetY = _peekGround;
        SetState(PetState.Idle);
        _nextAction = _random.Next(7, 16);
    }

    public void Walk() { FinishPeek(); Facing = _random.Next(2) == 0 ? -1 : 1; SetState(PetState.Walk, _random.Next(3, 7)); }
    public void Doze() { FinishPeek(); StopToy(); SetState(PetState.Doze, 20); }
    public void Perch() { FinishPeek(); SetState(PetState.Perch, 12); }
    public void Pet() { FinishPeek(); SetState(PetState.Pet, 2.2); Hearts = true; Bubble = null; _effectRemaining = 2.2; }
    public void Stretch() { FinishPeek(); SetState(PetState.Stretch, 3.1); }
    public void Remind() { Stretch(); Bubble = "该休息一下啦，陪糖豆伸个懒腰。"; Hearts = false; _effectRemaining = 7; }
    public void Say(string text) { Bubble = text; Hearts = false; _effectRemaining = 7; }
    public void ShowAngle(string clip) { FinishPeek(); StopToy(); Idle(); _angle = clip; }

    private void SetState(PetState state, double duration = 0)
    {
        State = state;
        _angle = null;
        Age = 0;
        _actionDuration = duration;
    }

    public void BeginDrag()
    {
        FinishPeek(); StopToy(); _support = null; _velocityY = 0; SetState(PetState.Drag);
    }

    public void DragTo(double x, double feet) { CenterX = x; FeetY = feet; }

    public void EndDrag()
    {
        _surfaces.Refresh();
        var snap = _surfaces.Surfaces.Where(s => s.ContainsX(CenterX) && Math.Abs(s.Top - FeetY) <= 36 * _settings.Scale)
            .OrderBy(s => Math.Abs(s.Top - FeetY)).Select(s => (Surface?)s).FirstOrDefault();
        if (snap is { } surface) { FeetY = surface.Top; Attach(surface); Idle(); }
        else Drop();
    }

    public void Drop()
    {
        FinishPeek(); _support = null; _velocityY = 0; SetState(PetState.Fall);
    }

    private void LandAt(double top)
    {
        FeetY = top; _velocityY = 0;
        var support = _surfaces.Surfaces.FirstOrDefault(s => s.ContainsX(CenterX) && Math.Abs(s.Top - top) < 4);
        Attach(support);
        Idle();
    }

    private void Attach(Surface support)
    {
        _support = support;
        if (support.Window != 0) _surfaces.TryGetWindowOrigin(support.Window, out _windowOrigin);
    }

    public void ToggleToy()
    {
        if (_toyActive) StopToy();
        else { FinishPeek(); _toyActive = true; _toyLifetime = 20; Idle(); }
    }

    public void StopToy() => _toyActive = false;

    private void BeginPounce()
    {
        _jumpStart = new Point(CenterX, FeetY);
        var area = _surfaces.Desktop.WorkAreaAt(CenterX, FeetY);
        var desired = CenterX + Math.Clamp(ToyPosition.X - CenterX, -110 * _settings.Scale, 110 * _settings.Scale);
        if (_support is { } edge) desired = Math.Clamp(desired, edge.Left + 30 * _settings.Scale, Math.Max(edge.Left + 30 * _settings.Scale, edge.Right - 30 * _settings.Scale));
        _jumpEnd = new Point(Math.Clamp(desired, area.Left + 60, area.Right - 60), FeetY);
        SetState(PetState.Pounce);
    }

    public bool JumpToWindow(bool showNotice = true)
    {
        FinishPeek(); StopToy(); _surfaces.Refresh();
        var target = _surfaces.Surfaces.Where(s => s.Window != 0 && s.Width >= 150 * _settings.Scale &&
                s.Window != _support?.Window && Math.Abs(s.Top - FeetY) < 450 &&
                Math.Abs(Math.Clamp(CenterX, s.Left, s.Right) - CenterX) < 450 &&
                s.Top >= _surfaces.Desktop.WorkAreaAt((s.Left + s.Right) / 2, s.Top).Top + BodyHeight)
            .OrderBy(s => Math.Abs(s.Top - FeetY) + Math.Abs((s.Left + s.Right) / 2 - CenterX))
            .Select(s => (Surface?)s).FirstOrDefault();
        if (target is not { } surface)
        {
            if (showNotice) { Notice = "附近没有适合落脚的窗口，先把窗口往下挪一点吧。"; Bubble = Notice; _effectRemaining = 5; }
            Idle(); return false;
        }
        _jumpTarget = surface;
        _jumpStart = new Point(CenterX, FeetY);
        _jumpEnd = new Point(Math.Clamp(CenterX, surface.Left + 75 * _settings.Scale, surface.Right - 75 * _settings.Scale), surface.Top);
        Facing = _jumpEnd.X < CenterX ? -1 : 1;
        _jumpDuration = 0.85;
        _support = null;
        _surfaces.TryGetWindowOrigin(surface.Window, out _windowOrigin);
        SetState(PetState.Jump);
        return true;
    }

    public void Peek()
    {
        StopToy();
        if (State == PetState.Peek) { FinishPeek(); Idle(); return; }
        if (_support is not { Window: not 0 })
        {
            Bubble = "把糖豆放到窗口顶边，就能探头看你啦。"; _effectRemaining = 5; return;
        }
        _peekGround = FeetY;
        SetState(PetState.Peek);
    }

    private void FinishPeek() { if (State == PetState.Peek) FeetY = _peekGround; }

    public void BringIntoView()
    {
        FinishPeek();
        var area = _surfaces.Desktop.WorkAreaAt(CenterX, FeetY);
        CenterX = Math.Clamp(CenterX, area.Left + 100 * _settings.Scale, area.Right - 100 * _settings.Scale);
        FeetY = Math.Clamp(FeetY, area.Top + BodyHeight + 10, area.Bottom);
    }

    public void ResetPosition()
    {
        StopToy();
        var area = _surfaces.Desktop.WorkAreas[0];
        CenterX = area.Right - 220; FeetY = area.Bottom; Drop();
    }

    public string Animation => _angle ?? (State switch
    {
        PetState.Walk => Facing < 0 ? "walk-left" : "walk-right",
        PetState.Doze => "doze", PetState.Perch => "perch", PetState.Pet => "pet",
        PetState.Stretch => "stretch", PetState.Pounce => Facing < 0 ? "pounce-left" : "pounce-right",
        PetState.Jump => Facing < 0 ? "jump-left" : "jump-right",
        PetState.Peek => "pet", PetState.Drag => "front-three-quarter",
        _ => Facing < 0 ? "idle-left" : "idle-right"
    });
}
