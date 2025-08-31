using System.Threading.Tasks;
using SqlSugar;
using ArkPlot.Core.Model;

namespace ArkPlot.Core.Data.Repositories;

/// <summary>
/// FormattedTextEntry 仓储类，提供 FormattedTextEntry 实体的特定业务操作
/// </summary>
public class FormattedTextEntryRepository : BaseRepository<FormattedTextEntry>
{
    public FormattedTextEntryRepository(SqlSugarClient? db = null) : base(db)
    {
    }

    #region 业务特定方法

    /// <summary>
    /// 根据类型查询 FormattedTextEntry
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>匹配的 FormattedTextEntry 列表</returns>
    public List<FormattedTextEntry> GetByType(string type) =>
        GetWhere(x => x.Type == type);

    /// <summary>
    /// 根据角色名称查询 FormattedTextEntry
    /// </summary>
    /// <param name="characterName">角色名称</param>
    /// <returns>匹配的 FormattedTextEntry 列表</returns>
    public List<FormattedTextEntry> GetByCharacterName(string characterName) =>
        GetWhere(x => x.CharacterName == characterName);

    /// <summary>
    /// 根据原始文本查询 FormattedTextEntry
    /// </summary>
    /// <param name="originalText">原始文本</param>
    /// <returns>匹配的 FormattedTextEntry</returns>
    public FormattedTextEntry GetByOriginalText(string originalText) =>
        FirstOrDefault(x => x.OriginalText == originalText);

    /// <summary>
    /// 根据索引查询 FormattedTextEntry
    /// </summary>
    /// <param name="index">索引</param>
    /// <returns>匹配的 FormattedTextEntry</returns>
    public FormattedTextEntry GetByIndex(int index) =>
        FirstOrDefault(x => x.Index == index);

    /// <summary>
    /// 获取所有角色名称
    /// </summary>
    /// <returns>角色名称列表</returns>
    public List<string> GetAllCharacterNames() =>
        _db.Queryable<FormattedTextEntry>()
           .Where(x => !string.IsNullOrEmpty(x.CharacterName))
           .Select(x => x.CharacterName)
           .Distinct()
           .ToList();

    /// <summary>
    /// 获取所有类型
    /// </summary>
    /// <returns>类型列表</returns>
    public List<string> GetAllTypes() =>
        _db.Queryable<FormattedTextEntry>()
           .Where(x => !string.IsNullOrEmpty(x.Type))
           .Select(x => x.Type)
           .Distinct()
           .ToList();

    /// <summary>
    /// 根据索引范围查询 FormattedTextEntry
    /// </summary>
    /// <param name="startIndex">开始索引</param>
    /// <param name="endIndex">结束索引</param>
    /// <returns>匹配的 FormattedTextEntry 列表</returns>
    public List<FormattedTextEntry> GetByIndexRange(int startIndex, int endIndex) =>
        GetWhere(x => x.Index >= startIndex && x.Index <= endIndex);

    /// <summary>
    /// 更新角色名称
    /// </summary>
    /// <param name="id">FormattedTextEntry ID</param>
    /// <param name="characterName">新角色名称</param>
    /// <returns>是否更新成功</returns>
    public bool UpdateCharacterName(long id, string characterName) =>
        Update(x => new FormattedTextEntry { CharacterName = characterName }, x => x.Id == id);

    /// <summary>
    /// 更新对话内容
    /// </summary>
    /// <param name="id">FormattedTextEntry ID</param>
    /// <param name="dialog">新对话内容</param>
    /// <returns>是否更新成功</returns>
    public bool UpdateDialog(long id, string dialog) =>
        Update(x => new FormattedTextEntry { Dialog = dialog }, x => x.Id == id);

    /// <summary>
    /// 获取包含特定标签的 FormattedTextEntry
    /// </summary>
    /// <param name="tag">标签</param>
    /// <returns>匹配的 FormattedTextEntry 列表</returns>
    public List<FormattedTextEntry> GetByTag(string tag) =>
        GetWhere(x => x.CommandSet.ContainsKey(tag));

    #endregion

    #region 异步业务方法

    /// <summary>
    /// 异步根据类型查询 FormattedTextEntry
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>匹配的 FormattedTextEntry 列表</returns>
    public async Task<List<FormattedTextEntry>> GetByTypeAsync(string type) =>
        await GetWhereAsync(x => x.Type == type);

    /// <summary>
    /// 异步根据角色名称查询 FormattedTextEntry
    /// </summary>
    /// <param name="characterName">角色名称</param>
    /// <returns>匹配的 FormattedTextEntry 列表</returns>
    public async Task<List<FormattedTextEntry>> GetByCharacterNameAsync(string characterName) =>
        await GetWhereAsync(x => x.CharacterName == characterName);

    /// <summary>
    /// 异步根据原始文本查询 FormattedTextEntry
    /// </summary>
    /// <param name="originalText">原始文本</param>
    /// <returns>匹配的 FormattedTextEntry</returns>
    public async Task<FormattedTextEntry> GetByOriginalTextAsync(string originalText) =>
        await FirstOrDefaultAsync(x => x.OriginalText == originalText);

    /// <summary>
    /// 异步根据索引查询 FormattedTextEntry
    /// </summary>
    /// <param name="index">索引</param>
    /// <returns>匹配的 FormattedTextEntry</returns>
    public async Task<FormattedTextEntry> GetByIndexAsync(int index) =>
        await FirstOrDefaultAsync(x => x.Index == index);

    /// <summary>
    /// 异步获取所有角色名称
    /// </summary>
    /// <returns>角色名称列表</returns>
    public async Task<List<string>> GetAllCharacterNamesAsync() =>
        await _db.Queryable<FormattedTextEntry>()
                 .Where(x => !string.IsNullOrEmpty(x.CharacterName))
                 .Select(x => x.CharacterName)
                 .Distinct()
                 .ToListAsync();

    /// <summary>
    /// 异步获取所有类型
    /// </summary>
    /// <returns>类型列表</returns>
    public async Task<List<string>> GetAllTypesAsync() =>
        await _db.Queryable<FormattedTextEntry>()
                 .Where(x => !string.IsNullOrEmpty(x.Type))
                 .Select(x => x.Type)
                 .Distinct()
                 .ToListAsync();

    /// <summary>
    /// 异步根据索引范围查询 FormattedTextEntry
    /// </summary>
    /// <param name="startIndex">开始索引</param>
    /// <param name="endIndex">结束索引</param>
    /// <returns>匹配的 FormattedTextEntry 列表</returns>
    public async Task<List<FormattedTextEntry>> GetByIndexRangeAsync(int startIndex, int endIndex) =>
        await GetWhereAsync(x => x.Index >= startIndex && x.Index <= endIndex);

    /// <summary>
    /// 异步更新角色名称
    /// </summary>
    /// <param name="id">FormattedTextEntry ID</param>
    /// <param name="characterName">新角色名称</param>
    /// <returns>是否更新成功</returns>
    public async Task<bool> UpdateCharacterNameAsync(long id, string characterName) =>
        await UpdateAsync(x => new FormattedTextEntry { CharacterName = characterName }, x => x.Id == id);

    /// <summary>
    /// 异步更新对话内容
    /// </summary>
    /// <param name="id">FormattedTextEntry ID</param>
    /// <param name="dialog">新对话内容</param>
    /// <returns>是否更新成功</returns>
    public async Task<bool> UpdateDialogAsync(long id, string dialog) =>
        await UpdateAsync(x => new FormattedTextEntry { Dialog = dialog }, x => x.Id == id);

    #endregion
}
