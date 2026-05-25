using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ArkPlot.Vision;
using Microsoft.Data.Sqlite;

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
    private readonly SqliteConnection _connection;
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

        // 数据库路径
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "PicDesc.db");
        var dataDir = Path.GetDirectoryName(dbPath)!;
        if (!Directory.Exists(dataDir))
            Directory.CreateDirectory(dataDir);

        // 临时图片缓存目录
        _cacheDir = Path.Combine(dataDir, "PicCache");
        if (!Directory.Exists(_cacheDir))
            Directory.CreateDirectory(_cacheDir);

        var connectionString = $"Data Source={dbPath}";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
        CREATE TABLE IF NOT EXISTS PicDescriptions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ImageUrl TEXT NOT NULL UNIQUE,
            PicDesc TEXT NOT NULL DEFAULT '',
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );
        """;
        command.ExecuteNonQuery();

        using var indexCommand = _connection.CreateCommand();
        indexCommand.CommandText = "CREATE INDEX IF NOT EXISTS idx_picdesc_url ON PicDescriptions(ImageUrl)";
        indexCommand.ExecuteNonQuery();
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
        var existing = GetPicDescFromDb(imageUrl);
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
        // 获取所有记录
        using var selectCommand = _connection.CreateCommand();
        selectCommand.CommandText = "SELECT ImageUrl FROM PicDescriptions";
        var deletedCount = 0;

        using var reader = selectCommand.ExecuteReader();
        var urlsToDelete = new List<string>();
        while (reader.Read())
        {
            var url = reader.GetString(0);
            if (!IsImageUrl(url))
                urlsToDelete.Add(url);
        }
        reader.Close();

        foreach (var url in urlsToDelete)
        {
            using var deleteCommand = _connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM PicDescriptions WHERE ImageUrl = @url";
            deleteCommand.Parameters.AddWithValue("@url", url);
            deleteCommand.ExecuteNonQuery();
            deletedCount++;
        }

        return deletedCount;
    }

    /// <summary>
    /// 批量获取或创建图片描述。
    /// 自动过滤非图片 URL（如 MP3 音频），只处理图片格式。
    /// </summary>
    public async Task<Dictionary<string, string>> GetOrCreatePicDescsAsync(System.Collections.Generic.IEnumerable<string> imageUrls)
    {
        var result = new Dictionary<string, string>();
        foreach (var url in imageUrls)
        {
            if (!IsImageUrl(url))
            {
                // 非图片 URL 跳过，返回空描述
                result[url] = "";
                continue;
            }
            result[url] = await GetOrCreatePicDescAsync(url);
        }
        return result;
    }

    /// <summary>
    /// 判断 URL 是否为图片。
    /// 支持的格式：png, jpg, jpeg, gif, webp, bmp, svg, apng, avif。
    /// </summary>
    private static bool IsImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        // 去掉查询参数后判断扩展名
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
    /// 优先使用 URL 直接调用（百炼无需下载），无 URL 客户端时下载到临时目录。
    /// 完整生命周期：描述 → 缓存 → 清理临时文件。
    /// </summary>
    private async Task<string> GenerateAndCacheAsync(string imageUrl)
    {
        string? tempFilePath = null;
        try
        {
            // 1. 生成描述
            string description;
            if (_describeByUrl != null)
            {
                // 有 URL 客户端（如百炼），直接传 URL 调用，无需下载
                description = await _describeByUrl(imageUrl);
            }
            else
            {
                // 无客户端时使用占位符
                description = GeneratePlaceholder(imageUrl);
            }

            // 2. 写入数据库缓存
            UpsertPicDesc(imageUrl, description);

            return description;
        }
        catch (Exception ex)
        {
            // 描述失败时，记录错误信息到缓存，避免下次重试
            var errorDesc = $"[DESC_ERROR: {ex.Message.Truncate(100)}]";
            UpsertPicDesc(imageUrl, errorDesc);
            return errorDesc;
        }
        finally
        {
            // 3. 无论成功或失败，都清理临时图片文件（如果有）
            if (tempFilePath != null && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // 删除失败不阻断流程
                }
            }
        }
    }

    /// <summary>
    /// 下载图片到临时缓存目录。
    /// 文件名使用 URL 的 MD5 hash，避免非法字符和冲突。
    /// </summary>
    private static async Task<string> DownloadImageAsync(string imageUrl)
    {
        // 用 URL 的 MD5 作为文件名，确保唯一且安全
        var fileName = ComputeMd5(imageUrl);
        var extension = ExtractImageExtension(imageUrl);
        var tempFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "PicCache", $"{fileName}{extension}");

        // 如果已存在（可能上次运行残留），直接复用
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

    /// <summary>
    /// 从 URL 中提取图片扩展名。
    /// 无法识别时默认使用 .png。
    /// </summary>
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
            var parts = imageUrl.Split('?')[0]; // 去掉查询参数
            var ext = Path.GetExtension(parts).ToLowerInvariant();
            return string.IsNullOrEmpty(ext) || ext == "." ? ".png" : ext;
        }
    }

    /// <summary>
    /// 计算字符串的 MD5 hash（用于生成安全的文件名）。
    /// </summary>
    private static string ComputeMd5(string input)
    {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 清理临时图片缓存目录中的所有文件。
    /// 建议在应用启动或退出时调用。
    /// </summary>
    public void CleanCacheDirectory()
    {
        if (!Directory.Exists(_cacheDir)) return;

        var files = Directory.GetFiles(_cacheDir);
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // 被占用的文件跳过
            }
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
            try
            {
                size += new FileInfo(file).Length;
            }
            catch { }
        }
        return size;
    }

    private string? GetPicDescFromDb(string imageUrl)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT PicDesc FROM PicDescriptions WHERE ImageUrl = @url";
        command.Parameters.AddWithValue("@url", imageUrl);

        var result = command.ExecuteScalar();
        return result?.ToString();
    }

    private void UpsertPicDesc(string imageUrl, string desc)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
        INSERT INTO PicDescriptions (ImageUrl, PicDesc, CreatedAt, UpdatedAt)
        VALUES (@url, @desc, @created, @updated)
        ON CONFLICT(ImageUrl) DO UPDATE SET
            PicDesc = excluded.PicDesc,
            UpdatedAt = excluded.UpdatedAt
        """;
        command.Parameters.AddWithValue("@url", imageUrl);
        command.Parameters.AddWithValue("@desc", desc);
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        command.Parameters.AddWithValue("@created", now);
        command.Parameters.AddWithValue("@updated", now);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 生成占位符图片描述（无 Ollama 客户端时的回退方案）。
    /// </summary>
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
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM PicDescriptions";
        var dbCount = Convert.ToInt32(command.ExecuteScalar());

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
        // 清理临时图片
        CleanCacheDirectory();

        _connection?.Close();
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 字符串扩展方法。
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// 截断字符串到指定长度，超出部分追加 "..."。
    /// </summary>
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
