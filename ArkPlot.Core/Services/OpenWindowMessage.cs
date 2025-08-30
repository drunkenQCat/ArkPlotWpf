namespace ArkPlot.Core.Services;

public class OpenWindowMessage
{
    public string WindowName { get; }
    public string JsonPath { get; }

    public OpenWindowMessage(string windowName, string jsonPath)
    {
        WindowName = windowName;
        JsonPath = jsonPath;
    }
}

