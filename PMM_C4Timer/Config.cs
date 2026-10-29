using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace PMM_C4Timer;

// Loaded from configs/plugins/PMM_C4Timer/PMM_C4Timer.jsonc by ConfigFile.cs (comments allowed).
// A new key must also be added to ConfigFile.Render and ../PMM_C4Timer.example.jsonc.
public class C4TimerConfig : BasePluginConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;

    // Compiled layout inside the client addon (note the _c).
    [JsonPropertyName("LayoutPath")] public string LayoutPath { get; set; } = "panorama/layout/custom_game/pmm_c4timer.vxml_c";

    // Style for a player who did not pick one: 1 text, 2 bar, 3 ring, 4 card + defuse marks,
    // 5 segments, 6 vertical bar, 8 chip, 9 timer + defuse progress.
    [JsonPropertyName("DefaultStyle")] public int DefaultStyle { get; set; } = 4;

    // Position for a player who did not pick one: top, radar, cross, bottom, right.
    [JsonPropertyName("DefaultPosition")] public string DefaultPosition { get; set; } = "top";

    // Styles and positions a player can choose in !c4. A default missing here is still used as the default.
    [JsonPropertyName("Styles")] public List<int> Styles { get; set; } = new() { 1, 2, 3, 4, 5, 6, 8, 9 };
    [JsonPropertyName("Positions")] public List<string> Positions { get; set; } = new() { "top", "radar", "cross", "bottom", "right" };

    // false: everyone sees DefaultStyle / DefaultPosition, !c4 only turns the timer on and off.
    [JsonPropertyName("AllowPlayerChoice")] public bool AllowPlayerChoice { get; set; } = true;

    // Who sees the timer. Alive players by team, dead players, spectators (and GOTV).
    [JsonPropertyName("Show")] public ShowOptions Show { get; set; } = new();

    // Who sees that the bomb is being defused (style 9: "Defusing", kit, progress, in time or not).
    // Players who may not see it get no defuse data from the server at all, only the bomb timer.
    [JsonPropertyName("ShowDefuse")] public ShowOptions ShowDefuse { get; set; } = new() { AliveT = false };

    // Chat commands: css_c4 = !c4 / /c4.
    [JsonPropertyName("Commands")] public List<string> Commands { get; set; } = new() { "css_c4", "css_c4timer" };

    // How often the timer is updated, seconds. The bars move smoothly in between (CSS transition).
    [JsonPropertyName("UpdateInterval")] public float UpdateInterval { get; set; } = 0.1f;

    // Colour phases, seconds left: >= Warn green, >= Crit amber, below red and blinking.
    [JsonPropertyName("WarnSeconds")] public float WarnSeconds { get; set; } = 20f;
    [JsonPropertyName("CritSeconds")] public float CritSeconds { get; set; } = 10f;

    // Seconds the timer stays on screen after a defuse / explosion (0 = hide at once).
    [JsonPropertyName("HideDelay")] public float HideDelay { get; set; } = 0f;

    [JsonPropertyName("Debug")] public bool Debug { get; set; } = false;

    [JsonPropertyName("ConfigVersion")] public override int Version { get; set; } = 1;
}

public class ShowOptions
{
    [JsonPropertyName("AliveCT")] public bool AliveCT { get; set; } = true;
    [JsonPropertyName("AliveT")] public bool AliveT { get; set; } = true;
    [JsonPropertyName("Dead")] public bool Dead { get; set; } = true;
    [JsonPropertyName("Spectators")] public bool Spectators { get; set; } = true;
}
