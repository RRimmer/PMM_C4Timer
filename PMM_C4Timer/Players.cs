using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace PMM_C4Timer;

// Per player: on / off, style, position (saved by SteamID), who sees the timer, the !c4 preview,
// chat commands and translations.
public partial class Plugin
{
    private const string PrefsFile = "players.json";

    internal sealed class PlayerPref
    {
        public bool On { get; set; } = true;
        public int Style { get; set; }          // 0 = server default
        public string Pos { get; set; } = "";   // "" = server default
    }

    private readonly Dictionary<ulong, PlayerPref> _prefs = new();
    private readonly float[] _previewStart = new float[65];   // 0 = no preview

    private const float PreviewLength = 10f;

    private void PlayersLoad()
    {
        LoadPrefs();
    }

    private void PlayerForgetSlot(int slot)
    {
        if (slot >= 0 && slot < _previewStart.Length) _previewStart[slot] = 0f;
    }

    // ---------------------------------------------------------------- preferences

    internal PlayerPref Pref(CCSPlayerController p) =>
        p.SteamID != 0 && _prefs.TryGetValue(p.SteamID, out var pr) ? pr : new PlayerPref();

    private PlayerPref EditPref(CCSPlayerController p)
    {
        if (!_prefs.TryGetValue(p.SteamID, out var pr))
        {
            pr = new PlayerPref();
            if (p.SteamID != 0) _prefs[p.SteamID] = pr;
        }
        return pr;
    }

    internal int StyleOf(CCSPlayerController p)
    {
        if (!Config.AllowPlayerChoice) return Config.DefaultStyle;
        int s = Pref(p).Style;
        return s != 0 && Config.Styles.Contains(s) ? s : Config.DefaultStyle;
    }

    internal string PosOf(CCSPlayerController p)
    {
        if (!Config.AllowPlayerChoice) return Config.DefaultPosition;
        string pos = Pref(p).Pos;
        return pos.Length > 0 && Config.Positions.Contains(pos) ? pos : Config.DefaultPosition;
    }

    internal void SetOn(CCSPlayerController p, bool on)
    {
        EditPref(p).On = on;
        SavePrefs();
    }

    internal void SetStyle(CCSPlayerController p, int style)
    {
        EditPref(p).Style = style == Config.DefaultStyle ? 0 : style;
        SavePrefs();
        StartPreview(p);
    }

    internal void SetPos(CCSPlayerController p, string pos)
    {
        EditPref(p).Pos = pos == Config.DefaultPosition ? "" : pos;
        SavePrefs();
        StartPreview(p);
    }

    private void LoadPrefs()
    {
        _prefs.Clear();
        try
        {
            string path = Path.Combine(ModuleDirectory, PrefsFile);
            if (!File.Exists(path)) return;
            var raw = JsonSerializer.Deserialize<Dictionary<string, PlayerPref>>(File.ReadAllText(path));
            if (raw == null) return;
            foreach (var (id, pref) in raw)
                if (ulong.TryParse(id, out ulong sid) && pref != null) _prefs[sid] = pref;
        }
        catch (Exception e)
        {
            Logger.LogWarning("{File} was not read: {Error}", PrefsFile, e.Message);
        }
    }

    private void SavePrefs()
    {
        try
        {
            // Players on the defaults are not stored.
            var raw = _prefs.Where(x => !x.Value.On || x.Value.Style != 0 || x.Value.Pos.Length > 0)
                .ToDictionary(x => x.Key.ToString(), x => x.Value);
            File.WriteAllText(Path.Combine(ModuleDirectory, PrefsFile),
                JsonSerializer.Serialize(raw, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            Logger.LogWarning("{File} was not written: {Error}", PrefsFile, e.Message);
        }
    }

    // ---------------------------------------------------------------- who sees it

    internal bool Sees(CCSPlayerController p) => Pref(p).On && Allowed(p, Config.Show);

    // May this player see the defuse (ShowDefuse)?
    internal bool SeesDefuse(CCSPlayerController p) => Allowed(p, Config.ShowDefuse);

    private static bool Allowed(CCSPlayerController p, ShowOptions o)
    {
        var team = p.Team;
        if (team is CsTeam.Spectator or CsTeam.None) return o.Spectators;
        if (!p.PawnIsAlive) return o.Dead;
        return team == CsTeam.CounterTerrorist ? o.AliveCT : o.AliveT;
    }

    // ---------------------------------------------------------------- per player drawing

    private void RenderPlayer(CCSPlayerController p, BombView? bomb)
    {
        int slot = p.Slot;
        if (slot < 0 || slot >= _previewStart.Length) return;

        string style = "st-" + StyleOf(p);
        string pos = "pos-" + PosOf(p);
        var pv = PreviewView(p);
        bool preview = pv != null;

        // Translated labels on both roots.
        foreach (var (name, key) in TextVars)
        {
            string text = T(p, key);
            PVar(p, RootId, name, text);
            if (preview) PVar(p, PrevId, name, text);
        }

        // The real timer: style, position, mute.
        foreach (int s in AllStyles) PClass(p, RootId, "st-" + s, "st-" + s == style);
        foreach (var ps in AllPositions) PClass(p, RootId, "pos-" + ps, "pos-" + ps == pos);
        PClass(p, RootId, "c4-mute", preview || !Sees(p));

        // The defuse goes only to players allowed to see it; the global state never has it.
        if (bomb != null)
            PaintDefuse(new RootPlayerTarget(this, p), SeesDefuse(p) && bomb.Value.Defusing ? bomb : null);

        // The preview.
        if (preview)
        {
            foreach (int s in AllStyles) PClass(p, PrevId, "st-" + s, "st-" + s == style);
            foreach (var ps in AllPositions) PClass(p, PrevId, "pos-" + ps, "pos-" + ps == pos);
            Paint(new PlayerTarget(this, p), pv!.Value);
            PClass(p, PrevId, "c4-on", true);
        }
        else
        {
            PClass(p, PrevId, "c4-on", false);
        }
    }

    // ---------------------------------------------------------------- preview

    internal void StartPreview(CCSPlayerController p)
    {
        int slot = p.Slot;
        if (slot < 0 || slot >= _previewStart.Length) return;
        if (!Config.Enabled || !EnsureHud()) return;
        // First preview in this slot: a player who had the slot before may have left ladder
        // classes on #c4-prev (per player state survives a reconnect). Clear them once.
        if (_pLadder[slot] == null)
        {
            foreach (var prefix in new[] { "p", "d", "mk5-", "mk10-" })
                for (int k = 0; k <= 100; k++) PClass(p, PrevId, prefix + k, false);
            _pLadder[slot] = new Dictionary<string, string>(StringComparer.Ordinal);
        }
        _previewStart[slot] = Server.CurrentTime;
        RenderPlayer(p, null);
    }

    private bool AnyPreview()
    {
        float now = Server.CurrentTime;
        bool any = false;
        for (int i = 0; i < _previewStart.Length; i++)
        {
            if (_previewStart[i] <= 0f) continue;
            // One more tick after the end, so c4-on is switched off.
            if (now - _previewStart[i] > PreviewLength + 1f) _previewStart[i] = 0f;
            else any = true;
        }
        return any;
    }

    // A made-up bomb on site A: 14 s left of 40, a kit defuse from 11 s that ends at 6 s,
    // then the picture stays for a moment. Shows the amber and the red phase and the defuse.
    private BombView? PreviewView(CCSPlayerController p)
    {
        int slot = p.Slot;
        if (slot < 0 || slot >= _previewStart.Length || _previewStart[slot] <= 0f) return null;
        float t = Server.CurrentTime - _previewStart[slot];
        if (t < 0f || t > PreviewLength) return null;

        const float total = 40f, start = 14f, defFrom = 3f, defLen = 5f;
        float run = Math.Min(t, defFrom + defLen);          // the timer stops when the defuse ends
        float left = start - run;
        bool defusing = t >= defFrom && t < defFrom + defLen;
        float progress = defusing ? (t - defFrom) / defLen : 0f;
        return new BombView(left, total, 0, defusing, true, progress, true);
    }

    // ---------------------------------------------------------------- !c4

    private void OnC4Command(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid || player.IsBot) return;
        string arg = info.ArgCount > 1 ? info.GetArg(1).Trim().ToLowerInvariant() : "";

        if (arg.Length == 0)
        {
            if (!OpenMenu(player)) Chat(player, "chat.help");
            return;
        }

        switch (arg)
        {
            case "on":
                SetOn(player, true);
                Chat(player, "chat.on");
                return;
            case "off":
                SetOn(player, false);
                Chat(player, "chat.off");
                return;
            case "test" or "preview":
                StartPreview(player);
                return;
            case "help" or "?":
                Chat(player, "chat.help");
                return;
        }

        if (!Config.AllowPlayerChoice)
        {
            Chat(player, "chat.no_choice");
            return;
        }
        if (int.TryParse(arg, out int style))
        {
            if (!Config.Styles.Contains(style))
            {
                Chat(player, "chat.unknown", arg);
                return;
            }
            SetStyle(player, style);
            Chat(player, "chat.style", StyleName(player, style));
            return;
        }
        string? pos = NormPos(arg);
        if (pos != null && Config.Positions.Contains(pos))
        {
            SetPos(player, pos);
            Chat(player, "chat.position", PosName(player, pos));
            return;
        }
        Chat(player, "chat.unknown", arg);
    }

    internal string StyleName(CCSPlayerController? p, int style) => T(p, "style." + style);
    internal string PosName(CCSPlayerController? p, string pos) => T(p, "pos." + pos);

    // ---------------------------------------------------------------- texts (lang/*.json)

    internal string T(CCSPlayerController? p, string key, params object[] args)
    {
        string text;
        try
        {
            text = p != null && p.IsValid ? Localizer.ForPlayer(p, key) : Localizer[key].Value;
        }
        catch
        {
            text = key;
        }
        if (args.Length == 0) return text;
        try
        {
            return string.Format(text, args);
        }
        catch
        {
            return text;
        }
    }

    internal void Chat(CCSPlayerController p, string key, params object[] args)
    {
        if (p.IsValid) p.PrintToChat($" {ChatColors.Red}[C4]{ChatColors.Default} {T(p, key, args)}");
    }
}
