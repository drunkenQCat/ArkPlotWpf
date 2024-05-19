namespace ArkPlotWpf.Utilities.TagProcessingComponents;

public partial class TagProcessor
{
    private string GetUrlFromPreloaded(string key)
    {
        var isKeyExists = _prts.Res.PreLoaded.TryGetValue(key, out var url);
        return isKeyExists ? url! : "";
    }

    private string GetImageUrl(string newTag, string newValueTrimed)
    {
        // in csv, the background is bg_bg, fuck
        if (newTag.Contains('景')) newValueTrimed = $"bg_{newValueTrimed}";
        return GetUrlFromPreloaded(newValueTrimed);
    }

    private string ConvertToImageTag(string newTag, string newValue)
    {
        var url = GetImageUrl(newTag, newValue);
        return $"<img  src=\"{url}\" alt=\"{newValue}\" loading=\"lazy\" style=\"max-height:350px\"/>";
    }

    private string ConvertToAudioTag(string newTag, string newValue)
    {
        var url = GetUrlFromPreloaded(newValue);
        url =
            $"<audio controls class=\"lazy-audio\" width=\"300\" alt=\"{newValue}\"><source src=\"{url}\" type=\"audio/mpeg\"></audio>";
        if (newTag.Contains('乐'))
        {
            var urlParts = url.Split(" ");
            urlParts[0] += " class=\"music\"";
            url = string.Join(" ", urlParts);
        }

        return url;
    }

    private string ConvertToPortraitTag(string newValue)
    {
        var url = GetUrlFromPreloaded(newValue);
        return
            $"<img class=\"portrait\" src=\"{url}\" alt=\"{newValue}\" loading=\"lazy\" style=\"max-height:300px\"/>";
    }
}