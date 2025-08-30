using System.Text.Json;

namespace ArkPlot.Core.Model;

/// <summary>
/// 管理PRTS资源数据的单例类，包含音频、角色图片、背景图片等资源数据
/// </summary>
public class PrtsAssets
{
    /// <summary>
    /// 空JSON文档常量
    /// </summary>
    private const string EmptyJson = "{ }";

    /// <summary>
    /// PRTS音频资源的基础URL
    /// </summary>
    public const string AudioAssetsUrl = "https://torappu.prts.wiki/assets/";

    private static PrtsAssets? _instance;

    /// <summary>
    /// 所有PRTS数据集合，包含图片、角色、音频等资源数据
    /// </summary>
    public List<PrtsData> AllData;

    /// table of Sound Effects and Musics
    public StringDict DataAudio = new();

    /// table of Character Images
    public StringDict DataChar = new();

    /// table of Background Images
    public StringDict DataImage = new();

    /// <summary>
    /// 覆盖数据字典，包含多层嵌套的配置数据
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> RideItems = new();

    /// <summary>
    /// PRTS补丁数据文档，用于覆盖默认配�?
    /// </summary>
    public JsonDocument DataOverrideDocument = JsonDocument.Parse(EmptyJson);

    /// <summary>
    /// 角色立绘链接数据文档
    /// </summary>
    public JsonDocument PortraitLinkDocument = JsonDocument.Parse(EmptyJson);

    /// set of preloaded resources
    /// <summary>
    /// 预加载资源集合，用于快速访问常用资�?
    /// </summary>
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

    public void RestoreAllData()
    {
        DataImage = AllData[0].Data;
        DataChar = AllData[1].Data;
        DataAudio = AllData[2].Data;
    }

    /// <summary>
    /// 获取PrtsAssets单例实例
    /// </summary>
    public static PrtsAssets Instance => _instance ??= new PrtsAssets();
}
