using EdgeTTS.DotNet;

var voices = await Voices.ListVoicesAsync();
var zhCnVoices = voices
    .Where(v => v.Locale.StartsWith("zh-"))
    .OrderBy(v => v.Locale)
    .ThenBy(v => v.Gender)
    .ThenBy(v => v.ShortName);

Console.WriteLine($"Total zh-CN voices: {zhCnVoices.Count()}\n");

foreach (var v in zhCnVoices)
{
    Console.WriteLine($"{v.ShortName}\t{v.Gender}");
}
