namespace Zoe.Core;

public sealed record AnimInfo(int Frames, double Fps, bool Loop)
{
    public double Duration => Frames / Fps;
}

public enum PetState { Sit, Walk, Run, Sniff, Yawn, Stretch, LieDown, Sleep, GetUp, Happy, Dangle, Air, Turn, Transition, Crouch,
    Wag, Greet, PlayBow, PickUp, Drop, Carry, Bark, Paw, BellyDown, Belly, BellyUp,
    Scratch, Dig, Spin, RestDown, Rest, RestUp, Circle }

/// <summary>Body posture, used to insert sit-down / stand-up transitions between states.</summary>
public enum Posture { None, Stand, Sit, Lie }

/// <summary>
/// Zoe's brain and body: behaviour state machine plus simple platformer physics.
/// Position (X, Y) is the point between her feet, in screen pixels.
/// Pure logic, no Windows dependencies, so it is unit tested on Linux.
/// </summary>
public sealed class PetSim
{
    public const double Gravity = 2400;

    private static readonly Dictionary<string, string> Fallback = new()
    {
        ["run"] = "walk", ["walk"] = "sit", ["sniff"] = "sit", ["yawn"] = "sit",
        ["stretch"] = "sit", ["liedown"] = "sleep", ["sleep"] = "sit", ["getup"] = "sit",
        ["happy"] = "sit", ["wag"] = "sit", ["greet"] = "happy", ["playbow"] = "stretch",
        ["restdown"] = "liedown", ["rest"] = "sleep", ["restup"] = "getup", ["scratch"] = "yawn", ["dig"] = "sniff", ["spin"] = "wag", ["bark"] = "wag", ["paw"] = "happy", ["bellydown"] = "liedown", ["belly"] = "sleep", ["bellyup"] = "getup",
        ["pickup"] = "sniff", ["drop"] = "sniff", ["carry"] = "walk", ["dangle"] = "fall", ["jump"] = "fall", ["fall"] = "sit", ["turn"] = "walk",
    };

    private readonly IReadOnlyDictionary<string, AnimInfo> _anims;
    private readonly ZoeConfig _cfg;
    private readonly Random _rng;
    private readonly double _s; // sprite scale, for distances and speeds

    private IReadOnlyList<Platform> _platforms = Array.Empty<Platform>();
    private IReadOnlyList<RectI> _areas = Array.Empty<RectI>();
    private Platform? _support;
    private double _stateTime, _stateDuration, _animTime;
    private int _walkDir = 1;
    private double? _targetX;
    private double _dragDx, _dragDy;
    private Mood _mood = Mood.Normal;
    private Posture _posture = Posture.None;
    private (PetState State, double Duration) _pending;
    private string _transitionAnim = "";
    private string? _variant;
    private (double Vx, double Vy) _jumpV;
    private string? _nextVariant;
    private double _rollAt;
    private double _clock, _lastPoke = -10;
    private int _zoomLaps;
    private bool _greetPending, _greeting;
    // ball
    private double _ballVx, _ballVy, _ballIdle, _chaseTime, _chaseAfter;
    private bool _ballRest, _ballWanted, _chasing;
    private int _ballPlays;
    private int _ask;            // "throw it for me": 1 staring, 2 paw, 3 staring, 4 paw, 5 last look
    private int _fetch;          // 1 running off-screen, 2 away, 3 coming back with the ball
    private double _fetchWait, _petUntil, _nextBark;
    private bool _following, _seekGo;
    private double _restFor = 10;
    private bool _bedtimeYawn;
    private string _circleAnim = "walk";   // circling before bed: "walk", "turnback" or "turn"
    private double _circleDist;
    private int _circleHalf;
    private int _seek;           // attention seeking: 1 stare, 2 paw, 3 stare, 4 bark, 5 stare
    private double _followUntil, _cursorMovedAt, _lastCursorX = double.NaN, _lastAttention;

    public PetSim(IReadOnlyDictionary<string, AnimInfo> anims, ZoeConfig cfg, Random rng, double scale = 1.0)
    {
        if (!anims.ContainsKey("sit")) throw new ArgumentException("A 'sit' animation is required.");
        _anims = anims; _cfg = cfg; _rng = rng; _s = scale;
    }

    public double X { get; private set; }
    public double Y { get; private set; }
    public double Vx { get; private set; }
    public double Vy { get; private set; }
    public bool FacingRight { get; private set; } = true;
    public PetState State { get; private set; } = PetState.Air;
    public string Anim { get; private set; } = "fall";
    public int Frame { get; private set; }
    public Platform? Support => _support;
    public bool Visible => _mood != Mood.Hidden;

    /// <summary>Mouse pointer X in screen pixels (set by the app); she brings her ball and her greetings to it.</summary>
    public double? CursorX { get; set; }
    /// <summary>Local time of day in hours (0-24), set by the shell; null = no daily rhythm.</summary>
    public double? Hour { get; set; }
    /// <summary>0 = fresh, 1 = worn out. Builds with play and time awake, falls while she sleeps or rests.</summary>
    public double Tiredness { get; set; }
    public bool BallActive { get; private set; }
    public bool BallHeld { get; private set; }
    /// <summary>The ball is drawn separately unless it's in her mouth (then it's part of her sprite).</summary>
    public bool BallVisible => BallActive && !BallHeld && _mood != Mood.Hidden;
    /// <summary>Ball position: centre X, bottom Y (where it touches the ground).</summary>
    public double BallX { get; private set; }
    public double BallY { get; private set; }
    public double BallRadius { get; set; } = 10;
    /// <summary>Distance the ball has rolled, for spinning its sprite.</summary>
    public double BallRoll { get; private set; }
    public bool Zooming => _zoomLaps > 0;
    private bool HasBallAnims => _anims.ContainsKey("carry") && _anims.ContainsKey("pickup");
    private double NoseReach => Math.Max(HalfWidth * 0.8, 20 * _s);

    /// <summary>Half of Zoe's drawn width; she turns back before any of her goes off the screen edge.</summary>
    public double HalfWidth { get; set; }
    public bool Grounded => State is not (PetState.Air or PetState.Dangle);
    public Posture Posture => _posture;

    public Mood Mood
    {
        get => _mood;
        set
        {
            if (value == _mood) return;
            var old = _mood;
            _mood = value;
            _zoomLaps = 0; _greeting = false; _chasing = false; _fetch = 0; _ask = 0;
            _following = false; _seek = 0; _seekGo = false;
            if (value != Mood.Normal) { BallActive = false; BallHeld = false; }
            if (!Grounded || old == Mood.Hidden && value == Mood.Normal) return;
            if (value == Mood.Normal && State is PetState.Sleep or PetState.LieDown) WakeUp();
            else if (value == Mood.Normal && State == PetState.Circle) Decide(); // no need for bed after all
            else if (value is Mood.Nap or Mood.Doze && State != PetState.Sleep) Decide();
            else if (value == Mood.Nap && State == PetState.Sleep && !InNapCorner()) Decide();
        }
    }

    /// <summary>Drop Zoe in from (x, y), e.g. the top of the screen.</summary>
    public void Spawn(double x, double y)
    {
        X = x; Y = y; _support = null;
        EnterAir(0, 0);
    }

    public void Tick(double dt, IReadOnlyList<Platform> platforms, IReadOnlyList<RectI> workAreas)
    {
        dt = Math.Clamp(dt, 0, 0.1);
        _platforms = platforms;
        _areas = workAreas;
        _clock += dt;
        if (CursorX is { } cur && !(Math.Abs(cur - _lastCursorX) < 4)) { _lastCursorX = cur; _cursorMovedAt = _clock; }
        if (_mood == Mood.Hidden && State != PetState.Dangle) return;
        UpdateBall(dt);

        _stateTime += dt;
        _animTime += dt;

        UpdateTiredness(dt);
        if (State == PetState.Air) UpdateAir(dt);
        else if (State != PetState.Dangle)
        {
            // Fetching her ball takes her past the edge of the screen, where there's no floor to find.
            bool away = _fetch != 0 && !_platforms.Any(p => p.Spans(X) && Math.Abs(p.Y - Y) <= 3);
            if (away || UpdateSupport()) UpdateGrounded(dt);
            else EnterAir(0, 0);
        }
        UpdateAnim();
    }

    // ---------- sleep cycle ----------

    /// <summary>How sleepy the hour makes her: 1 in the daytime, more after lunch, most at night.</summary>
    private double Drowsy => Hour switch
    {
        null => 1,
        >= 22 or < 6 => 2.0,      // night
        >= 20 => 1.5,             // evening
        >= 13 and < 15 => 1.3,    // after-lunch lull
        _ => 1,
    };

    /// <summary>Liveliness after tiredness and time of day: what her zoomies, running and ball games scale with.</summary>
    private double Pep => _cfg.SleepCycle ? _cfg.Energy * (1 - 0.85 * Tiredness) / Drowsy : _cfg.Energy;

    private void UpdateTiredness(double dt)
    {
        if (!_cfg.SleepCycle) { Tiredness = 0; return; }
        double perSec = State switch
        {
            PetState.Sleep => -1.0 / 150,                     // a worn-out Zoe needs a couple of minutes
            PetState.Rest or PetState.Belly => -1.0 / 400,    // a sploot helps a little
            PetState.LieDown or PetState.GetUp or PetState.RestDown or PetState.RestUp or PetState.BellyDown or PetState.BellyUp => 0,
            PetState.Run => 1.0 / 120,                         // zoomies and ball chasing wear her out
            PetState.Air or PetState.Crouch or PetState.Spin or PetState.Dig or PetState.Greet or PetState.PlayBow or PetState.Bark => 1.0 / 300,
            PetState.Walk or PetState.Carry or PetState.PickUp or PetState.Drop => 1.0 / 900,
            PetState.Dangle => 0,
            _ => 1.0 / 4000,                                  // just being awake
        };
        if (perSec > 0) perSec *= Drowsy;
        Tiredness = Math.Clamp(Tiredness + perSec * dt, 0, 1);
    }

    // ---------- interaction ----------

    public void Poke()
    {
        if (!Grounded) return;
        _zoomLaps = 0; _chasing = false; _following = false; _seek = 0; _seekGo = false;
        _lastAttention = _clock;
        bool twice = _clock - _lastPoke < 1.2;
        _lastPoke = _clock;
        if (State is PetState.Sleep or PetState.LieDown) WakeUp();
        else if (State is PetState.Rest) Enter(PetState.RestUp, 0); // up she gets
        else if (State is PetState.RestDown or PetState.RestUp) return;
        else if (State is PetState.Carry or PetState.PickUp or PetState.Drop || _fetch != 0) return;
        else if (State is PetState.Belly or PetState.BellyDown or PetState.BellyUp) Pet();
        else if (twice && _anims.ContainsKey("greet")) { _targetX = null; Set(PetState.Greet); } // double-click: paws up
        else Set(PetState.Happy, Rand(1.5, 2.5));
    }

    /// <summary>her owner's back (or Zoe has just arrived): run over, stand up on her hind legs and say hello.</summary>
    public void Greet()
    {
        if (!_anims.ContainsKey("greet")) return;
        if (_mood != Mood.Normal || !Grounded || _fetch != 0 || BallHeld || State is PetState.Sleep or PetState.LieDown or PetState.GetUp or PetState.Belly or PetState.BellyDown or PetState.BellyUp or PetState.RestDown or PetState.RestUp) _greetPending = true;
        else if (State == PetState.Rest) { _greetPending = true; Enter(PetState.RestUp, 0); }
        else StartGreet();
    }

    private void StartGreet()
    {
        _greetPending = false; _zoomLaps = 0; _chasing = false;
        if (CursorX is { } cx && SupportSpan() is var (lo, hi) && hi > lo)
        {
            double tx = Math.Clamp(cx, lo, hi);
            if (Math.Abs(tx - X) > 120 * _s) { _greeting = true; _targetX = tx; Set(PetState.Run, double.PositiveInfinity); return; }
        }
        Set(PetState.Greet);
    }

    /// <summary>her owner is rubbing her with the mouse: over she goes for a belly rub, for as long as it lasts.</summary>
    public void Pet()
    {
        if (_mood != Mood.Normal || !Grounded) return;
        _petUntil = _clock + 2.0; _lastAttention = _clock;
        if (State is PetState.Belly or PetState.BellyDown or PetState.BellyUp) return;
        if (State == PetState.Rest) { Enter(PetState.RestUp, 0); return; }
        if (_chasing || _zoomLaps > 0 || _fetch != 0 || BallHeld) return;
        if (State is not (PetState.Sit or PetState.Walk or PetState.Run or PetState.Wag or PetState.Happy or PetState.Sniff or PetState.Paw)) return;
        _targetX = null; _ask = 0; _following = false; _seek = 0; _seekGo = false;
        if (_anims.ContainsKey("belly") && _anims.ContainsKey("bellydown")) Set(PetState.BellyDown);
        else Set(PetState.Happy, Rand(1.5, 2.5));
    }

    /// <summary>There's a horse or a dog on the screen at x: bark at it (not too often).</summary>
    public void BarkAt(double x)
    {
        if (_mood != Mood.Normal || !Grounded || _clock < _nextBark || !_anims.ContainsKey("bark")) return;
        if (_chasing || _zoomLaps > 0 || _fetch != 0 || BallHeld) return;
        if (State is not (PetState.Sit or PetState.Walk or PetState.Run or PetState.Wag or PetState.Happy or PetState.Sniff)) return;
        _nextBark = _clock + Rand(12, 25);
        _targetX = null; _ask = 0; _following = false; _seek = 0; _seekGo = false;
        FacingRight = x >= X;
        Set(PetState.Bark, Rand(2.5, 4.5));
    }

    /// <summary>Run off the nearest side of the screen and come back with her ball.</summary>
    private bool StartFetch()
    {
        if (!HasBallAnims || BallActive || _support?.Kind != PlatformKind.Floor || _areas.Count == 0) return false;
        var (lo, hi) = SupportSpan();
        double min = _areas.Min(a => a.Left), max = _areas.Max(a => a.Right);
        bool canLeft = lo <= min + HalfWidth + 3, canRight = hi >= max - HalfWidth - 3;
        if (!canLeft && !canRight) return false;
        bool left = canLeft && (!canRight || X - min < max - X);
        _fetch = 1;
        _targetX = left ? min - HalfWidth * 1.5 - 10 : max + HalfWidth * 1.5 + 10;
        Set(PetState.Run, double.PositiveInfinity);
        return true;
    }

    /// <summary>Sit facing her owner and stare.</summary>
    private void Stare(double seconds)
    {
        _nextVariant = _anims.ContainsKey("sit~look") ? "sit~look" : null;
        Set(PetState.Sit, seconds);
    }

    /// <summary>Throw (or drop) her ball. Position is the ball's centre-bottom.</summary>
    public void ThrowBall(double x, double y, double vx, double vy)
    {
        if (_mood != Mood.Normal || BallHeld) return;
        _lastAttention = _clock;
        BallActive = true; BallX = x; BallY = y;
        _ballVx = Math.Clamp(vx, -2500, 2500); _ballVy = Math.Clamp(vy, -2500, 2500);
        _ballRest = false; _ballIdle = 0; _ballWanted = true; _ask = 0;
        _ballPlays = _rng.Next(3, 7);
        _chaseAfter = _clock + 0.25;
    }

    /// <summary>Her ball drops in from above somewhere near her.</summary>
    public void DropBallNearby()
    {
        double x = X + (_rng.Next(2) == 0 ? -1 : 1) * Rand(150, 500) * _s;
        if (_areas.Count > 0) x = Math.Clamp(x, _areas.Min(a => a.Left) + 40 * _s, _areas.Max(a => a.Right) - 40 * _s);
        double seen = _lastAttention;
        ThrowBall(x, Y - 600 * _s, Rand(-150, 150) * _s, 0);
        _lastAttention = seen; // her own ball turning up isn't her owner paying attention
    }

    /// <summary>Trot after the mouse pointer, sit and watch it, and pounce when it holds still.</summary>
    private void StartFollow()
    {
        _following = true; _followUntil = _clock + Rand(8, 16);
        FollowMove();
    }

    private void FollowMove()
    {
        var (lo, hi) = SupportSpan();
        double t = Math.Clamp(CursorX ?? X, lo, hi);
        if (Math.Abs(t - X) < 110 * _s) { _targetX = null; Set(PetState.Sit, double.PositiveInfinity); return; }
        _targetX = t;
        Set(Math.Abs(t - X) > 450 * _s ? PetState.Run : PetState.Walk, double.PositiveInfinity);
    }

    /// <summary>Nobody's fussed her for ages: go and sit by the pointer, stare, paw, woof.</summary>
    private void SeekAttention()
    {
        var (lo, hi) = SupportSpan();
        double t = Math.Clamp(CursorX ?? X, lo, hi);
        if (Math.Abs(t - X) < 120 * _s) { _seek = 1; Stare(Rand(3, 5)); return; }
        _seekGo = true; _targetX = t;
        Set(Math.Abs(t - X) > 450 * _s ? PetState.Run : PetState.Walk, double.PositiveInfinity);
    }

    private void ReleaseBall(double vx, double vy)
    {
        if (!BallHeld) return;
        BallHeld = false;
        BallX = X + (FacingRight ? 1 : -1) * NoseReach; BallY = Y - 20 * _s;
        _ballVx = vx; _ballVy = vy; _ballRest = false; _ballIdle = 0; _ballWanted = false;
    }

    private void UpdateBall(double dt)
    {
        if (!BallActive || BallHeld) return;
        bool onGround = _platforms.Any(p => p.Spans(BallX) && Math.Abs(p.Y - BallY) <= 3);
        if (_ballRest)
        {
            if (onGround) { _ballIdle += dt; if (_ballIdle > 45) BallActive = false; return; }
            _ballRest = false; // the window it was lying on moved away
        }
        _ballVy += Gravity * dt;
        double nx = BallX + _ballVx * dt, ny = BallY + _ballVy * dt;
        if (_areas.Count > 0)
        {
            double minX = _areas.Min(a => a.Left) + BallRadius, maxX = _areas.Max(a => a.Right) - 1 - BallRadius;
            if (nx < minX || nx > maxX) { nx = Math.Clamp(nx, minX, maxX); _ballVx = -_ballVx * 0.6; }
        }
        if (_ballVy > 0)
        {
            Platform? land = null;
            foreach (var p in _platforms)
                if (p.Spans(nx) && p.Y >= BallY - 0.01 && p.Y <= ny && (land is null || p.Y < land.Value.Y))
                    land = p;
            if (land is { } l)
            {
                ny = l.Y;
                if (_ballVy > 260 * _s) { _ballVy = -_ballVy * 0.55; _ballVx *= 0.85; }
                else
                {
                    // rolling
                    _ballVy = 0;
                    double slow = 350 * _s * dt;
                    _ballVx = Math.Abs(_ballVx) <= slow ? 0 : _ballVx - Math.Sign(_ballVx) * slow;
                    if (_ballVx == 0) { _ballRest = true; _ballIdle = 0; }
                }
            }
        }
        BallRoll += nx - BallX;
        BallX = nx; BallY = ny;
        var floors = _platforms.Where(p => p.Kind == PlatformKind.Floor).ToList();
        if (floors.Count > 0 && BallY > floors.Max(p => p.Y) + 400) BallActive = false;
    }

    /// <summary>The stretch of her current surface she can stand on without leaving the screen.</summary>
    private (double Lo, double Hi) SupportSpan()
    {
        if (_support is not { } s) return (X, X);
        double lo = s.X1 + 1, hi = s.X2 - 1;
        // Side-by-side monitors are separate floors at the same height: treat them as one run.
        for (bool grew = true; grew;)
        {
            grew = false;
            foreach (var p in _platforms)
            {
                if (Math.Abs(p.Y - s.Y) > 3) continue;
                if (p.X1 + 1 < lo && p.X2 >= lo - 3) { lo = p.X1 + 1; grew = true; }
                if (p.X2 > hi + 1 && p.X1 <= hi + 3) { hi = p.X2 - 1; grew = true; }
            }
        }
        if (_areas.Count > 0)
        {
            lo = Math.Max(lo, _areas.Min(a => a.Left) + HalfWidth);
            hi = Math.Min(hi, _areas.Max(a => a.Right) - HalfWidth);
        }
        return (lo, hi);
    }

    public void BeginDrag(double cx, double cy)
    {
        _dragDx = X - cx; _dragDy = Y - cy;
        _lastAttention = _clock; _following = false; _seek = 0; _seekGo = false;
        ReleaseBall(0, 0);
        _zoomLaps = 0; _chasing = false; _greeting = false;
        _support = null; _targetX = null;
        Set(PetState.Dangle);
    }

    public void DragTo(double cx, double cy)
    {
        if (State != PetState.Dangle) return;
        X = cx + _dragDx; Y = cy + _dragDy;
    }

    public void EndDrag(double vx, double vy)
    {
        if (State != PetState.Dangle) return;
        EnterAir(Math.Clamp(vx, -1500, 1500), Math.Clamp(vy, -1500, 1500));
    }

    // ---------- physics ----------

    private bool UpdateSupport()
    {
        Platform? found = null;
        if (_support is { } s)
        {
            // Follow the same surface if its window moved.
            found = Nearest(_platforms.Where(p => p.Kind == s.Kind && p.WindowId == s.WindowId && p.Spans(X)));
        }
        found ??= Nearest(_platforms.Where(p => p.Spans(X) && Math.Abs(p.Y - Y) <= 3));
        if (found is null) return false;
        _support = found;
        Y = found.Value.Y;
        return true;
    }

    private Platform? Nearest(IEnumerable<Platform> ps)
    {
        Platform? best = null;
        foreach (var p in ps)
            if (best is null || Math.Abs(p.Y - Y) < Math.Abs(best.Value.Y - Y)) best = p;
        return best;
    }

    private bool CanStand(double x) =>
        _platforms.Any(p => p.Spans(x) && Math.Abs(p.Y - Y) <= 3) && OnScreen(x);

    private bool OnScreen(double x) =>
        _areas.Count == 0 || (x - HalfWidth >= _areas.Min(a => a.Left) && x + HalfWidth <= _areas.Max(a => a.Right));

    private void EnterAir(double vx, double vy)
    {
        _support = null; _targetX = null;
        ReleaseBall(vx, vy);
        _zoomLaps = 0; _chasing = false; _greeting = false; _fetch = 0; _ask = 0;
        Vx = vx; Vy = vy;
        if (Math.Abs(vx) > 1) FacingRight = vx > 0;
        Set(PetState.Air);
    }

    private void UpdateAir(double dt)
    {
        Vy += Gravity * dt;
        double nx = X + Vx * dt, ny = Y + Vy * dt;

        if (_areas.Count > 0)
        {
            double minX = _areas.Min(a => a.Left) + HalfWidth, maxX = _areas.Max(a => a.Right) - 1 - HalfWidth;
            if (nx < minX || nx > maxX) { nx = Math.Clamp(nx, minX, maxX); Vx = -Vx * 0.3; }
        }

        if (Vy > 0)
        {
            Platform? land = null;
            foreach (var p in _platforms)
                if (p.Spans(nx) && p.Y >= Y - 0.01 && p.Y <= ny && (land is null || p.Y < land.Value.Y))
                    land = p;
            if (land is { } l)
            {
                X = nx; Y = l.Y; Vx = Vy = 0; _support = l;
                OnLanded();
                return;
            }
        }
        X = nx; Y = ny;

        // Fell into a gap between monitors, or off the bottom: put her back on a floor.
        var floors = _platforms.Where(p => p.Kind == PlatformKind.Floor).ToList();
        if (floors.Count > 0 && !floors.Any(p => p.Spans(X)))
        {
            var f = floors.MinBy(p => Math.Min(Math.Abs(p.X1 - X), Math.Abs(p.X2 - X)));
            X = Math.Clamp(X, f.X1 + 1, f.X2 - 1);
        }
        if (floors.Count > 0 && Y > floors.Max(p => p.Y) + 400) Spawn(X, floors.Min(p => p.Y) - 600);
    }

    private void OnLanded()
    {
        if (_mood is Mood.Nap or Mood.Doze) Decide();
        else Set(PetState.Sit, Rand(0.5, 1.2));
    }

    // ---------- behaviour ----------

    private void UpdateGrounded(double dt)
    {
        // Ball in play while she's splooted: up first.
        if (State == PetState.Rest && _mood == Mood.Normal && BallActive && !BallHeld && _ballWanted && _clock >= _chaseAfter)
        { Enter(PetState.RestUp, 0); return; }
        // Ball in play: drop what she's doing and go after it.
        if (_mood == Mood.Normal && BallActive && !BallHeld && _ballWanted && !_chasing && _clock >= _chaseAfter &&
            _fetch == 0 &&
            State is PetState.Sit or PetState.Walk or PetState.Run or PetState.Wag or PetState.Sniff or PetState.Happy or PetState.Paw or PetState.Bark)
        {
            _chasing = true; _chaseTime = 0; _zoomLaps = 0; _greeting = false; _targetX = null;
            _following = false; _seek = 0; _seekGo = false;
            Set(PetState.Run, double.PositiveInfinity);
            return;
        }
        if (_following && State == PetState.Sit)
        {
            // Sitting watching the pointer.
            if (_clock > _followUntil || CursorX is null) { _following = false; Decide(); return; }
            var (lo, hi) = SupportSpan();
            double dx = Math.Clamp(CursorX.Value, lo, hi) - X;
            if (Math.Abs(dx) > 30 * _s) FacingRight = dx > 0;
            if (Math.Abs(dx) > 260 * _s) { FollowMove(); return; }            // it's getting away
            if (_stateTime > 1.2 && _clock - _cursorMovedAt > 1.5 && Math.Abs(dx) > 50 * _s)
            {
                // It's stopped... pounce!
                _following = false;
                const double air = 0.45;
                _jumpV = (dx / air, -Gravity * air / 2);
                if (_anims.ContainsKey("jumpprep")) Set(PetState.Crouch);
                else EnterAir(_jumpV.Vx, _jumpV.Vy);
            }
            return;
        }
        if (_fetch == 2)
        {
            // Out of sight, finding her ball.
            if (_stateTime < _fetchWait) return;
            _fetch = 3;
            BallActive = true; BallHeld = true; _ballPlays = _rng.Next(2, 5); _ballWanted = false;
            double min = _areas.Min(a => a.Left), max = _areas.Max(a => a.Right);
            bool fromLeft = X < min;
            // Bring it towards the pointer, but not on a route march across two monitors.
            double far = 750 * _s, near = 200 * _s;
            double want = CursorX ?? (fromLeft ? min + far : max - far);
            _targetX = fromLeft ? Math.Clamp(want, min + near, min + far) : Math.Clamp(want, max - far, max - near);
            FacingRight = fromLeft;
            _posture = Posture.Stand;
            Enter(PetState.Carry, double.PositiveInfinity);
            return;
        }
        switch (State)
        {
            case PetState.Walk:
            case PetState.Run:
                Move(dt);
                break;
            case PetState.Scratch:
            case PetState.Dig:
            case PetState.Spin:
            case PetState.RestUp:
                if (AnimFinished()) Decide();
                break;
            case PetState.Circle:
                // Walking a tight circle, seen side-on: a few steps, turn away, a few steps back, turn towards us. Twice round.
                if (_circleAnim == "walk")
                {
                    double step = _cfg.WalkSpeed * 0.7 * _s * dt, nx = X + (FacingRight ? step : -step);
                    if (_circleDist < 36 * _s && CanStand(nx)) { X = nx; _circleDist += step; }
                    else _circleAnim = _circleHalf % 2 == 0 ? "turnback" : "turn";
                }
                else if (Anim == _circleAnim && AnimFinished())
                {
                    FacingRight = !FacingRight;
                    if (++_circleHalf >= 4) Set(PetState.LieDown);
                    else { _circleAnim = "walk"; _circleDist = 0; }
                }
                break;
            case PetState.RestDown:
                if (AnimFinished()) Enter(PetState.Rest, _restFor);
                break;
            case PetState.Rest:
                if (_stateTime >= _stateDuration) Enter(PetState.RestUp, 0);
                break;
            case PetState.Paw:
            case PetState.BellyUp:
                if (AnimFinished()) { if (State == PetState.BellyUp) WagFor(Rand(1.5, 3)); else Decide(); }
                break;
            case PetState.BellyDown:
                if (AnimFinished()) Enter(PetState.Belly, Rand(3, 5));
                break;
            case PetState.Belly:
                if (_stateTime >= _stateDuration && _clock >= _petUntil) Enter(PetState.BellyUp, 0);
                break;
            case PetState.Carry:
                if (_stateTime > 14) { Enter(PetState.Drop, 0); break; }
                Move(dt);
                break;
            case PetState.PickUp:
                if (!BallHeld && _animTime >= 0.35 * _anims[Anim].Duration) BallHeld = true;
                if (AnimFinished())
                {
                    BallHeld = true;
                    var (lo, hi) = SupportSpan();
                    double tx = Math.Clamp(CursorX ?? X + (_rng.Next(2) == 0 ? -1 : 1) * 400 * _s, lo, hi);
                    if (Math.Abs(tx - X) < 80 * _s) tx = Math.Clamp(X + (X - lo > hi - X ? -1 : 1) * Rand(200, 500) * _s, lo, hi);
                    _targetX = tx;
                    Enter(PetState.Carry, double.PositiveInfinity);
                }
                break;
            case PetState.Drop:
                if (BallHeld && _animTime >= 0.6 * _anims[Anim].Duration)
                {
                    BallHeld = false;
                    BallX = X + (FacingRight ? 1 : -1) * NoseReach; BallY = Y;
                    _ballVx = _ballVy = 0; _ballRest = true; _ballIdle = 0; _ballWanted = false;
                }
                if (AnimFinished())
                {
                    // "Throw it for me!": sit and stare at her owner (and, if that doesn't work, a paw on the knee)...
                    if (_rng.NextDouble() < 0.65) { _ask = 1; Stare(Rand(5, 8)); }
                    // ...or a play bow / wag.
                    else if (_anims.ContainsKey("playbow") && _rng.NextDouble() < 0.5) Set(PetState.PlayBow);
                    else WagFor(Rand(2, 4));
                }
                break;
            case PetState.Greet:
                if (AnimFinished())
                {
                    if (_rng.NextDouble() < 0.4 * Pep) StartZoomies();
                    else WagFor(Rand(2, 4));
                }
                break;
            case PetState.PlayBow:
                if (AnimFinished())
                {
                    if (_zoomLaps > 0) NextLap(_rng.Next(2) == 0 ? -1 : 1);
                    else WagFor(Rand(1.5, 3));
                }
                break;
            case PetState.LieDown:
                // The more worn out she is (and the later it is), the longer she's out.
                if (AnimFinished()) Set(PetState.Sleep, _mood != Mood.Normal ? double.PositiveInfinity
                    : _cfg.SleepCycle ? (Rand(15, 30) + 150 * Tiredness) * Drowsy : Rand(8, 20));
                break;
            case PetState.Stretch:
            case PetState.Yawn:
            case PetState.GetUp:
                if (AnimFinished()) Decide();
                break;
            case PetState.Sniff:
                // A one-shot sniff (head down, snuffle, head up) plays through; a looping one is timed.
                if (_anims[Anim].Loop ? _stateTime >= _stateDuration : AnimFinished()) Decide();
                break;
            case PetState.Turn:
                if (AnimFinished())
                {
                    FacingRight = !FacingRight;
                    Enter(_pending.State, _pending.Duration);
                }
                break;
            case PetState.Transition:
                if (AnimFinished()) Enter(_pending.State, _pending.Duration);
                break;
            case PetState.Crouch:
                if (AnimFinished()) { bool f = FacingRight; EnterAir(_jumpV.Vx, _jumpV.Vy); FacingRight = f; }
                break;
            case PetState.Sleep:
                if (_mood == Mood.Normal && _stateTime >= _stateDuration) WakeUp();
                else if (_variant is null && _stateTime >= _rollAt && _anims.ContainsKey("rollover") && _anims.ContainsKey("sleep~belly"))
                {
                    // Long nap: roll over onto her back.
                    _pending = (PetState.Sleep, Math.Max(0, _stateDuration - _stateTime));
                    _nextVariant = "sleep~belly";
                    _transitionAnim = "rollover";
                    Enter(PetState.Transition, 0);
                }
                break;
            default:
                if (_stateTime >= _stateDuration) Decide();
                break;
        }
    }

    private void Move(double dt)
    {
        double speed = State switch
        {
            PetState.Run => _cfg.RunSpeed * (_zoomLaps > 0 ? 1.5 : 1),
            PetState.Carry => _cfg.WalkSpeed * 2,
            _ => _cfg.WalkSpeed,
        } * _s;

        if (_chasing) { Chase(dt, speed); return; }
        if (_following)
        {
            if (_clock > _followUntil || CursorX is null) { _following = false; _targetX = null; Decide(); return; }
            var (lo, hi) = SupportSpan();
            double t2 = Math.Clamp(CursorX.Value, lo, hi);
            if (Math.Abs(t2 - X) < 90 * _s) { _targetX = null; Set(PetState.Sit, double.PositiveInfinity); return; }
            _targetX = t2;
        }

        if (_targetX is { } t)
        {
            if (Math.Abs(t - X) <= speed * dt) { X = t; _targetX = null; OnArrived(); return; }
            _walkDir = Math.Sign(t - X);
        }
        if (FacingRight != _walkDir > 0)
        {
            // Turn around properly (if there's an animation for it) before heading the other way.
            // Zoomies and ball-carrying flip on the spot: no time for ceremony.
            if (_zoomLaps == 0 && State != PetState.Carry && BeginTurn(State, Math.Max(0, _stateDuration - _stateTime))) return;
            FacingRight = _walkDir > 0;
        }
        double nx = X + _walkDir * speed * dt;

        if (_fetch != 0)
        {
            X = nx; // free to run past the edge of the screen and back
            if (_fetch == 3 && OnScreen(X) && CanStand(X)) _fetch = 0;
        }
        else if (CanStand(nx)) X = nx;
        else if (_zoomLaps > 0 || _greeting || State == PetState.Carry) { _targetX = null; OnArrived(); return; }
        else if (_support?.Kind == PlatformKind.Window &&
                 (_mood == Mood.Nap || _rng.NextDouble() < 0.35 * _cfg.Energy))
        {
            X = nx; // hop off the edge
            EnterAir(_walkDir * speed * 0.8, -250 * _s);
            return;
        }
        else if (_mood == Mood.Nap && _support?.Kind == PlatformKind.Floor)
        {
            Set(PetState.LieDown); // can't reach the corner (e.g. other monitor): nap here
            return;
        }
        else
        {
            _walkDir = -_walkDir;
            _targetX = null;
        }

        if (_targetX is null && _stateTime >= _stateDuration && _mood == Mood.Normal) Decide();
        else if (_targetX is null && double.IsInfinity(_stateDuration) && _mood == Mood.Normal) Decide();
    }

    private void Chase(double dt, double speed)
    {
        if (!BallActive || BallHeld || _mood != Mood.Normal) { _chasing = false; Decide(); return; }
        _chaseTime += dt;
        double dx = BallX - X;
        bool level = Math.Abs(BallY - Y) <= 6 * _s;
        bool slow = Math.Abs(_ballVx) < 180 * _s && _ballVy == 0;
        if (level && slow && Math.Abs(dx) <= NoseReach + speed * dt) { ReachBall(); return; }
        if (_chaseTime > 12 || (_ballRest && BallY < Y - 30 * _s && !TryJump(BallX, BallY)))
        {
            // Can't get to it: sulk for a moment and forget about it.
            if (_ballRest && BallY < Y - 30 * _s || _chaseTime > 12)
            {
                _chasing = false; _ballWanted = false;
                Set(PetState.Sit, Rand(1.5, 3));
            }
            return;
        }
        if (State != PetState.Run) return; // TryJump took over
        if (Math.Abs(dx) > NoseReach * 0.5) _walkDir = dx > 0 ? 1 : -1;
        FacingRight = _walkDir > 0;
        if (Math.Abs(dx) <= NoseReach && level) return; // on top of it while it's still bouncing: wait
        double nx = X + _walkDir * speed * dt;
        if (CanStand(nx)) X = nx;
        else if (_support?.Kind == PlatformKind.Window)
        {
            X = nx;
            EnterAir(_walkDir * speed * 0.6, -250 * _s); // after it, off the edge
        }
    }

    private void ReachBall()
    {
        _chasing = false;
        FacingRight = BallX >= X;
        _ballVx = _ballVy = 0; _ballRest = true; _ballIdle = 0;
        BallX = X + (FacingRight ? 1 : -1) * NoseReach;
        _ballPlays--;
        if (HasBallAnims && (_ballPlays <= 0 || _rng.NextDouble() < 0.5)) { _ballWanted = false; Enter(PetState.PickUp, 0); }
        else Kick();
    }

    /// <summary>Bat the ball away with her nose and go after it again.</summary>
    private void Kick()
    {
        int dir = BallX >= X ? 1 : -1;
        FacingRight = dir > 0;
        // Keep it on screen: bat it towards the side with more room.
        if (_areas.Count > 0)
        {
            double left = BallX - _areas.Min(a => a.Left), right = _areas.Max(a => a.Right) - BallX;
            if (dir > 0 && right < 500 * _s && left > right) dir = -1;
            else if (dir < 0 && left < 500 * _s && right > left) dir = 1;
        }
        _ballVx = dir * Rand(350, 800) * _s; _ballVy = -Rand(500, 950) * _s;
        _ballRest = false; _ballIdle = 0;
        _ballWanted = _ballPlays > 0;
        _chaseAfter = _clock + 0.35;
        WagFor(0.6);
    }

    /// <summary>Stand there wagging (or just sit, if there's no wag animation).</summary>
    private void WagFor(double seconds) => Set(_anims.ContainsKey("wag") ? PetState.Wag : PetState.Sit, seconds);

    private void StartZoomies()
    {
        _zoomLaps = _rng.Next(3, 7); _targetX = null; _chasing = false; _greeting = false;
        if (_anims.ContainsKey("playbow")) Set(PetState.PlayBow);
        else NextLap(_rng.Next(2) == 0 ? -1 : 1);
    }

    private void NextLap(int dir)
    {
        var (lo, hi) = SupportSpan();
        double tx = Math.Clamp(X + dir * Rand(450, 1200) * _s, lo, hi);
        if (Math.Abs(tx - X) < 200 * _s) tx = Math.Clamp(X - dir * Rand(450, 1200) * _s, lo, hi);
        if (Math.Abs(tx - X) < 60 * _s) { _zoomLaps = 0; WagFor(Rand(1.5, 3)); return; } // no room up here
        _targetX = tx;
        Set(PetState.Run, double.PositiveInfinity);
    }

    private void OnArrived()
    {
        if (_fetch == 1) { _fetch = 2; _fetchWait = Rand(1.5, 4); Enter(PetState.Sit, double.PositiveInfinity); }
        else if (_mood == Mood.Nap) Set(PetState.LieDown);
        else if (State == PetState.Carry) { _fetch = 0; Enter(PetState.Drop, 0); }
        else if (_greeting) { _greeting = false; Set(PetState.Greet); }
        else if (_seekGo) { _seekGo = false; _seek = 1; Stare(Rand(3, 5)); }
        else if (_following) Set(PetState.Sit, double.PositiveInfinity);
        else if (_zoomLaps > 0)
        {
            int dir = _walkDir;
            if (--_zoomLaps > 0) NextLap(-dir);
            else if (_anims.ContainsKey("playbow") && _rng.NextDouble() < 0.4) Set(PetState.PlayBow);
            else WagFor(Rand(2, 4));
        }
        else Decide();
    }

    private double NapCornerX()
    {
        var home = _areas.Count > 0 ? _areas[0] : new RectI(0, 0, 1920, 1080);
        double margin = 60 * _s;
        return _cfg.NapCorner.EndsWith("left", StringComparison.OrdinalIgnoreCase) ? home.Left + margin : home.Right - margin;
    }

    private bool InNapCorner() =>
        _support?.Kind == PlatformKind.Floor && Math.Abs(X - NapCornerX()) < 20 * _s;

    private void Decide()
    {
        _targetX = null;
        if (_mood is Mood.Doze or Mood.Nap && State is PetState.Belly or PetState.BellyDown)
        { Enter(PetState.BellyUp, 0); return; } // roll back onto her feet first
        if (_mood is Mood.Doze or Mood.Nap && State is PetState.Rest or PetState.RestDown)
        { Enter(PetState.RestUp, 0); return; }
        switch (_mood)
        {
            case Mood.Doze:
                if (State is not (PetState.Sleep or PetState.Circle)) Set(PetState.LieDown);
                return;

            case Mood.Nap:
            {
                double cx = NapCornerX();
                if (_support?.Kind == PlatformKind.Window)
                {
                    _walkDir = cx >= X ? 1 : -1; // run to the edge and jump off
                    Set(PetState.Run, double.PositiveInfinity);
                }
                else if (InNapCorner()) Set(PetState.LieDown);
                else
                {
                    _targetX = cx;
                    Set(Math.Abs(cx - X) > 300 * _s ? PetState.Run : PetState.Walk, double.PositiveInfinity);
                }
                return;
            }
        }

        _zoomLaps = 0; _greeting = false; _chasing = false;
        _following = false; _seekGo = false;
        if (_greetPending) { _ask = 0; _seek = 0; StartGreet(); return; }
        if (_seek > 0)
        {
            _seek++;
            if (_seek == 2 && _anims.ContainsKey("paw")) { Set(PetState.Paw); return; }
            if (_seek == 4 && _anims.ContainsKey("bark") && CursorX is { } bx) { FacingRight = bx >= X; Set(PetState.Bark, Rand(1.5, 2.5)); return; }
            if (_seek is 3 or 5) { Stare(Rand(2, 4)); return; }
            if (_seek < 6) { Decide(); return; }
            _seek = 0;
            _lastAttention = _clock - 150; // try again in a few minutes
        }
        // Ignored for a good while, and her owner's clearly about (the pointer's moving): go and ask for a fuss.
        if (_ask == 0 && CursorX is not null && _clock - _lastAttention > 300 && _clock - _cursorMovedAt < 15 &&
            _rng.NextDouble() < 0.5)
        { SeekAttention(); return; }
        if (_ask > 0)
        {
            bool waiting = BallActive && !BallHeld && _ballRest && Math.Abs(BallX - X) < 4 * NoseReach && Math.Abs(BallY - Y) <= 6 * _s;
            if (waiting && _ask < 5)
            {
                _ask++;
                if (_ask is 2 or 4 && _anims.ContainsKey("paw")) Set(PetState.Paw); // paw on the knee
                else Stare(Rand(3, 5));
                return;
            }
            _ask = 0; // gave up waiting (she may well play with it herself, below)
        }
        // Her ball is lying right there and she isn't done with it: bat it away and chase it.
        if (BallActive && !BallHeld && _ballRest && _ballPlays > 0 && Math.Abs(BallX - X) < 3 * NoseReach &&
            Math.Abs(BallY - Y) <= 6 * _s && _rng.NextDouble() < 0.7)
        { _ballPlays--; Kick(); return; }

        // Worn out: a big yawn, then bed.
        if (_cfg.SleepCycle && Tiredness >= 0.85 && !BallHeld)
        {
            if (!_bedtimeYawn && _posture != Posture.Lie) { _bedtimeYawn = true; Set(PetState.Yawn); return; }
            _bedtimeYawn = false;
            Set(PetState.LieDown); return;
        }
        _bedtimeYawn = false;

        if (_rng.NextDouble() < 0.2 * Pep && TryJump()) return;

        double e = Pep, tired = _cfg.SleepCycle ? Tiredness : 0;
        bool onFloor = _support?.Kind == PlatformKind.Floor;
        var options = new (PetState S, double W)[]
        {
            (PetState.Sit, 3), (PetState.Walk, 4), (PetState.Run, 0.8 * e), (PetState.Sniff, 1.2),
            (PetState.Yawn, 0.6 + 3 * tired), (PetState.Stretch, 0.5),
            (PetState.Wag, _anims.ContainsKey("wag") ? 1.6 : 0),
            (PetState.PlayBow, onFloor ? 0.45 * e : 0),                              // zoomies
            (PetState.Carry, onFloor && HasBallAnims && !BallActive ? 0.55 * e : 0), // fetch her ball
            (PetState.BellyDown, _anims.ContainsKey("belly") && _anims.ContainsKey("bellydown") ? 0.3 : 0),
            (PetState.Greet, CursorX is not null ? 1.0 : 0),                         // follow the pointer about
            (PetState.RestDown, _anims.ContainsKey("rest") && _anims.ContainsKey("restdown") ? 1.6 + 4 * tired : 0), // sploot
            (PetState.Scratch, _anims.ContainsKey("scratch") ? 0.5 : 0),
            (PetState.Dig, _anims.ContainsKey("dig") ? 0.35 : 0),
            (PetState.Spin, _anims.ContainsKey("spin") ? 0.35 * e : 0),              // chase her tail
            (PetState.LieDown, (_cfg.SleepCycle ? 0.08 + 0.4 * tired : 0.3) / _cfg.Energy + 4 * tired * tired * tired * Drowsy),                                             // keep last: wins outright at energy 0
        };
        double roll = _rng.NextDouble() * options.Sum(o => o.W);
        var pick = options[^1].S;
        foreach (var o in options) { if ((roll -= o.W) < 0) { pick = o.S; break; } }

        switch (pick)
        {
            case PetState.Walk: _walkDir = _rng.Next(2) == 0 ? -1 : 1; Set(pick, Rand(2, 7)); break;
            case PetState.Run: _walkDir = _rng.Next(2) == 0 ? -1 : 1; Set(pick, Rand(1, 3)); break;
            case PetState.Sit: Set(pick, Rand(2, 6)); break;
            case PetState.Sniff: Set(pick, Rand(1.5, 3)); break;
            case PetState.Wag: Set(pick, Rand(2, 5)); break;
            case PetState.PlayBow: StartZoomies(); break;
            case PetState.Carry:
                // Usually she trots off-screen to fetch it; sometimes it just turns up.
                if (_rng.NextDouble() < 0.8 && StartFetch()) break;
                DropBallNearby(); WagFor(0.5); break;
            case PetState.Greet: StartFollow(); break;
            case PetState.RestDown: _restFor = Rand(8, 25); Set(pick); break;
            default: Set(pick); break;
        }
    }

    /// <summary>Leap up onto a nearby higher window edge, if one is in reach.</summary>
    private bool TryJump(double? aimX = null, double? aimY = null)
    {
        double maxUp = 350 * _s, maxDx = 450 * _s, pad = 30 * _s;
        var candidates = new List<(Platform P, double Tx)>();
        foreach (var p in _platforms)
        {
            if (p.Kind != PlatformKind.Window || p.Width < 2 * pad + 20) continue;
            if (p.Y >= Y - 30 * _s || p.Y < Y - maxUp) continue;
            if (aimX is { } ax && !(p.Spans(ax) && Math.Abs(p.Y - aimY!.Value) <= 4)) continue;
            double tx = Math.Clamp(aimX ?? X, p.X1 + pad, p.X2 - pad);
            if (Math.Abs(tx - X) <= maxDx) candidates.Add((p, tx));
        }
        if (candidates.Count == 0) return false;

        var (target, txPick) = candidates[_rng.Next(candidates.Count)];
        double clearance = 40 * _s;
        double rise = Y - target.Y + clearance;
        double vy0 = -Math.Sqrt(2 * Gravity * rise);
        double time = -vy0 / Gravity + Math.Sqrt(2 * clearance / Gravity);
        FacingRight = txPick >= X;
        _jumpV = ((txPick - X) / time, vy0);
        if (_anims.ContainsKey("jumpprep")) Set(PetState.Crouch); // crouch first, take off when it finishes
        else EnterAir(_jumpV.Vx, _jumpV.Vy);
        return true;
    }

    // ---------- animation ----------

    /// <summary>Change state, playing a sit-down / stand-up transition first if the posture changes.</summary>
    private void Set(PetState s, double duration = 0)
    {
        // Round and round before she settles, like dogs do.
        if (s == PetState.LieDown && State != PetState.Circle && _posture != Posture.Lie &&
            _anims.ContainsKey("turnback") && _anims.ContainsKey("turn") && _anims.ContainsKey("walk"))
        { s = PetState.Circle; _circleAnim = "walk"; _circleDist = 0; _circleHalf = 0; }
        var want = PostureOf(s);
        if (want != Posture.None && _posture != Posture.None && want != _posture)
        {
            string? t = (_posture, want) switch
            {
                (Posture.Stand, Posture.Sit) => "sitdown",
                (Posture.Sit, Posture.Stand) => "standup",
                _ => null,
            };
            if (t is not null && _anims.ContainsKey(t))
            {
                _pending = (s, duration);
                _transitionAnim = t;
                Enter(PetState.Transition, 0);
                return;
            }
        }
        Enter(s, duration);
    }

    private void Enter(PetState s, double duration)
    {
        State = s;
        _stateTime = 0;
        _stateDuration = duration;
        _animTime = 0;
        // Sleep always starts curled up (that's how lie-down ends); belly-up only comes via a roll-over.
        _variant = _nextVariant ?? (s == PetState.Sleep ? null : PickVariant(BaseAnim(s)));
        if (s != PetState.Transition) _nextVariant = null;
        if (s == PetState.Sleep && _variant is null) _rollAt = Rand(25, 90);
        var p = s == PetState.Transition ? PostureOf(_pending.State) : EndPosture(s);
        if (p != Posture.None) _posture = p;
        UpdateAnim();
    }

    /// <summary>Get up from sleep, rolling back off her back first if she's belly-up.</summary>
    private void WakeUp()
    {
        if (_variant == "sleep~belly" && _anims.ContainsKey("rollback"))
        {
            _pending = (PetState.GetUp, 0);
            _transitionAnim = "rollback";
            Enter(PetState.Transition, 0);
        }
        else Set(PetState.GetUp);
    }

    private bool BeginTurn(PetState resume, double remaining)
    {
        if (!_anims.ContainsKey("turn")) return false;
        _pending = (resume, remaining);
        Enter(PetState.Turn, 0);
        return true;
    }

    private static Posture PostureOf(PetState s) => s switch
    {
        PetState.Sit or PetState.Happy or PetState.Yawn => Posture.Sit,
        PetState.Walk or PetState.Run or PetState.Sniff or PetState.Stretch or PetState.Crouch or PetState.Wag
            or PetState.Greet or PetState.PlayBow or PetState.PickUp or PetState.Drop or PetState.Carry or PetState.Bark or PetState.BellyDown => Posture.Stand,
        PetState.Dig or PetState.Spin or PetState.Circle or PetState.RestDown => Posture.Stand,
        PetState.Rest => Posture.Lie,
        PetState.Paw or PetState.Scratch => Posture.Sit,
        PetState.Belly => Posture.Lie,
        PetState.Sleep => Posture.Lie,
        _ => Posture.None,
    };

    private static Posture EndPosture(PetState s) => s switch
    {
        PetState.LieDown or PetState.BellyDown or PetState.RestDown => Posture.Lie,
        PetState.RestUp or PetState.BellyUp or PetState.GetUp or PetState.Turn or PetState.Air or PetState.Dangle => Posture.Stand,
        _ => PostureOf(s),
    };

    private static string BaseAnim(PetState s) => s.ToString().ToLowerInvariant();

    /// <summary>Folders named "sit~look", "sleep~belly" etc. are alternatives picked at random.</summary>
    private string? PickVariant(string baseName)
    {
        var options = _anims.Keys.Where(k => k.StartsWith(baseName + "~", StringComparison.Ordinal)).ToList();
        if (options.Count == 0) return null;
        if (_anims.ContainsKey(baseName)) { options.Add(baseName); options.Add(baseName); } // base is twice as likely
        return options[_rng.Next(options.Count)];
    }

    private string WantedAnim() => State switch
    {
        PetState.Air => Vy < 0 ? "jump" : "fall",
        PetState.Transition => _transitionAnim,
        PetState.Crouch => "jumpprep",
        PetState.Circle => _circleAnim,
        _ => _variant ?? BaseAnim(State),
    };

    public string Resolve(string name)
    {
        while (!_anims.ContainsKey(name)) name = Fallback.GetValueOrDefault(name, "sit");
        return name;
    }

    private void UpdateAnim()
    {
        string name = Resolve(WantedAnim());
        if (name != Anim) { Anim = name; _animTime = 0; }
        var a = _anims[name];
        int f = (int)(_animTime * a.Fps);
        Frame = a.Loop ? f % a.Frames : Math.Min(a.Frames - 1, f);
    }

    private bool AnimFinished()
    {
        var a = _anims[Anim];
        return a.Loop ? _stateTime >= 1.2 : _animTime >= a.Duration;
    }

    private double Rand(double lo, double hi) => lo + _rng.NextDouble() * (hi - lo);
}
