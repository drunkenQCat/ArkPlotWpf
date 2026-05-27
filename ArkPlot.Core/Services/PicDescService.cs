using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using SqlSugar;

namespace ArkPlot.Core.Services;

/// <summary>
/// 图片描述服务。
///
/// 生命周期：
/// 1. 检查数据库缓存 → 有则直接返回
/// 2. 无缓存 → 调用视觉模型（百炼/Ollama）生成描述
/// 3. 写入数据库缓存
/// 4. 如果使用了本地临时文件，立即清理
///
/// Debug 模式下强制跳过缓存，重新描述。
/// </summary>
public class PicDescService : IDisposable
{
    private readonly SqlSugarClient _db;
    private readonly Func<string, Task<string>>? _describeByUrl;
    private readonly string _cacheDir;
    private readonly bool _debugMode;

    /// <summary>
    /// 创建 PicDescService 实例。
    /// </summary>
    /// <param name="describeByUrl">可选的图片描述函数，接收图片 URL 返回描述文本。为 null 时使用占位符模式。</param>
    /// <param name="debugMode">Debug 模式：强制跳过数据库缓存，重新生成描述并清理。</param>
    public PicDescService(Func<string, Task<string>>? describeByUrl = null, bool debugMode = false)
    {
        _describeByUrl = describeByUrl;
        _debugMode = debugMode;

        _db = DbFactory.GetClient();

        // 临时图片缓存目录
        _cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "PicCache");
        if (!Directory.Exists(_cacheDir))
            Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>
    /// 获取或创建图片描述。
    /// 非图片 URL（如 MP3 音频）直接返回空字符串，不入库。
    /// </summary>
    public async Task<string> GetOrCreatePicDescAsync(string imageUrl)
    {
        // 非图片 URL 直接跳过，不下载、不描述、不入库
        if (!IsImageUrl(imageUrl))
            return "";

        // Debug 模式：强制重新生成
        if (_debugMode)
        {
            return await GenerateAndCacheAsync(imageUrl);
        }

        // 正常模式：先查数据库
        var existing = await GetPicDescFromDbAsync(imageUrl);
        if (existing != null)
            return existing;

        // 无缓存 → 生成并缓存
        return await GenerateAndCacheAsync(imageUrl);
    }

    /// <summary>
    /// 清理数据库中已存在的非图片 URL 记录（如误入库的 MP3）。
    /// </summary>
    public int CleanNonImageRecords()
    {
        var allRecords = _db.Queryable<PicDescription>().ToList();
        var toDelete = allRecords.Where(r => !IsImageUrl(r.ImageUrl)).ToList();

        foreach (var record in toDelete)
        {
            _db.Deleteable<PicDescription>().In(record.Id).ExecuteCommand();
        }

        return toDelete.Count;
    }

    /// <summary>
    /// 批量获取或创建图片描述。
    /// 自动过滤非图片 URL（如 MP3 音频），只处理图片格式。
    /// </summary>
    public async Task<Dictionary<string, string>> GetOrCreatePicDescsAsync(IEnumerable<string> imageUrls)
    {
        var result = new Dictionary<string, string>();
        foreach (var url in imageUrls)
        {
            if (!IsImageUrl(url))
            {
                result[url] = "";
                continue;
            }
            result[url] = await GetOrCreatePicDescAsync(url);
        }
        return result;
    }

    /// <summary>
    /// 判断 URL 是否是图片。
    /// 支持的格式：png, jpg, jpeg, gif, webp, bmp, svg, apng, avif。
    /// </summary>
    private static bool IsImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        var cleanUrl = url.Split('?')[0].ToLowerInvariant();
        var ext = Path.GetExtension(cleanUrl);

        return ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp"
                or ".bmp" or ".svg" or ".apng" or ".avif" => true,
            _ => false
        };
    }

    /// <summary>
    /// 生成图片描述并缓存到数据库。
    /// </summary>
    private async Task<string> GenerateAndCacheAsync(string imageUrl)
    {
        try
        {
            string description;
            if (_describeByUrl != null)
            {
                description = await _describeByUrl(imageUrl);
            }
            else
            {
                description = GeneratePlaceholder(imageUrl);
            }

            UpsertPicDesc(imageUrl, description);
            return description;
        }
        catch (Exception ex)
        {
            var errorDesc = $"[DESC_ERROR: {ex.Message.Truncate(100)}]";
            UpsertPicDesc(imageUrl, errorDesc);
            return errorDesc;
        }
    }

    /// <summary>
    /// 下载图片到临时缓存目录。
    /// </summary>
    private static async Task<string> DownloadImageAsync(string imageUrl)
    {
        var fileName = ComputeMd5(imageUrl);
        var extension = ExtractImageExtension(imageUrl);
        var tempFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "PicCache", $"{fileName}{extension}");

        if (File.Exists(tempFilePath))
            return tempFilePath;

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(5);

        var response = await http.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await contentStream.CopyToAsync(fileStream);

        return tempFilePath;
    }

    private static string ExtractImageExtension(string imageUrl)
    {
        try
        {
            var uri = new Uri(imageUrl);
            var ext = Path.GetExtension(uri.LocalPath).ToLowerInvariant();
            return string.IsNullOrEmpty(ext) || ext == "." ? ".png" : ext;
        }
        catch
        {
            var parts = imageUrl.Split('?')[0];
            var ext = Path.GetExtension(parts).ToLowerInvariant();
            return string.IsNullOrEmpty(ext) || ext == "." ? ".png" : ext;
        }
    }

    private static string ComputeMd5(string input)
    {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 清理临时图片缓存目录中的所有文件。
    /// </summary>
    public void CleanCacheDirectory()
    {
        if (!Directory.Exists(_cacheDir)) return;

        var files = Directory.GetFiles(_cacheDir);
        foreach (var file in files)
        {
            try { File.Delete(file); }
            catch { /* 被占用的文件跳过 */ }
        }
    }

    /// <summary>
    /// 获取缓存目录的当前大小（字节）。
    /// </summary>
    public long GetCacheDirectorySize()
    {
        if (!Directory.Exists(_cacheDir)) return 0;

        long size = 0;
        foreach (var file in Directory.GetFiles(_cacheDir))
        {
            try { size += new FileInfo(file).Length; }
            catch { }
        }
        return size;
    }

    private async Task<string?> GetPicDescFromDbAsync(string imageUrl)
    {
        var record = await _db.Queryable<PicDescription>()
            .FirstAsync(it => it.ImageUrl == imageUrl);
        return record?.PicDesc;
    }

    private void UpsertPicDesc(string imageUrl, string desc)
    {
        var now = DateTime.UtcNow;
        var existing = _db.Queryable<PicDescription>()
            .First(it => it.ImageUrl == imageUrl);

        if (existing != null)
        {
            _db.Updateable<PicDescription>()
                .SetColumns(it => it.PicDesc == desc)
                .SetColumns(it => it.UpdatedAt == now)
                .Where(it => it.ImageUrl == imageUrl)
                .ExecuteCommand();
        }
        else
        {
            _db.Insertable(new PicDescription
            {
                ImageUrl = imageUrl,
                PicDesc = desc,
                CreatedAt = now,
                UpdatedAt = now
            }).ExecuteCommand();
        }
    }

    private static string GeneratePlaceholder(string imageUrl)
    {
        var fileName = imageUrl;
        try
        {
            var uri = new Uri(imageUrl);
            fileName = Path.GetFileName(uri.LocalPath);
        }
        catch
        {
            var parts = imageUrl.Split('?')[0];
            fileName = parts.Split('/').LastOrDefault() ?? imageUrl;
        }

        return $"[PIC_DESC: {fileName}]";
    }

    /// <summary>
    /// 获取数据库和缓存统计信息。
    /// </summary>
    public (int DbCount, int CacheFileCount, long CacheSizeBytes) GetStats()
    {
        var dbCount = _db.Queryable<PicDescription>().Count();
        var cacheFileCount = Directory.Exists(_cacheDir) ? Directory.GetFiles(_cacheDir).Length : 0;
        var cacheSize = GetCacheDirectorySize();

        return (dbCount, cacheFileCount, cacheSize);
    }

    /// <summary>
    /// 初始化时自动清理非图片记录。
    /// </summary>
    public void InitializeCleanup()
    {
        var deleted = CleanNonImageRecords();
        if (deleted > 0)
            Console.WriteLine($"[PicDesc] 已清理 {deleted} 条非图片记录（MP3 等）");
    }

    public void Dispose()
    {
        CleanCacheDirectory();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 字符串扩展方法。
/// </summary>
internal static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
