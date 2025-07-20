using ArkPlotWpf.Data.Entities;
using ArkPlotWpf.Model;
using ArkPlotWpf.Data.Mappers;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ArkPlotWpf.Data.Repositories;

public class PrtsAssetsRepository : IPrtsAssetsRepository
{
    private readonly PrtsDataRepository _prtsDataRepository;

    public PrtsAssetsRepository(string? connectionString = null)
    {
        _prtsDataRepository = new PrtsDataRepository(connectionString);
    }

    /// <summary>
    /// 保存PrtsAssets的所有数据到数据库
    /// </summary>
    /// <param name="prtsAssets">要保存的PrtsAssets实例</param>
    public void SavePrtsAssets(PrtsAssets prtsAssets)
    {
        // 保存各个PrtsData
        foreach (var prtsData in prtsAssets.AllData)
        {
            _prtsDataRepository.AddOrUpdatePrtsData(prtsData);
        }

        // 保存覆盖数据和链接数据
        SaveOverrideData(prtsAssets);
        SavePortraitLinkData(prtsAssets);
        SavePreLoadedData(prtsAssets);
    }

    /// <summary>
    /// 从数据库加载PrtsAssets的所有数据
    /// </summary>
    /// <returns>加载了数据的PrtsAssets实例</returns>
    public PrtsAssets LoadPrtsAssets()
    {
        var prtsAssets = PrtsAssets.Instance;

        // 加载各个PrtsData
        var allPrtsData = _prtsDataRepository.GetAllPrtsData();
        foreach (var prtsData in allPrtsData)
        {
            UpdatePrtsAssetsByData(prtsAssets, prtsData);
        }

        // 加载覆盖数据和链接数据
        LoadOverrideData(prtsAssets);
        LoadPortraitLinkData(prtsAssets);
        LoadPreLoadedData(prtsAssets);

        return prtsAssets;
    }

    /// <summary>
    /// 根据标签获取特定的PrtsData
    /// </summary>
    /// <param name="tag">数据标签</param>
    /// <returns>PrtsData实例</returns>
    public PrtsData? GetPrtsDataByTag(string tag)
    {
        return _prtsDataRepository.GetPrtsDataByTag(tag);
    }

    /// <summary>
    /// 更新特定的PrtsData
    /// </summary>
    /// <param name="prtsData">要更新的PrtsData</param>
    public void UpdatePrtsData(PrtsData prtsData)
    {
        _prtsDataRepository.AddOrUpdatePrtsData(prtsData);
    }

    /// <summary>
    /// 删除特定的PrtsData
    /// </summary>
    /// <param name="tag">要删除的数据标签</param>
    public void DeletePrtsData(string tag)
    {
        _prtsDataRepository.DeletePrtsData(tag);
    }

    private void UpdatePrtsAssetsByData(PrtsAssets prtsAssets, PrtsData prtsData)
    {
        switch (prtsData.Tag)
        {
            case "Data_Audio":
                prtsAssets.DataAudio.Clear();
                foreach (var kvp in prtsData.Data)
                {
                    prtsAssets.DataAudio[kvp.Key] = kvp.Value;
                }
                break;
            case "Data_Char":
                prtsAssets.DataChar.Clear();
                foreach (var kvp in prtsData.Data)
                {
                    prtsAssets.DataChar[kvp.Key] = kvp.Value;
                }
                break;
            case "Data_Image":
                prtsAssets.DataImage.Clear();
                foreach (var kvp in prtsData.Data)
                {
                    prtsAssets.DataImage[kvp.Key] = kvp.Value;
                }
                break;
        }
    }

    private void SaveOverrideData(PrtsAssets prtsAssets)
    {
        var overrideData = new PrtsData("Data_Override");
        overrideData.Data["OverrideDocument"] = prtsAssets.DataOverrideDocument.RootElement.ToString();
        _prtsDataRepository.AddOrUpdatePrtsData(overrideData);
    }

    private void SavePortraitLinkData(PrtsAssets prtsAssets)
    {
        var portraitLinkData = new PrtsData("Data_Link");
        portraitLinkData.Data["PortraitLinkDocument"] = prtsAssets.PortraitLinkDocument.RootElement.ToString();
        _prtsDataRepository.AddOrUpdatePrtsData(portraitLinkData);
    }

    private void SavePreLoadedData(PrtsAssets prtsAssets)
    {
        var preLoadedData = new PrtsData("Data_PreLoaded");
        foreach (var kvp in prtsAssets.PreLoaded)
        {
            preLoadedData.Data[kvp.Key] = kvp.Value;
        }
        _prtsDataRepository.AddOrUpdatePrtsData(preLoadedData);
    }

    private void LoadOverrideData(PrtsAssets prtsAssets)
    {
        var overrideData = _prtsDataRepository.GetPrtsDataByTag("Data_Override");
        if (overrideData?.Data.ContainsKey("OverrideDocument") == true)
        {
            try
            {
                prtsAssets.DataOverrideDocument = JsonDocument.Parse(overrideData.Data["OverrideDocument"]);
            }
            catch
            {
                prtsAssets.DataOverrideDocument = JsonDocument.Parse("{}");
            }
        }
    }

    private void LoadPortraitLinkData(PrtsAssets prtsAssets)
    {
        var portraitLinkData = _prtsDataRepository.GetPrtsDataByTag("Data_Link");
        if (portraitLinkData?.Data.ContainsKey("PortraitLinkDocument") == true)
        {
            try
            {
                prtsAssets.PortraitLinkDocument = JsonDocument.Parse(portraitLinkData.Data["PortraitLinkDocument"]);
            }
            catch
            {
                prtsAssets.PortraitLinkDocument = JsonDocument.Parse("{}");
            }
        }
    }

    private void LoadPreLoadedData(PrtsAssets prtsAssets)
    {
        var preLoadedData = _prtsDataRepository.GetPrtsDataByTag("Data_PreLoaded");
        if (preLoadedData != null)
        {
            prtsAssets.PreLoaded.Clear();
            foreach (var kvp in preLoadedData.Data)
            {
                prtsAssets.PreLoaded[kvp.Key] = kvp.Value;
            }
        }
    }

    public void Save(PrtsAssets prtsAssets)
    {
        SavePrtsAssets(prtsAssets);
    }

    public PrtsAssets Load()
    {
        return LoadPrtsAssets();
    }
} 