using System.Text.Json;

namespace ArkPlotWpf.Model;

class ResourceCsv
{
    private const string EmptyJson = "{ }";
    public const string AssetsUrl = "https://torappu.prts.wiki/assets/";

    /// table of Background Images
    public readonly StringDict DataImage = new();
    /// table of Character Images
    public readonly StringDict DataChar = new();
    /// table of Sound Effects and Musics
    public readonly StringDict DataAudio = new();
    /// set of preloaded resources
    public StringDict PreLoaded = new();
    /// <summary>
    /// table of links in prts
    /// </summary>
    public JsonDocument DataOverrideDocument = JsonDocument.Parse(EmptyJson);
    public JsonDocument PortraitLinkDocument = JsonDocument.Parse(EmptyJson);
    /* public StringDict DataOverride = new(); */
    /* public StringDict DataLink = new(); */
    public readonly List<PrtsData> AllData;
    public readonly Dictionary<string, Dictionary<string, object>> RideItems = new();
    
    private static ResourceCsv? _instance;
    public static ResourceCsv Instance => _instance ??= new ResourceCsv();

    private ResourceCsv()
    {
        AllData = new(){
            new PrtsData( "Data_Image", DataImage ),
            new PrtsData( "Data_Char", DataChar  ),
            new PrtsData( "Data_Audio", DataAudio ),
            new PrtsData( "Data_Link"),
            new PrtsData( "Data_Override")
            /* { "Data_Override", DataOverride }, */
            /* { "Data_Link", DataLink } */
        };
    }
}
