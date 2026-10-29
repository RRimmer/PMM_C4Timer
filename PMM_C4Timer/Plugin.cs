using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace PMM_C4Timer;

// C4 timer on the HUD (custom_hud_layout pmm_c4timer.xml). One entity, two roots:
//   #c4-root  the real timer: time, bars, defuse state are global (one write reaches everybody),
//             style, position and "this player does not see it" are per player;
//   #c4-prev  the !c4 preview: everything per player, so a preview never touches the real timer.
// The server only writes dialog variables and toggles classes; bars are clip ladders that the
// stylesheet animates between steps.
public partial class Plugin : BasePlugin
{
    public override string ModuleName => "PMM_C4Timer";
    public override string ModuleVersion => "0.1.0";
    public override string ModuleAuthor => "Rimmer";
    public override string ModuleDescription => "C4 timer on the CS2 Panorama HUD";

    // Read from configs/plugins/PMM_C4Timer/PMM_C4Timer.jsonc by ConfigFile (not by CounterStrikeSharp).
    public C4TimerConfig Config { get; set; } = new();

    private string ConfigDir =>
        Path.GetFullPath(Path.Combine(ModuleDirectory, "..", "..", "configs", "plugins", ModuleName));

    internal static readonly int[] AllStyles = { 1, 2, 3, 4, 5, 6, 8, 9 };
    internal static readonly string[] AllPositions = { "top", "radar", "cross", "bottom", "right" };

    private Timer? _tick;

    public void OnConfigParsed(C4TimerConfig config)
    {
        config.Styles = (config.Styles ?? new()).Where(s => AllStyles.Contains(s)).Distinct().ToList();
        if (config.Styles.Count == 0) config.Styles.AddRange(AllStyles);
        if (!AllStyles.Contains(config.DefaultStyle))
        {
            Logger.LogWarning("DefaultStyle {Style} is unknown (1, 2, 3, 4, 5, 6, 8, 9), using 4", config.DefaultStyle);
            config.DefaultStyle = 4;
        }

        config.Positions = (config.Positions ?? new()).Select(NormPos).Where(p => p != null).Select(p => p!).Distinct().ToList();
        if (config.Positions.Count == 0) config.Positions.AddRange(AllPositions);
        string? pos = NormPos(config.DefaultPosition);
        if (pos == null)
        {
            Logger.LogWarning("DefaultPosition \"{Pos}\" is unknown (top, radar, cross, bottom, right), using top", config.DefaultPosition);
            pos = "top";
        }
        config.DefaultPosition = pos;

        if (config.UpdateInterval < 0.05f) config.UpdateInterval = 0.05f;
        if (config.UpdateInterval > 1f) config.UpdateInterval = 1f;
        if (config.CritSeconds < 0f) config.CritSeconds = 0f;
        if (config.WarnSeconds < config.CritSeconds) config.WarnSeconds = config.CritSeconds;
        if (config.HideDelay < 0f) config.HideDelay = 0f;
        config.Commands = (config.Commands ?? new()).Select(c => c.Trim()).Where(c => c.Length > 0).Distinct().ToList();
        if (string.IsNullOrWhiteSpace(config.LayoutPath)) config.LayoutPath = new C4TimerConfig().LayoutPath;
        Config = config;
    }

    // Accepts the names and the letters of the drawings (A top, B radar, C cross, D bottom, E right).
    internal static string? NormPos(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "top" or "a" or "up" or "players" => "top",
        "radar" or "b" or "left" => "radar",
        "cross" or "crosshair" or "c" or "center" or "centre" => "cross",
        "bottom" or "d" or "down" => "bottom",
        "right" or "e" or "side" => "right",
        _ => null
    };

    public override void Load(bool hotReload)
    {
        OnConfigParsed(ConfigFile.Load(ConfigDir, Logger));
        PlayersLoad();

        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            BombClear();
            HudReset();
        });
        RegisterListener<Listeners.OnClientDisconnect>(slot =>
        {
            HudForgetSlot(slot);
            PlayerForgetSlot(slot);
        });

        foreach (var cmd in Config.Commands)
            AddCommand(cmd, "PMM_C4Timer: timer settings", OnC4Command);

        _tick = AddTimer(Config.UpdateInterval, Tick, TimerFlags.REPEAT);

        if (hotReload) Server.NextFrame(BombFind);
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        MenuAttach();
    }

    public override void Unload(bool hotReload)
    {
        _tick?.Kill();
        _tick = null;
        BombClear();
        HudRemove();
    }

    // ---------------------------------------------------------------- game events

    [GameEventHandler]
    public HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
    {
        // The planted_c4 entity exists on the next frame.
        Server.NextFrame(BombFind);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info)
    {
        BombFinish();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombExploded(EventBombExploded @event, GameEventInfo info)
    {
        BombFinish();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundPrestart(EventRoundPrestart @event, GameEventInfo info)
    {
        BombClear();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        BombClear();
        // The entity is made in advance, so the client has the layout loaded when the bomb goes down.
        if (Config.Enabled) Server.NextFrame(() => EnsureHud());
        return HookResult.Continue;
    }

    // ---------------------------------------------------------------- tick

    // Every UpdateInterval: the real timer (global), then every player (mute, style, position,
    // texts, preview). Sends only differences.
    private void Tick()
    {
        if (!Config.Enabled)
        {
            if (HudAlive) GClass(RootId, "c4-on", false);
            return;
        }

        BombView? view = BombCurrent();
        bool previews = AnyPreview();
        if (view == null && !previews)
        {
            if (HudAlive) GClass(RootId, "c4-on", false);
            return;
        }
        if (!EnsureHud()) return;

        if (view != null)
        {
            PaintTimer(GlobalSink, view.Value);
            PaintDefuse(GlobalSink, null);   // the defuse goes per player (ShowDefuse)
        }
        GClass(RootId, "c4-on", view != null);

        foreach (var p in Utilities.GetPlayers())
        {
            if (p == null || !p.IsValid || p.IsBot || p.IsHLTV) continue;
            RenderPlayer(p, view);
        }
    }

    // ---------------------------------------------------------------- server console

    [ConsoleCommand("css_pmm_c4", "PMM_C4Timer status; css_pmm_c4 reload = read the config again")]
    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    public void OnStatus(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount > 1 && info.GetArg(1).Equals("reload", StringComparison.OrdinalIgnoreCase))
        {
            OnConfigParsed(ConfigFile.Load(ConfigDir, Logger));
            _tick?.Kill();
            _tick = AddTimer(Config.UpdateInterval, Tick, TimerFlags.REPEAT);
            if (HudAlive) PaintDefaults();
            info.ReplyToCommand($"[PMM_C4Timer] {ConfigFile.FileName} reloaded. Commands are applied after a plugin reload.");
            return;
        }
        var v = BombCurrent();
        info.ReplyToCommand($"[PMM_C4Timer] {ModuleVersion} enabled={Config.Enabled} hud={(HudAlive ? "alive" : "none")} layout={Config.LayoutPath}");
        info.ReplyToCommand($"  bomb: {(v == null ? "none" : $"left={v.Value.Left:0.0} total={v.Value.Total:0.0} site={SiteLetter(v.Value.Site)} defusing={v.Value.Defusing} kit={v.Value.Kit}")}");
        info.ReplyToCommand($"  menu api: {(MenuReady ? "PMM core" : "none (chat commands only)")}");
        foreach (var p in Utilities.GetPlayers())
        {
            if (p == null || !p.IsValid || p.IsBot) continue;
            var pr = Pref(p);
            info.ReplyToCommand($"  {p.PlayerName}: on={pr.On} style={StyleOf(p)} pos={PosOf(p)} sees={Sees(p)} defuse={SeesDefuse(p)}");
        }
    }

    private void Dbg(string msg, params object?[] args)
    {
        if (Config.Debug) Logger.LogInformation(msg, args);
    }
}
