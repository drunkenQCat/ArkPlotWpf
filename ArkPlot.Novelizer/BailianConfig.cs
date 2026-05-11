namespace ArkPlot.Novelizer;

public class BailianConfig
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
    public string[] Models { get; set; } = ["deepseek-v4-pro", "deepseek-v4-flash"];
    public bool EnableThinking { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxTokens { get; set; } = 16384;
}