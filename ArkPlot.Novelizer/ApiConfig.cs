namespace ArkPlot.Novelizer;

public enum ApiProvider { Bailian, DeepSeek, Custom }

public class ApiConfig
{
    public ApiProvider Provider { get; set; } = ApiProvider.DeepSeek;
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.deepseek.com";
    public string[] Models { get; set; } = ["deepseek-v4-pro", "deepseek-v4-flash"];
    public bool EnableThinking { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxTokens { get; set; } = 16384;
}