using Zoe.Core;

namespace Zoe.Core.Tests;

public class PlatformBuilderTests
{
    static readonly RectI Screen = new(0, 0, 1920, 1040);

    [Fact]
    public void FloorIsWorkAreaBottom()
    {
        var ps = PlatformBuilder.Build(new World([Screen], []), 100, 40);
        var floor = Assert.Single(ps);
        Assert.Equal(PlatformKind.Floor, floor.Kind);
        Assert.Equal(1040, floor.Y);
    }

    [Fact]
    public void WindowTopEdgeBecomesPlatform()
    {
        var w = new WindowInfo(7, new RectI(200, 400, 900, 800));
        var ps = PlatformBuilder.Build(new World([Screen], [w]), 100, 40);
        var p = Assert.Single(ps, p => p.Kind == PlatformKind.Window);
        Assert.Equal((200d, 900d, 400d, 7L), (p.X1, p.X2, p.Y, p.WindowId));
    }

    [Fact]
    public void MaximisedWindowHasNoRoomAbove()
    {
        var w = new WindowInfo(1, new RectI(0, 0, 1920, 1040));
        Assert.DoesNotContain(PlatformBuilder.Build(new World([Screen], [w]), 100, 40), p => p.Kind == PlatformKind.Window);
    }

    [Fact]
    public void EdgeCoveredByHigherWindowIsSplit()
    {
        var top = new WindowInfo(1, new RectI(400, 300, 600, 700));   // covers y=400 between 400..600
        var under = new WindowInfo(2, new RectI(200, 400, 900, 800));
        var ps = PlatformBuilder.Build(new World([Screen], [top, under]), 100, 40)
            .Where(p => p.WindowId == 2).OrderBy(p => p.X1).ToList();
        Assert.Equal(2, ps.Count);
        Assert.Equal((200d, 400d), (ps[0].X1, ps[0].X2));
        Assert.Equal((600d, 900d), (ps[1].X1, ps[1].X2));
    }
}

public class RuleEngineTests
{
    static readonly WindowInfo Yt = new(1, default, "Cute dogs - YouTube - Google Chrome", "chrome");
    static readonly WindowInfo Excel = new(2, default, "Budget.xlsx - Excel", "EXCEL");

    [Fact]
    public void MatchesTitleAndProcessCaseInsensitively()
    {
        Assert.True(RuleEngine.Matches(new Rule { Title = ["youtube"] }, Yt));
        Assert.True(RuleEngine.Matches(new Rule { Process = ["excel.exe"] }, Excel));
        Assert.False(RuleEngine.Matches(new Rule { Process = ["chrome"], Title = ["Netflix"] }, Yt));
        Assert.False(RuleEngine.Matches(new Rule(), Yt));
    }

    [Fact]
    public void ForegroundScopeOnlyChecksActiveWindow()
    {
        var rules = new[] { new Rule { Name = "yt", Title = ["YouTube"], Mood = Mood.Nap } };
        Assert.Null(RuleEngine.Evaluate(rules, Excel, [Excel, Yt]));
        Assert.Equal(Mood.Nap, RuleEngine.Evaluate(rules, Yt, [Yt])!.Value.Mood);
    }

    [Fact]
    public void AnyScopeAndStrongestMoodWins()
    {
        var rules = new[]
        {
            new Rule { Name = "yt", Title = ["YouTube"], Mood = Mood.Nap, Scope = "any" },
            new Rule { Name = "xl", Process = ["excel"], Mood = Mood.Hidden },
        };
        var r = RuleEngine.Evaluate(rules, Excel, [Excel, Yt])!.Value;
        Assert.Equal((Mood.Hidden, "xl"), (r.Mood, r.Rule));
    }

    [Fact]
    public void ConfigParsesWithCommentsAndEnums()
    {
        var c = ZoeConfig.Parse("""
            { // comment
              "napCorner": "bottom-left",
              "rules": [ { "name": "m", "title": ["Meeting"], "mood": "hidden", "scope": "any" }, ],
            }
            """);
        Assert.Equal("bottom-left", c.NapCorner);
        Assert.Equal(Mood.Hidden, c.Rules[0].Mood);
    }
}

public class PetSimTests
{
    static readonly RectI Screen = new(0, 0, 1920, 1040);
    static readonly RectI[] Areas = [Screen];

    static readonly Dictionary<string, AnimInfo> Anims = new()
    {
        ["sit"] = new(4, 6, true), ["walk"] = new(6, 10, true), ["run"] = new(6, 14, true),
        ["liedown"] = new(4, 8, false), ["sleep"] = new(4, 3, true), ["getup"] = new(4, 8, false),
        ["fall"] = new(2, 8, true),
    };

    static PetSim NewSim(ZoeConfig? cfg = null, int seed = 1) => new(Anims, cfg ?? new ZoeConfig(), new Random(seed));

    static List<Platform> Plats(params WindowInfo[] ws) => PlatformBuilder.Build(new World(Areas, ws), 100, 40);

    static void Run(PetSim sim, double seconds, IReadOnlyList<Platform> ps)
    {
        for (double t = 0; t < seconds; t += 1 / 60.0) sim.Tick(1 / 60.0, ps, Areas);
    }

    [Fact]
    public void FallsAndLandsOnWindowEdge()
    {
        var sim = NewSim();
        var ps = Plats(new WindowInfo(5, new RectI(500, 600, 1100, 900)));
        sim.Spawn(800, 0);
        Run(sim, 1.5, ps);
        Assert.True(sim.Grounded);
        Assert.Equal(600, sim.Y);
        Assert.Equal(5, sim.Support!.Value.WindowId);
    }

    [Fact]
    public void FollowsWindowWhenItMovesAndFallsWhenItCloses()
    {
        var sim = NewSim(new ZoeConfig { Energy = 0 }); // no jumping off on her own
        sim.Spawn(800, 0);
        Run(sim, 1.5, Plats(new WindowInfo(5, new RectI(0, 600, 1920, 900))));
        Run(sim, 0.2, Plats(new WindowInfo(5, new RectI(0, 450, 1920, 750))));
        Assert.Equal(450, sim.Y);
        Run(sim, 2, Plats());
        Assert.Equal(1040, sim.Y);
        Assert.Equal(PlatformKind.Floor, sim.Support!.Value.Kind);
    }

    [Fact]
    public void NapMoodGoesToCornerAndSleeps()
    {
        var sim = NewSim(new ZoeConfig { NapCorner = "bottom-right" });
        var ps = Plats(new WindowInfo(5, new RectI(300, 500, 1000, 900)));
        sim.Spawn(600, 0);
        Run(sim, 1.5, ps);
        sim.Mood = Mood.Nap;
        Run(sim, 20, ps);
        Assert.Equal(PetState.Sleep, sim.State);
        Assert.Equal(PlatformKind.Floor, sim.Support!.Value.Kind);
        Assert.InRange(sim.X, 1920 - 80, 1920 - 40);
    }

    [Fact]
    public void WakesUpWhenMoodReturnsToNormal()
    {
        var sim = NewSim();
        sim.Spawn(600, 900);
        Run(sim, 1, Plats());
        sim.Mood = Mood.Doze;
        Run(sim, 2, Plats());
        Assert.Equal(PetState.Sleep, sim.State);
        sim.Mood = Mood.Normal;
        Assert.Equal(PetState.GetUp, sim.State);
        Run(sim, 1, Plats());
        Assert.NotEqual(PetState.Sleep, sim.State);
    }

    [Fact]
    public void HiddenFreezesAndIsInvisible()
    {
        var sim = NewSim();
        sim.Spawn(600, 900);
        Run(sim, 1, Plats());
        sim.Mood = Mood.Hidden;
        double x = sim.X;
        Run(sim, 5, Plats());
        Assert.False(sim.Visible);
        Assert.Equal(x, sim.X);
    }

    [Fact]
    public void DragThenThrowLandsOnFloorWithinScreen()
    {
        var sim = NewSim();
        sim.Spawn(600, 900);
        Run(sim, 1, Plats());
        sim.BeginDrag(600, 980);
        sim.DragTo(1800, 200);
        Assert.Equal(PetState.Dangle, sim.State);
        sim.EndDrag(1500, -800);
        Run(sim, 3, Plats());
        Assert.True(sim.Grounded);
        Assert.Equal(1040, sim.Y);
        Assert.InRange(sim.X, 0, 1919);
    }

    [Fact]
    public void WandersForAWhileWithoutLeavingTheScreen()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var sim = NewSim(new ZoeConfig { Energy = 2 }, seed);
            var ps = Plats(new WindowInfo(1, new RectI(100, 700, 700, 1000)),
                           new WindowInfo(2, new RectI(600, 500, 1300, 900)),
                           new WindowInfo(3, new RectI(1200, 800, 1900, 1000)));
            sim.Spawn(900, 0);
            for (int i = 0; i < 60 * 120; i++)
            {
                sim.Tick(1 / 60.0, ps, Areas);
                Assert.InRange(sim.X, 0, 1920);
                Assert.InRange(sim.Y, -2000, 1040);
            }
        }
    }

    [Fact]
    public void MissingAnimationsFallBack()
    {
        var sim = NewSim();
        Assert.Equal("run", sim.Resolve("run"));
        Assert.Equal("sit", sim.Resolve("sniff"));
        Assert.Equal("sit", sim.Resolve("happy"));
        Assert.Equal("fall", sim.Resolve("dangle"));
    }
    static readonly Dictionary<string, AnimInfo> Rich = new(Anims)
    {
        ["turn"] = new(8, 16, false), ["sitdown"] = new(6, 16, false), ["standup"] = new(6, 16, false),
        ["sit~look"] = new(4, 8, true), ["sleep~belly"] = new(4, 4, true), ["sniff"] = new(4, 8, true),
        ["yawn"] = new(4, 8, true), ["stretch"] = new(6, 8, false), ["happy"] = new(4, 8, true), ["jump"] = new(4, 8, false),
    };

    [Fact]
    public void TurnsAroundBeforeWalkingTheOtherWay()
    {
        var sim = new PetSim(Rich, new ZoeConfig { Energy = 0 }, new Random(3));
        var ps = Plats();
        sim.Spawn(1000, 900);
        Run(sim, 1, ps);
        bool facing = sim.FacingRight;
        sim.Mood = Mood.Nap;  // corner is to the right; force a walk that may need a turn
        var states = new List<PetState>();
        for (int i = 0; i < 60 * 15; i++) { sim.Tick(1 / 60.0, ps, Areas); states.Add(sim.State); }
        if (!facing) Assert.Contains(PetState.Turn, states);
        Assert.Equal(PetState.Sleep, sim.State);
        Assert.InRange(sim.X, 1920 - 80, 1920 - 40);
    }

    [Fact]
    public void SitsDownAndStandsUpThroughTransitions()
    {
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 10; seed++)
        {
            var sim = new PetSim(Rich, new ZoeConfig(), new Random(seed));
            var ps = Plats();
            sim.Spawn(900, 900);
            for (int i = 0; i < 60 * 90; i++) { sim.Tick(1 / 60.0, ps, Areas); seen.Add(sim.Anim); }
        }
        Assert.Contains("sitdown", seen);
        Assert.Contains("standup", seen);
        Assert.Contains("sit~look", seen);
        Assert.Contains("turn", seen);
    }

    [Fact]
    public void WalkingNeverStartsFromSittingWithoutStandingUp()
    {
        var sim = new PetSim(Rich, new ZoeConfig(), new Random(5));
        var ps = Plats();
        sim.Spawn(900, 900);
        string prev = "";
        for (int i = 0; i < 60 * 120; i++)
        {
            sim.Tick(1 / 60.0, ps, Areas);
            if (sim.Anim is "walk" or "run" && prev is "sit" or "sit~look")
                Assert.Fail($"jumped from {prev} straight to {sim.Anim}");
            prev = sim.Anim;
        }
    }
}

public class ScreenEdgeTests
{
    [Fact]
    public void NeverWalksPartlyOffTheScreen()
    {
        var areas = new[] { new RectI(0, 0, 1920, 1040) };
        var anims = new Dictionary<string, AnimInfo> { ["sit"] = new(4, 6, true), ["walk"] = new(6, 10, true), ["run"] = new(6, 14, true) };
        for (int seed = 0; seed < 10; seed++)
        {
            var sim = new PetSim(anims, new ZoeConfig { Energy = 2 }, new Random(seed)) { HalfWidth = 60 };
            var ps = PlatformBuilder.Build(new World(areas, []), 100, 40);
            sim.Spawn(900, 900);
            for (int i = 0; i < 60 * 120; i++)
            {
                sim.Tick(1 / 60.0, ps, areas);
                Assert.InRange(sim.X, 60 - 0.01, 1920 - 60 + 0.01);
            }
        }
    }
    [Fact]
    public void WalksAcrossBothMonitors()
    {
        // her owner's layout: two 2560-wide screens side by side, taskbar on each (same work-area bottom).
        var areas = new[] { new RectI(0, 0, 2560, 1392), new RectI(2560, 0, 5120, 1392) };
        var anims = new Dictionary<string, AnimInfo> { ["sit"] = new(4, 6, true), ["walk"] = new(6, 10, true), ["run"] = new(6, 14, true) };
        bool left = false, right = false;
        for (int seed = 0; seed < 10 && !(left && right); seed++)
        {
            var sim = new PetSim(anims, new ZoeConfig { Energy = 2 }, new Random(seed)) { HalfWidth = 60 };
            var ps = PlatformBuilder.Build(new World(areas, []), 100, 40);
            sim.Spawn(1300, 1300);
            for (int i = 0; i < 60 * 600; i++)
            {
                sim.Tick(1 / 60.0, ps, areas);
                if (sim.X < 2000) left = true;
                if (sim.X > 3200) right = true;
            }
        }
        Assert.True(left && right, "never crossed between the two screens");
    }
}

public class UrlRuleTests
{
    static readonly Rule Acme = new() { Name = "acme", Title = ["Acme"], Url = ["acme"], Mood = Mood.Nap };

    [Fact]
    public void MatchesOnAddressWhenTitleDoesNotNameTheApp() =>
        Assert.True(RuleEngine.Matches(Acme, new WindowInfo(1, default, "Appointment Book - Google Chrome", "chrome", "app.acme.example/book")));

    [Fact]
    public void StillMatchesOnTitleWithoutAnAddress() =>
        Assert.True(RuleEngine.Matches(Acme, new WindowInfo(1, default, "Acme - Chrome", "chrome")));

    [Fact]
    public void OtherSitesDoNotMatch() =>
        Assert.False(RuleEngine.Matches(Acme, new WindowInfo(1, default, "News - Chrome", "chrome", "news.example.com")));

    [Fact]
    public void UrlOnlyRuleNeedsAnAddress() =>
        Assert.False(RuleEngine.Matches(new Rule { Url = ["acme"] }, new WindowInfo(1, default, "Acme", "chrome")));
}

public class PlayTests
{
    static readonly RectI[] Areas = [new RectI(0, 0, 2560, 1392), new RectI(2560, 0, 5120, 1392)];
    static readonly Dictionary<string, AnimInfo> All = new()
    {
        ["sit"] = new(4, 6, true), ["walk"] = new(6, 10, true), ["run"] = new(6, 14, true), ["sniff"] = new(8, 16, false),
        ["sitdown"] = new(6, 16, false), ["standup"] = new(6, 16, false), ["turn"] = new(8, 16, false),
        ["happy"] = new(4, 8, true), ["yawn"] = new(8, 16, false), ["stretch"] = new(8, 16, false),
        ["liedown"] = new(8, 16, false), ["sleep"] = new(4, 4, true), ["getup"] = new(8, 16, false),
        ["jumpprep"] = new(6, 24, false), ["jump"] = new(4, 16, false), ["fall"] = new(4, 16, true), ["dangle"] = new(4, 8, true),
        ["wag"] = new(8, 16, true), ["greet"] = new(40, 16, false), ["playbow"] = new(20, 16, false),
        ["carry"] = new(8, 16, true), ["pickup"] = new(10, 16, false), ["drop"] = new(10, 16, false),
        ["bark"] = new(8, 16, true), ["paw"] = new(16, 16, false), ["sit~look"] = new(8, 16, true),
        ["restdown"] = new(20, 16, false), ["rest"] = new(40, 16, true), ["restup"] = new(20, 16, false),
        ["scratch"] = new(30, 16, false), ["dig"] = new(20, 16, false), ["spin"] = new(30, 16, false),
        ["bellydown"] = new(20, 16, false), ["belly"] = new(8, 16, true), ["bellyup"] = new(20, 16, false),
    };

    static (PetSim, List<Platform>) Make(int seed, double energy = 1, params WindowInfo[] ws)
    {
        var sim = new PetSim(All, new ZoeConfig { Energy = energy }, new Random(seed)) { HalfWidth = 60 };
        var ps = PlatformBuilder.Build(new World(Areas, ws), 100, 40);
        sim.Spawn(1300, 1300);
        for (int i = 0; i < 120; i++) sim.Tick(1 / 60.0, ps, Areas);
        return (sim, ps);
    }

    [Fact]
    public void ChasesAThrownBallAndEventuallyCarriesIt()
    {
        bool chased = false, carried = false, dropped = false;
        for (int seed = 0; seed < 5 && !(carried && dropped); seed++)
        {
            var (sim, ps) = Make(seed);
            sim.ThrowBall(2200, 900, 300, -200);
            for (int i = 0; i < 60 * 90; i++)
            {
                sim.Tick(1 / 60.0, ps, Areas);
                if (sim.State == PetState.Run && sim.BallActive) chased = true;
                if (sim.State == PetState.Carry) { carried = true; Assert.True(sim.BallHeld); Assert.False(sim.BallVisible); }
                if (carried && sim.BallActive && !sim.BallHeld && sim.State != PetState.Drop) dropped = true;
                if (sim.BallActive) Assert.InRange(sim.BallX, 0, 5120);
                Assert.InRange(sim.X, -200, 5320); // may trot off-screen to fetch
            }
        }
        Assert.True(chased && carried && dropped, $"chased={chased} carried={carried} dropped={dropped}");
    }

    [Fact]
    public void BallComesToRestOnTheFloorAndGoesAwayWhenIgnored()
    {
        var (sim, ps) = Make(1);
        sim.Mood = Mood.Normal;
        sim.ThrowBall(4000, 500, 200, 0);
        sim.Mood = Mood.Nap; // work time: no ball
        Assert.False(sim.BallActive);
    }

    [Fact]
    public void ZoomiesRunBackAndForthThenStop()
    {
        bool zoomed = false;
        for (int seed = 0; seed < 20 && !zoomed; seed++)
        {
            var (sim, ps) = Make(seed, energy: 2);
            int flips = 0; bool last = sim.FacingRight;
            for (int i = 0; i < 60 * 120; i++)
            {
                sim.Tick(1 / 60.0, ps, Areas);
                if (sim.Zooming && sim.State == PetState.Run)
                {
                    if (sim.FacingRight != last) flips++;
                    Assert.NotEqual("turn", sim.Anim);
                }
                last = sim.FacingRight;
            }
            zoomed = flips >= 2;
        }
        Assert.True(zoomed);
    }

    [Fact]
    public void GreetsAtThePointerWhenHerOwnerComesBack()
    {
        var (sim, ps) = Make(2);
        sim.Mood = Mood.Doze;
        for (int i = 0; i < 60 * 10; i++) sim.Tick(1 / 60.0, ps, Areas);
        Assert.Equal(PetState.Sleep, sim.State);
        sim.CursorX = 3000;
        sim.Greet();
        sim.Mood = Mood.Normal;
        bool greeted = false; double where = 0;
        for (int i = 0; i < 60 * 40 && !greeted; i++)
        {
            sim.Tick(1 / 60.0, ps, Areas);
            if (sim.State == PetState.Greet) { greeted = true; where = sim.X; }
        }
        Assert.True(greeted);
        Assert.InRange(where, 2990, 3010);
    }

    [Fact]
    public void DoubleClickMakesHerStandUp()
    {
        var (sim, ps) = Make(3);
        sim.Poke();
        for (int i = 0; i < 20; i++) sim.Tick(1 / 60.0, ps, Areas);
        sim.Poke();
        bool greeted = false;
        for (int i = 0; i < 120 && !greeted; i++) { sim.Tick(1 / 60.0, ps, Areas); greeted = sim.State == PetState.Greet; }
        Assert.True(greeted);
    }

    [Fact]
    public void FetchesHerBallFromOffScreenThenStaresAndPaws()
    {
        bool left = false, back = false, pawed = false, stared = false;
        for (int seed = 0; seed < 40 && !(back && pawed); seed++)
        {
            var (sim, ps) = Make(seed, energy: 2);
            bool gone = false, returned = false;
            for (int i = 0; i < 60 * 240; i++)
            {
                sim.Tick(1 / 60.0, ps, Areas);
                if (sim.X < 0 || sim.X > 5120) { gone = left = true; Assert.False(sim.BallVisible); }
                if (gone && sim.BallVisible && sim.X is > 60 and < 5060)
                {
                    returned = back = true;
                    Assert.InRange(sim.BallX, 0, 5120);
                }
                if (returned && sim.Anim == "sit~look") stared = true;
                if (returned && sim.State == PetState.Paw) { pawed = true; Assert.True(sim.BallVisible); }
            }
        }
        Assert.True(left && back && stared && pawed, $"left={left} back={back} stared={stared} pawed={pawed}");
    }

    [Fact]
    public void BellyRubLastsWhileSheIsBeingPetted()
    {
        var (sim, ps) = Make(4);
        for (int i = 0; i < 3600 && sim.State is not (PetState.Sit or PetState.Walk or PetState.Wag); i++) sim.Tick(1 / 60.0, ps, Areas);
        sim.Pet();
        bool belly = false;
        for (int i = 0; i < 60 * 12; i++)
        {
            sim.Pet(); // still rubbing
            sim.Tick(1 / 60.0, ps, Areas);
            if (sim.State == PetState.Belly) belly = true;
        }
        Assert.True(belly);
        Assert.Equal(PetState.Belly, sim.State);
        bool up = false;
        for (int i = 0; i < 60 * 12 && !up; i++) { sim.Tick(1 / 60.0, ps, Areas); up = sim.State == PetState.Wag; }
        Assert.True(up, "should roll back onto her feet once the rubbing stops");
    }

    [Fact]
    public void BarksTowardsTheHorse()
    {
        var (sim, ps) = Make(4);
        for (int i = 0; i < 3600 && sim.State is not (PetState.Sit or PetState.Walk or PetState.Wag); i++) sim.Tick(1 / 60.0, ps, Areas);
        sim.BarkAt(sim.X - 800);
        bool barked = false;
        for (int i = 0; i < 240 && !barked; i++) { sim.Tick(1 / 60.0, ps, Areas); barked = sim.State == PetState.Bark; }
        Assert.True(barked);
        Assert.False(sim.FacingRight);
        sim.BarkAt(sim.X + 800); // too soon: no machine-gun barking
        Assert.False(sim.FacingRight);
    }

    [Fact]
    public void FollowsThePointerThenPouncesWhenItStops()
    {
        bool followed = false, pounced = false;
        for (int seed = 0; seed < 30 && !pounced; seed++)
        {
            var (sim, ps) = Make(seed);
            double cursor = 3400;
            for (int i = 0; i < 60 * 120 && !pounced; i++)
            {
                sim.CursorX = cursor;
                sim.Tick(1 / 60.0, ps, Areas);
                if (sim.State == PetState.Sit && Math.Abs(sim.X - cursor) < 200 && !followed) { followed = true; cursor = sim.X + 150; }
                if (followed && sim.State == PetState.Air && sim.Vx > 100) pounced = true;
            }
            if (!pounced) followed = false;
        }
        Assert.True(followed && pounced, $"followed={followed} pounced={pounced}");
    }

    [Fact]
    public void AsksForAttentionWhenIgnored()
    {
        bool pawed = false;
        for (int seed = 0; seed < 5 && !pawed; seed++)
        {
            var (sim, ps) = Make(seed);
            for (int i = 0; i < 60 * 900 && !pawed; i++)
            {
                sim.CursorX = 1000 + (i / 30 % 2) * 10; // her owner's working: pointer keeps moving, nobody touches Zoe
                sim.Tick(1 / 60.0, ps, Areas);
                if (i > 60 * 300 && sim.State == PetState.Paw && !sim.BallActive) { pawed = true; Assert.InRange(sim.X, 850, 1150); }
            }
        }
        Assert.True(pawed);
    }

    [Fact]
    public void DoesHerLittleIdles()
    {
        var seen = new HashSet<PetState>();
        for (int seed = 0; seed < 6; seed++)
        {
            var (sim, ps) = Make(seed);
            for (int i = 0; i < 60 * 600; i++) { sim.Tick(1 / 60.0, ps, Areas); seen.Add(sim.State); }
        }
        Assert.Contains(PetState.Scratch, seen);
        Assert.Contains(PetState.Dig, seen);
        Assert.Contains(PetState.Spin, seen);
        Assert.Contains(PetState.Wag, seen);
    }

    [Fact]
    public void SplootsProperlyAndGetsUpForWork()
    {
        bool rested = false;
        for (int seed = 0; seed < 10 && !rested; seed++)
        {
            var (sim, ps) = Make(seed);
            string prev = "";
            for (int i = 0; i < 60 * 200; i++)
            {
                sim.Tick(1 / 60.0, ps, Areas);
                // only ever reached by lying down into it, and left by getting up out of it
                if (sim.Anim == "rest" && prev != "rest") Assert.Equal("restdown", prev);
                if (prev == "rest" && sim.Anim != "rest") Assert.Equal("restup", sim.Anim);
                prev = sim.Anim;
                if (sim.State == PetState.Rest && !rested)
                {
                    rested = true;
                    sim.Mood = Mood.Nap; // Acme comes up mid-sploot
                }
            }
            if (rested) Assert.Equal(PetState.Sleep, sim.State);
        }
        Assert.True(rested);
    }

    [Fact]
    public void LongPlaySessionStaysSane()
    {
        var win = new WindowInfo(7, new RectI(600, 700, 1800, 1300));
        for (int seed = 0; seed < 8; seed++)
        {
            var (sim, ps) = Make(seed, 1.5, win);
            var rng = new Random(seed);
            for (int i = 0; i < 60 * 600; i++)
            {
                if (i % 1800 == 900) sim.ThrowBall(rng.Next(100, 5000), rng.Next(200, 1200), rng.Next(-900, 900), rng.Next(-900, 200));
                if (i % 2500 == 0) { sim.CursorX = rng.Next(0, 5120); sim.Poke(); }
                if (i % 7000 == 3000) sim.Greet();
                if (i == 20000) sim.Mood = Mood.Nap;
                if (i == 24000) sim.Mood = Mood.Normal;
                if (i % 4100 == 2000) sim.BarkAt(rng.Next(0, 5120));
                if (i % 5300 >= 5200) sim.Pet();
                sim.Tick(1 / 60.0, ps, Areas);
                Assert.InRange(sim.X, -200, 5320);
                if (sim.State is not (PetState.Run or PetState.Walk or PetState.Carry or PetState.Sit or PetState.Turn or PetState.Air or PetState.Transition))
                    Assert.InRange(sim.X, 59.9, 5120 - 59.9); // only ever off-screen mid-fetch
                Assert.False(double.IsNaN(sim.X) || double.IsNaN(sim.BallX));
                Assert.InRange(sim.Frame, 0, All[sim.Anim].Frames - 1);
            }
        }
    }
}

public class JumpTests
{
    [Fact]
    public void CrouchesOnTheGroundThenLeapsOntoAWindow()
    {
        var areas = new[] { new RectI(0, 0, 1920, 1040) };
        var anims = new Dictionary<string, AnimInfo>
        {
            ["sit"] = new(4, 6, true), ["walk"] = new(6, 10, true), ["jumpprep"] = new(14, 24, false),
            ["jump"] = new(5, 16, false), ["fall"] = new(4, 16, true),
        };
        var ps = PlatformBuilder.Build(new World(areas, [new WindowInfo(9, new RectI(700, 800, 1300, 1000))]), 100, 40);
        bool sawCrouchOnGround = false, sawJumpRising = false, landedOnWindow = false;
        for (int seed = 0; seed < 30 && !landedOnWindow; seed++)
        {
            var sim = new PetSim(anims, new ZoeConfig { Energy = 3 }, new Random(seed));
            sim.Spawn(1000, 900);
            for (int i = 0; i < 60 * 60; i++)
            {
                sim.Tick(1 / 60.0, ps, areas);
                if (sim.State == PetState.Crouch) { sawCrouchOnGround = true; Assert.True(sim.Grounded); Assert.Equal("jumpprep", sim.Anim); }
                if (sim.State == PetState.Air && sim.Vy < 0 && sim.Anim == "jump") sawJumpRising = true;
                if (sim.Support?.WindowId == 9) { landedOnWindow = true; break; }
            }
        }
        Assert.True(sawCrouchOnGround);
        Assert.True(sawJumpRising);
        Assert.True(landedOnWindow);
    }
}

public class NapTests
{
    [Fact]
    public void CurlsUpThenRollsOntoBackAndRollsBackBeforeGettingUp()
    {
        var areas = new[] { new RectI(0, 0, 1920, 1040) };
        var anims = new Dictionary<string, AnimInfo>
        {
            ["sit"] = new(4, 6, true), ["liedown"] = new(8, 16, false), ["sleep"] = new(8, 8, true),
            ["sleep~belly"] = new(8, 8, true), ["rollover"] = new(8, 16, false), ["rollback"] = new(8, 16, false),
            ["getup"] = new(8, 16, false),
        };
        var ps = PlatformBuilder.Build(new World(areas, []), 100, 40);
        var sim = new PetSim(anims, new ZoeConfig(), new Random(1));
        sim.Spawn(900, 900);
        for (int i = 0; i < 60; i++) sim.Tick(1 / 60.0, ps, areas);
        sim.Mood = Mood.Doze;
        var seen = new List<string>();
        for (int i = 0; i < 60 * 120; i++) { sim.Tick(1 / 60.0, ps, areas); if (seen.Count == 0 || seen[^1] != sim.Anim) seen.Add(sim.Anim); }
        Assert.Equal(new[] { "liedown", "sleep", "rollover", "sleep~belly" }, seen.SkipWhile(a => a != "liedown").Take(4));
        sim.Mood = Mood.Normal;
        Assert.Equal("rollback", sim.Anim);
        for (int i = 0; i < 60 * 2; i++) { sim.Tick(1 / 60.0, ps, areas); if (seen[^1] != sim.Anim) seen.Add(sim.Anim); }
        Assert.Contains("getup", seen.SkipWhile(a => a != "rollback"));
    }
}

public class SleepCycleTests
{
    static readonly RectI[] Areas = { new(0, 0, 2560, 1400) };
    static readonly Dictionary<string, AnimInfo> Anims = new()
    {
        ["sit"] = new(4, 6, true), ["walk"] = new(6, 10, true), ["run"] = new(6, 14, true), ["yawn"] = new(8, 8, false),
        ["liedown"] = new(4, 8, false), ["sleep"] = new(4, 3, true), ["getup"] = new(4, 8, false), ["fall"] = new(2, 8, true),
    };

    static (PetSim, List<Platform>) Make(int seed, ZoeConfig? cfg = null)
    {
        var sim = new PetSim(Anims, cfg ?? new ZoeConfig(), new Random(seed)) { HalfWidth = 60 };
        var ps = PlatformBuilder.Build(new World(Areas, []), 100, 40);
        sim.Spawn(1300, 1300);
        for (int i = 0; i < 120; i++) sim.Tick(1 / 60.0, ps, Areas);
        return (sim, ps);
    }

    /// <summary>Fraction of the next <paramref name="seconds"/> spent asleep.</summary>
    static double AsleepShare(PetSim sim, List<Platform> ps, double seconds)
    {
        int asleep = 0, n = (int)(seconds * 60);
        for (int i = 0; i < n; i++) { sim.Tick(1 / 60.0, ps, Areas); if (sim.State == PetState.Sleep) asleep++; }
        return (double)asleep / n;
    }

    [Fact]
    public void ChasingABallTiresHerAndSleepRestoresHer()
    {
        var (sim, ps) = Make(1);
        double before = sim.Tiredness;
        sim.ThrowBall(2300, 1000, 500, -300);
        for (int i = 0; i < 60 * 8; i++) sim.Tick(1 / 60.0, ps, Areas);
        Assert.True(sim.Tiredness > before + 0.02, $"tiredness {before} -> {sim.Tiredness}");

        sim.Tiredness = 0.9;
        sim.Mood = Mood.Doze;
        for (int i = 0; i < 60 * 60; i++) sim.Tick(1 / 60.0, ps, Areas);
        Assert.Equal(PetState.Sleep, sim.State);
        Assert.True(sim.Tiredness < 0.6, $"tiredness after a minute asleep {sim.Tiredness}");
    }

    [Fact]
    public void WornOutZoeYawnsGoesToBedAndSleepsUntilRested()
    {
        var (sim, ps) = Make(2);
        sim.Tiredness = 0.95;
        var seen = new List<PetState>();
        double slept = 0;
        for (int i = 0; i < 60 * 400; i++)
        {
            sim.Tick(1 / 60.0, ps, Areas);
            if (seen.Count == 0 || seen[^1] != sim.State) seen.Add(sim.State);
            if (sim.State == PetState.Sleep) slept += 1 / 60.0;
            if (seen.Contains(PetState.Sleep) && sim.State == PetState.GetUp) break;
        }
        int yawn = seen.IndexOf(PetState.Yawn), bed = seen.IndexOf(PetState.LieDown);
        Assert.True(yawn >= 0 && bed > yawn, string.Join(",", seen));
        Assert.True(slept > 100, $"slept only {slept:0}s");
        Assert.True(sim.Tiredness < 0.3, $"woke still tired: {sim.Tiredness}");
    }

    [Fact]
    public void SleepsMoreAtNightThanInTheMorning()
    {
        double day = 0, night = 0;
        for (int seed = 0; seed < 4; seed++)
        {
            var (a, pa) = Make(seed); a.Hour = 10; day += AsleepShare(a, pa, 1800);
            var (b, pb) = Make(seed); b.Hour = 23; night += AsleepShare(b, pb, 1800);
        }
        Assert.True(night > day * 1.3, $"day {day / 4:0.00}, night {night / 4:0.00}");
    }

    [Fact]
    public void CanBeSwitchedOff()
    {
        var (sim, ps) = Make(3, new ZoeConfig { SleepCycle = false });
        sim.ThrowBall(2300, 1000, 500, -300);
        for (int i = 0; i < 60 * 30; i++) sim.Tick(1 / 60.0, ps, Areas);
        Assert.Equal(0, sim.Tiredness);
    }

    [Fact]
    public void CirclesBeforeLyingDownAndGivesUpIfWorkIsOver()
    {
        var anims = new Dictionary<string, AnimInfo>(Anims) { ["turn"] = new(10, 20, false), ["turnback"] = new(10, 20, false) };
        var ps = PlatformBuilder.Build(new World(Areas, []), 100, 40);
        var sim = new PetSim(anims, new ZoeConfig(), new Random(4)) { HalfWidth = 60 };
        sim.Spawn(1300, 1300);
        for (int i = 0; i < 120; i++) sim.Tick(1 / 60.0, ps, Areas);
        sim.Mood = Mood.Doze;
        var seen = new List<string>();
        double x0 = sim.X, far = 0;
        for (int i = 0; i < 60 * 12; i++)
        {
            sim.Tick(1 / 60.0, ps, Areas); far = Math.Max(far, Math.Abs(sim.X - x0));
            if (seen.Count == 0 || seen[^1] != sim.Anim) seen.Add(sim.Anim);
        }
        Assert.Equal(new[] { "walk", "turnback", "walk", "turn", "walk", "turnback", "walk", "turn", "liedown", "sleep" },
            seen.SkipWhile(a => a != "walk").Take(10));
        Assert.True(far > 20, $"she only moved {far:0}px while circling");
        Assert.True(Math.Abs(sim.X - x0) < 8, "she should end up where she started");

        sim.Mood = Mood.Normal;
        for (int i = 0; i < 60 * 3; i++) sim.Tick(1 / 60.0, ps, Areas);
        sim.Mood = Mood.Doze;
        for (int i = 0; i < 30 && sim.State != PetState.Circle; i++) sim.Tick(1 / 60.0, ps, Areas);
        Assert.Equal(PetState.Circle, sim.State);
        sim.Mood = Mood.Normal;                       // back at the keyboard mid-circle
        Assert.NotEqual(PetState.Circle, sim.State);
        for (int i = 0; i < 60; i++) { sim.Tick(1 / 60.0, ps, Areas); Assert.NotEqual(PetState.LieDown, sim.State); }
    }
}
