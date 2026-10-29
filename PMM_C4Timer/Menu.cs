using System.Runtime.CompilerServices;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Menu;
using MenuManager;
using Microsoft.Extensions.Logging;

namespace PMM_C4Timer;

// !c4 settings through PMM core (menu:nfcore). Only plain options are used, so the menu works in
// every menu type the player picked in !menu. Without PMM core !c4 prints the chat commands.
public partial class Plugin
{
    private object? _menuUi;   // MenuUi; object, so this class loads without MenuManagerApi.dll

    internal bool MenuReady => _menuUi != null;

    private void MenuAttach()
    {
        try
        {
            _menuUi = CreateMenuUi();
        }
        catch (Exception e)
        {
            _menuUi = null;
            Logger.LogWarning("PMM core menu API is not available ({Error}): !c4 works with chat commands", e.Message);
            return;
        }
        if (_menuUi == null)
            Logger.LogWarning("PMM core (menu:nfcore) not found: !c4 works with chat commands");
    }

    // Separate method: if MenuManagerApi.dll is missing, the type load fails here, inside the try above.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private object? CreateMenuUi() => MenuUi.Create(this);

    private bool OpenMenu(CCSPlayerController player)
    {
        if (_menuUi == null) return false;
        try
        {
            return OpenMenuCore(player);
        }
        catch (Exception e)
        {
            Logger.LogWarning("!c4 menu failed: {Error}", e.Message);
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool OpenMenuCore(CCSPlayerController player) => ((MenuUi)_menuUi!).OpenMain(player);
}

internal sealed class MenuUi
{
    private static readonly PluginCapability<IMenuApi?> Capability = new("menu:nfcore");

    private readonly Plugin _plugin;
    private readonly IMenuApi _api;

    private MenuUi(Plugin plugin, IMenuApi api)
    {
        _plugin = plugin;
        _api = api;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static MenuUi? Create(Plugin plugin)
    {
        IMenuApi? api;
        try { api = Capability.Get(); } catch { api = null; }
        return api == null ? null : new MenuUi(plugin, api);
    }

    public bool OpenMain(CCSPlayerController player)
    {
        if (!player.IsValid) return false;
        var pref = _plugin.Pref(player);
        IMenu menu = _api.GetMenu(_plugin.T(player, "menu.title"));

        menu.AddMenuOption(_plugin.T(player, pref.On ? "menu.on" : "menu.off"), (p, _) =>
        {
            _plugin.SetOn(p, !_plugin.Pref(p).On);
            OpenMain(p);
        });

        if (_plugin.Config.AllowPlayerChoice)
        {
            menu.AddMenuOption(_plugin.T(player, "menu.style", _plugin.StyleName(player, _plugin.StyleOf(player))),
                (p, _) => OpenStyles(p));
            menu.AddMenuOption(_plugin.T(player, "menu.position", _plugin.PosName(player, _plugin.PosOf(player))),
                (p, _) => OpenPositions(p));
        }

        menu.AddMenuOption(_plugin.T(player, "menu.preview"), (p, _) =>
        {
            _plugin.StartPreview(p);
            OpenMain(p);
        });

        menu.Open(player);
        return true;
    }

    private void OpenStyles(CCSPlayerController player)
    {
        int cur = _plugin.StyleOf(player);
        IMenu menu = _api.GetMenu(_plugin.T(player, "menu.style_title"), p => OpenMain(p));
        foreach (int style in _plugin.Config.Styles)
        {
            int s = style;
            string mark = s == cur ? " ✓" : "";
            menu.AddMenuOption($"{s}. {_plugin.StyleName(player, s)}{mark}", (p, _) =>
            {
                _plugin.SetStyle(p, s);
                OpenStyles(p);
            });
        }
        menu.Open(player);
    }

    private void OpenPositions(CCSPlayerController player)
    {
        string cur = _plugin.PosOf(player);
        IMenu menu = _api.GetMenu(_plugin.T(player, "menu.position_title"), p => OpenMain(p));
        foreach (var pos in _plugin.Config.Positions)
        {
            string ps = pos;
            string mark = ps == cur ? " ✓" : "";
            menu.AddMenuOption(_plugin.PosName(player, ps) + mark, (p, _) =>
            {
                _plugin.SetPos(p, ps);
                OpenPositions(p);
            });
        }
        menu.Open(player);
    }
}
