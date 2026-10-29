using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Extensions;
using Microsoft.Extensions.Logging;

namespace PMM_C4Timer;

// The custom_hud_layout entity and everything sent to it. Every write goes through a cache, so a
// tick sends only what changed. A new entity (round restart, map change) starts with empty caches.
public partial class Plugin
{
    internal const string RootId = "c4-root";
    internal const string PrevId = "c4-prev";

    private CCSCustomHudLayout? _hud;

    private readonly Dictionary<string, bool> _gClass = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _gVar = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool>?[] _pClass = new Dictionary<string, bool>?[65];
    private readonly Dictionary<string, string>?[] _pVar = new Dictionary<string, string>?[65];

    // Current member of each class ladder ("p" -> "p57"), per target.
    private readonly Dictionary<string, string> _gLadder = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string>?[] _pLadder = new Dictionary<string, string>?[65];
    // Per player ladders on #c4-root (the defuse progress "d" of players allowed to see it).
    private readonly Dictionary<string, string>?[] _pRootLadder = new Dictionary<string, string>?[65];

    private bool HudAlive => _hud != null && _hud.IsValid;

    private void HudReset()
    {
        _hud = null;
        ForgetAllSent();
    }

    private void HudRemove()
    {
        try
        {
            var hud = _hud;
            if (hud != null && hud.IsValid)
            {
                hud.SetHasClass(RootId, "c4-on", false);
                hud.SetHasClass(PrevId, "c4-on", false);
                hud.Remove();
            }
        }
        catch { }
        HudReset();
    }

    private void HudForgetSlot(int slot)
    {
        if (slot < 0 || slot >= _pClass.Length) return;
        _pClass[slot] = null;
        _pVar[slot] = null;
        _pLadder[slot] = null;
        _pRootLadder[slot] = null;
    }

    private void ForgetAllSent()
    {
        _gClass.Clear();
        _gVar.Clear();
        _gLadder.Clear();
        Array.Clear(_pClass);
        Array.Clear(_pVar);
        Array.Clear(_pLadder);
        Array.Clear(_pRootLadder);
    }

    private bool EnsureHud()
    {
        if (HudAlive) return true;
        _hud = null;
        CCSCustomHudLayout? created;
        try
        {
            created = Utilities.CreateEntityByName<CCSCustomHudLayout>("custom_hud_layout");
        }
        catch (Exception e)
        {
            Logger.LogError("custom_hud_layout was not created: {Error}", e.Message);
            return false;
        }
        if (created == null || created.Handle == IntPtr.Zero)
        {
            Logger.LogError("custom_hud_layout was not created");
            return false;
        }
        created.StrLayout = Config.LayoutPath;
        created.DispatchSpawn();
        if (!created.IsValid) return false;
        _hud = created;
        ForgetAllSent();
        PaintDefaults();
        Dbg("hud spawned: {Layout}", Config.LayoutPath);
        return true;
    }

    // Global fallbacks: what a viewer without per-player state (GOTV) sees.
    private void PaintDefaults()
    {
        string style = "st-" + Config.DefaultStyle;
        string pos = "pos-" + Config.DefaultPosition;
        foreach (int s in AllStyles) GClass(RootId, "st-" + s, "st-" + s == style);
        foreach (var p in AllPositions) GClass(RootId, "pos-" + p, "pos-" + p == pos);
        foreach (var (name, key) in TextVars)
            GVar(RootId, name, T(null, key));
    }

    // Translated labels of the layout: dialog variable -> lang key.
    internal static readonly (string Var, string Key)[] TextVars =
    {
        ("c4_l_bomb", "hud.bomb"),
        ("c4_l_kit", "hud.kit"),
        ("c4_l_nokit", "hud.nokit"),
        ("c4_l_sec", "hud.sec"),
        ("c4_l_def", "hud.defusing"),
    };

    // ---------------------------------------------------------------- painting

    // Where a picture goes: the global state of #c4-root, or one player's state of #c4-prev.
    private abstract class Sink
    {
        public abstract void Class(string cls, bool on);
        public abstract void Var(string name, string value);
        public abstract Dictionary<string, string>? Ladders { get; }

        // Exactly one member of a ladder is on.
        public void Ladder(string prefix, int value)
        {
            var map = Ladders;
            if (map == null) return;
            string cls = prefix + value.ToString(CultureInfo.InvariantCulture);
            map.TryGetValue(prefix, out var prev);
            if (prev == cls) return;
            if (prev != null) Class(prev, false);
            Class(cls, true);
            map[prefix] = cls;
        }
    }

    private sealed class GlobalTarget : Sink
    {
        private readonly Plugin _p;
        public GlobalTarget(Plugin p) => _p = p;
        public override void Class(string cls, bool on) => _p.GClass(RootId, cls, on);
        public override void Var(string name, string value) => _p.GVar(RootId, name, value);
        public override Dictionary<string, string>? Ladders => _p._gLadder;
    }

    private sealed class PlayerTarget : Sink
    {
        private readonly Plugin _p;
        private readonly CCSPlayerController _player;
        public PlayerTarget(Plugin p, CCSPlayerController player)
        {
            _p = p;
            _player = player;
        }
        public override void Class(string cls, bool on) => _p.PClass(_player, PrevId, cls, on);
        public override void Var(string name, string value) => _p.PVar(_player, PrevId, name, value);
        public override Dictionary<string, string>? Ladders
        {
            get
            {
                int slot = _player.Slot;
                if (slot < 0 || slot >= _p._pLadder.Length) return null;
                return _p._pLadder[slot] ??= new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }
    }

    // One player's own defuse state on #c4-root, over the global "nobody defuses".
    private sealed class RootPlayerTarget : Sink
    {
        private readonly Plugin _p;
        private readonly CCSPlayerController _player;
        public RootPlayerTarget(Plugin p, CCSPlayerController player)
        {
            _p = p;
            _player = player;
        }
        public override void Class(string cls, bool on) => _p.PClass(_player, RootId, cls, on);
        public override void Var(string name, string value) => _p.PVar(_player, RootId, name, value);
        public override Dictionary<string, string>? Ladders
        {
            get
            {
                int slot = _player.Slot;
                if (slot < 0 || slot >= _p._pRootLadder.Length) return null;
                if (_p._pRootLadder[slot] == null)
                {
                    // A player who had this slot before may have left "d" classes (per player state
                    // survives a reconnect). Clear them once.
                    for (int k = 0; k <= 100; k++) _p.PClass(_player, RootId, "d" + k.ToString(Inv), false);
                    _p._pRootLadder[slot] = new Dictionary<string, string>(StringComparer.Ordinal);
                }
                return _p._pRootLadder[slot];
            }
        }
    }

    private Sink? _globalSink;
    private Sink GlobalSink => _globalSink ??= new GlobalTarget(this);

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Timer and defuse together (the !c4 preview).
    private void Paint(Sink s, BombView v)
    {
        PaintTimer(s, v);
        PaintDefuse(s, v);
    }

    // Everything but the defuse: what every viewer of the real timer gets.
    private void PaintTimer(Sink s, BombView v)
    {
        float left = Math.Max(0f, v.Left);
        float total = Math.Max(0.1f, v.Total);
        int secs = (int)Math.Ceiling(left - 0.001f);

        s.Var("c4_t", left.ToString("0.0", Inv));
        s.Var("c4_s", secs.ToString(Inv));
        s.Var("c4_m", $"{secs / 60}:{secs % 60:00}");
        s.Var("c4_site", SiteLetter(v.Site));

        s.Ladder("p", Math.Clamp((int)Math.Ceiling(left / total * 100f - 0.001f), 0, 100));
        s.Ladder("mk5-", Math.Clamp((int)Math.Round(5f / total * 100f), 0, 100));
        s.Ladder("mk10-", Math.Clamp((int)Math.Round(10f / total * 100f), 0, 100));

        string phase = left >= Config.WarnSeconds ? "ph-ok" : left >= Config.CritSeconds ? "ph-warn" : "ph-crit";
        s.Class("ph-ok", phase == "ph-ok");
        s.Class("ph-warn", phase == "ph-warn");
        s.Class("ph-crit", phase == "ph-crit");
        s.Class("late5", left < 5f);
        s.Class("late10", left < 10f);
    }

    // The defuse state; null = "nobody defuses" (also what a player not allowed to see it gets).
    private void PaintDefuse(Sink s, BombView? view)
    {
        var v = view ?? default;
        s.Class("defusing", v.Defusing);
        s.Class("kit", v.Defusing && v.Kit);
        s.Class("def-ok", v.Defusing && v.DefuseInTime);
        s.Class("def-late", v.Defusing && !v.DefuseInTime);
        int dp = v.Defusing ? Math.Clamp((int)Math.Round(v.DefuseProgress * 100f), 0, 100) : 0;
        s.Ladder("d", dp);
        s.Var("c4_dp", dp.ToString(Inv) + "%");
    }

    // ---------------------------------------------------------------- send helpers

    private void GClass(string panel, string cls, bool on)
    {
        var l = _hud;
        if (l == null || !l.IsValid) return;
        string key = panel + "\u0001" + cls;
        if (_gClass.TryGetValue(key, out bool cur) && cur == on) return;
        _gClass[key] = on;
        l.SetHasClass(panel, cls, on);
    }

    private void GVar(string panel, string name, string value)
    {
        var l = _hud;
        if (l == null || !l.IsValid) return;
        string key = panel + "\u0001" + name;
        if (_gVar.TryGetValue(key, out var cur) && cur == value) return;
        _gVar[key] = value;
        l.SetDialogVariableString(panel, name, value);
    }

    private void PClass(CCSPlayerController player, string panel, string cls, bool on)
    {
        var l = _hud;
        int slot = player.Slot;
        if (l == null || !l.IsValid || slot < 0 || slot >= _pClass.Length || player.Handle == IntPtr.Zero) return;
        var sent = _pClass[slot] ??= new Dictionary<string, bool>(StringComparer.Ordinal);
        string key = panel + "\u0001" + cls;
        if (sent.TryGetValue(key, out bool cur) && cur == on) return;
        sent[key] = on;
        l.SetHasClassForPlayer(player, panel, cls, on);
    }

    private void PVar(CCSPlayerController player, string panel, string name, string value)
    {
        var l = _hud;
        int slot = player.Slot;
        if (l == null || !l.IsValid || slot < 0 || slot >= _pVar.Length || player.Handle == IntPtr.Zero) return;
        var sent = _pVar[slot] ??= new Dictionary<string, string>(StringComparer.Ordinal);
        string key = panel + "\u0001" + name;
        if (sent.TryGetValue(key, out var cur) && cur == value) return;
        sent[key] = value;
        l.SetDialogVariableStringForPlayer(player, panel, name, value);
    }
}
