using System.Text.Json;

namespace ArkPlotWpf.Model;

public class PrtsAssets
{
    private const string EmptyJson = "{ }";
    public const string AudioAssetsUrl = "https://torappu.prts.wiki/assets/";

    private static PrtsAssets? _instance;

    public readonly List<PrtsData> AllData;

    /// table of Sound Effects and Musics
    public readonly StringDict DataAudio = new();

    /// table of Character Images
    public readonly StringDict DataChar = new();

    /// table of Background Images
    public readonly StringDict DataImage = new();

    public readonly Dictionary<string, Dictionary<string, object>> RideItems = new();

    /// <summary>
    ///     prts 的补丁数据。
    /// </summary>
    public JsonDocument DataOverrideDocument = JsonDocument.Parse(EmptyJson);

    public JsonDocument PortraitLinkDocument = JsonDocument.Parse(EmptyJson);

    /// set of preloaded resources
    public StringDict PreLoaded = new();

    private PrtsAssets()
    {
        AllData = new List<PrtsData>
        {
            new("Data_Image", DataImage),
            new("Data_Char", DataChar),
            new("Data_Audio", DataAudio),
            new("Data_Link"),
            new("Data_Override")
            /* { "Data_Override", DataOverride }, */
            /* { "Data_Link", DataLink } */
        };
    }

    public static PrtsAssets Instance => _instance ??= new PrtsAssets();
}