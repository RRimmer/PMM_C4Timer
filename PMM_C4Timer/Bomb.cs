using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace PMM_C4Timer;

// What the timer shows. Comes from the planted bomb or from the !c4 preview.
internal readonly record struct BombView(
    float Left,          // seconds to the explosion
    float Total,         // full timer (mp_c4timer)
    int Site,            // 0 = A, 1 = B
    bool Defusing,
    bool Kit,            // the defuser has a kit (5 s defuse)
    float DefuseProgress,// 0..1
    bool DefuseInTime);  // the defuse ends before the explosion

public partial class Plugin
{
    private CPlantedC4? _bomb;
    private BombView? _frozen;      // last picture after a defuse / explosion (HideDelay)
    private float _frozenUntil;

    private void BombFind()
    {
        try
        {
            _bomb = Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4")
                .FirstOrDefault(b => b != null && b.IsValid && b.BombTicking && !b.HasExploded && !b.BombDefused);
        }
        catch
        {
            _bomb = null;
        }
        _frozen = null;
        Dbg("bomb found: {Found}", _bomb != null);
        if (_bomb != null && Config.Enabled) EnsureHud();
    }

    private void BombClear()
    {
        _bomb = null;
        _frozen = null;
    }

    // Defused or exploded: hide now, or keep the last picture for HideDelay seconds.
    private void BombFinish()
    {
        var last = ReadBomb();
        _bomb = null;
        if (Config.HideDelay > 0f && last != null)
        {
            _frozen = last.Value with { Defusing = false };
            _frozenUntil = Server.CurrentTime + Config.HideDelay;
        }
        else
        {
            _frozen = null;
        }
    }

    private BombView? BombCurrent()
    {
        if (_bomb != null)
        {
            var v = ReadBomb();
            if (v != null) return v;
            BombFinish();
        }
        if (_frozen != null)
        {
            if (Server.CurrentTime < _frozenUntil) return _frozen;
            _frozen = null;
        }
        return null;
    }

    private BombView? ReadBomb()
    {
        var b = _bomb;
        if (b == null || !b.IsValid || b.Handle == IntPtr.Zero) return null;
        if (b.HasExploded || b.BombDefused || !b.BombTicking) return null;

        float now = Server.CurrentTime;
        float total = b.TimerLength;
        if (total <= 0.1f) total = C4TimerCvar();
        float left = Math.Clamp(b.C4Blow - now, 0f, total);

        bool defusing = b.BeingDefused;
        float progress = 0f;
        bool kit = false, inTime = false;
        if (defusing)
        {
            float len = b.DefuseLength;
            if (len <= 0.1f) len = 10f;
            kit = len < 7.5f;
            float defLeft = b.DefuseCountDown - now;
            progress = Math.Clamp(1f - defLeft / len, 0f, 1f);
            inTime = b.DefuseCountDown <= b.C4Blow;
        }
        return new BombView(left, total, b.BombSite, defusing, kit, progress, inTime);
    }

    private static float C4TimerCvar()
    {
        try
        {
            var cv = ConVar.Find("mp_c4timer");
            if (cv != null)
            {
                int v = cv.GetPrimitiveValue<int>();
                if (v > 0) return v;
            }
        }
        catch { }
        return 40f;
    }

    internal static string SiteLetter(int site) => site switch
    {
        1 => "B",
        _ => "A"
    };
}
