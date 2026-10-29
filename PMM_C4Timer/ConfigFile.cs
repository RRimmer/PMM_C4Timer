using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PMM_C4Timer;

// configs/plugins/PMM_C4Timer/PMM_C4Timer.jsonc: JSON with comments and trailing commas.
// CounterStrikeSharp loads only .json / .toml, so the plugin reads this file itself.
//   - no .jsonc, but an old PMM_C4Timer.json (0.1.0 before jsonc): its values are moved into a new .jsonc;
//   - no file at all: a .jsonc with the defaults and comments is written.
// The file is never rewritten when it exists, so the admin's own comments stay.
internal static class ConfigFile
{
    public const string FileName = "PMM_C4Timer.jsonc";
    private const string OldFileName = "PMM_C4Timer.json";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public static C4TimerConfig Load(string dir, ILogger log)
    {
        string path = Path.Combine(dir, FileName);
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<C4TimerConfig>(File.ReadAllText(path), ReadOptions) ?? new C4TimerConfig();

            var config = new C4TimerConfig();
            string old = Path.Combine(dir, OldFileName);
            if (File.Exists(old))
            {
                config = JsonSerializer.Deserialize<C4TimerConfig>(File.ReadAllText(old), ReadOptions) ?? new C4TimerConfig();
                log.LogInformation("{Old} moved to {New} (the old file is not used any more)", OldFileName, FileName);
            }
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, Render(config), new UTF8Encoding(false));
            return config;
        }
        catch (Exception e)
        {
            log.LogError("{File} was not read, using the defaults: {Error}", path, e.Message);
            return new C4TimerConfig();
        }
    }

    private static string J(object value) => JsonSerializer.Serialize(value);
    private static string B(bool v) => v ? "true" : "false";
    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    // The file with comments. Keep in step with ../PMM_C4Timer.example.jsonc.
    public static string Render(C4TimerConfig c)
    {
        var s = new StringBuilder();
        s.Append("// PMM_C4Timer — таймер C4 на панораме.\n");
        s.Append("// Комментарии // и /* */ и запятая после последнего элемента разрешены.\n");
        s.Append("// После правки: css_pmm_c4 reload в консоли сервера (Commands и UpdateInterval — после перезапуска плагина).\n");
        s.Append("{\n");
        s.Append("  // false — плагин ничего не показывает.\n");
        s.Append($"  \"Enabled\": {B(c.Enabled)},\n\n");
        s.Append("  // Скомпилированный layout в клиентском аддоне (с _c на конце).\n");
        s.Append($"  \"LayoutPath\": {J(c.LayoutPath)},\n\n");
        s.Append("  // Вид для тех, кто не выбирал сам:\n");
        s.Append("  //   1 — цифры, 2 — полоса, 3 — кольцо, 4 — плашка с метками 5 с / 10 с,\n");
        s.Append("  //   5 — сегменты, 6 — вертикальная шкала, 8 — чип как у таймера раунда, 9 — таймер и прогресс дефьюза.\n");
        s.Append($"  \"DefaultStyle\": {c.DefaultStyle},\n\n");
        s.Append("  // Место для тех, кто не выбирал сам:\n");
        s.Append("  //   top — под игроками, radar — под радаром, cross — под прицелом, bottom — внизу, right — справа.\n");
        s.Append($"  \"DefaultPosition\": {J(c.DefaultPosition)},\n\n");
        s.Append("  // Какие виды и места игрок может выбрать в !c4.\n");
        s.Append($"  \"Styles\": {J(c.Styles)},\n");
        s.Append($"  \"Positions\": {J(c.Positions)},\n\n");
        s.Append("  // false — у всех вид и место по умолчанию, !c4 только включает и выключает таймер.\n");
        s.Append($"  \"AllowPlayerChoice\": {B(c.AllowPlayerChoice)},\n\n");
        s.Append("  // Кому показывать таймер.\n");
        s.Append("  \"Show\": {\n");
        s.Append($"    \"AliveCT\": {B(c.Show.AliveCT)},       // живые CT\n");
        s.Append($"    \"AliveT\": {B(c.Show.AliveT)},        // живые T\n");
        s.Append($"    \"Dead\": {B(c.Show.Dead)},          // мёртвые\n");
        s.Append($"    \"Spectators\": {B(c.Show.Spectators)}     // наблюдатели\n");
        s.Append("  },\n\n");
        s.Append("  // Кому показывать, что бомбу обезвреживают: «Обезвреживают», кит, процент, успевает или нет (вид 9).\n");
        s.Append("  // Кому нельзя — видит только таймер бомбы; сервер не отправляет ему данные о дефьюзе вообще.\n");
        s.Append("  \"ShowDefuse\": {\n");
        s.Append($"    \"AliveCT\": {B(c.ShowDefuse.AliveCT)},       // живые CT\n");
        s.Append($"    \"AliveT\": {B(c.ShowDefuse.AliveT)},       // живые T\n");
        s.Append($"    \"Dead\": {B(c.ShowDefuse.Dead)},          // мёртвые\n");
        s.Append($"    \"Spectators\": {B(c.ShowDefuse.Spectators)}     // наблюдатели\n");
        s.Append("  },\n\n");
        s.Append("  // Команды в чат: css_c4 = !c4 и /c4.\n");
        s.Append($"  \"Commands\": {J(c.Commands)},\n\n");
        s.Append("  // Как часто обновлять таймер, секунд (0.05 – 1). Полосы между обновлениями двигаются плавно.\n");
        s.Append($"  \"UpdateInterval\": {F(c.UpdateInterval)},\n\n");
        s.Append("  // Цвет по оставшемуся времени: с WarnSeconds — жёлтый, с CritSeconds — красный и мигание.\n");
        s.Append($"  \"WarnSeconds\": {F(c.WarnSeconds)},\n");
        s.Append($"  \"CritSeconds\": {F(c.CritSeconds)},\n\n");
        s.Append("  // Сколько секунд держать таймер на экране после дефьюза или взрыва (0 — сразу убрать).\n");
        s.Append($"  \"HideDelay\": {F(c.HideDelay)},\n\n");
        s.Append("  // Подробный лог.\n");
        s.Append($"  \"Debug\": {B(c.Debug)},\n\n");
        s.Append($"  \"ConfigVersion\": {c.Version}\n");
        s.Append("}\n");
        return s.ToString();
    }
}
