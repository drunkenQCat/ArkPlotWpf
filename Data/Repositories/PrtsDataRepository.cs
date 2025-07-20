using ArkPlotWpf.Data.Entities;
using ArkPlotWpf.Model;
using ArkPlotWpf.Data.Mappers;
using ArkPlotWpf.Data.Validation;
using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;

namespace ArkPlotWpf.Data.Repositories;

public class PrtsDataRepository : BaseRepository, IPrtsDataRepository
{

    public PrtsDataRepository(string? connectionString = null) : base(connectionString)
    {
        // 执行数据库迁移
        DatabaseMigration.Migrate(_connectionString);
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        // 数据库迁移系统已经处理了表创建和索引
    }

    public long AddPrtsData(PrtsData prtsData)
    {
        // 数据验证
        DataValidator.ValidatePrtsData(prtsData);
        
        var entity = prtsData.ToEntity();
        var sql = """
        INSERT INTO PrtsData (Tag, DataJson)
        VALUES (@Tag, @DataJson);
        SELECT last_insert_rowid();
        """;

        return ExecuteScalar<long>(sql, entity);
    }

    public void AddOrUpdatePrtsData(PrtsData prtsData)
    {
        // 数据验证
        DataValidator.ValidatePrtsData(prtsData);
        
        var entity = prtsData.ToEntity();

        // 尝试更新，如果不存在则插入
        var updateSql = """
        UPDATE PrtsData 
        SET DataJson = @DataJson
        WHERE Tag = @Tag
        """;

        var rowsAffected = Execute(updateSql, entity);
        if (rowsAffected == 0)
        {
            // 如果更新失败，说明记录不存在，执行插入
            var insertSql = """
            INSERT INTO PrtsData (Tag, DataJson)
            VALUES (@Tag, @DataJson)
            """;
            Execute(insertSql, entity);
        }
    }

    public PrtsData? GetPrtsDataByTag(string tag)
    {
        return GetByTag(tag);
    }

    public PrtsData? GetByTag(string tag)
    {
        var entity = QuerySingleOrDefault<PrtsDataEntity>(
            "SELECT * FROM PrtsData WHERE Tag = @Tag",
            new { Tag = tag });

        return entity?.ToModel();
    }

    public List<PrtsData> GetAllPrtsData()
    {
        var entities = Query<PrtsDataEntity>("SELECT * FROM PrtsData").AsList();
        return entities.Select(e => e.ToModel()).ToList();
    }

    public bool UpdatePrtsData(PrtsData prtsData)
    {
        // 数据验证
        DataValidator.ValidatePrtsData(prtsData);
        
        var entity = prtsData.ToEntity();
        var sql = """
        UPDATE PrtsData 
        SET DataJson = @DataJson
        WHERE Tag = @Tag
        """;

        var rowsAffected = Execute(sql, entity);
        return rowsAffected > 0;
    }

    public bool DeletePrtsData(string tag)
    {
        return DeleteByTag(tag);
    }

    public PrtsData? GetById(long id)
    {
        var entity = QuerySingleOrDefault<PrtsDataEntity>(
            "SELECT * FROM PrtsData WHERE Id = @Id",
            new { Id = id });

        return entity?.ToModel();
    }

    public IEnumerable<PrtsData> GetAll()
    {
        return GetAllPrtsData();
    }

    public long Add(PrtsData entity)
    {
        return AddPrtsData(entity);
    }

    public bool Update(PrtsData entity)
    {
        return UpdatePrtsData(entity);
    }

    public bool Delete(long id)
    {
        var sql = "DELETE FROM PrtsData WHERE Id = @Id";
        var rowsAffected = Execute(sql, new { Id = id });
        return rowsAffected > 0;
    }

    public void AddOrUpdate(PrtsData entity)
    {
        AddOrUpdatePrtsData(entity);
    }

    public bool DeleteByTag(string tag)
    {
        var sql = "DELETE FROM PrtsData WHERE Tag = @Tag";
        var rowsAffected = Execute(sql, new { Tag = tag });
        return rowsAffected > 0;
    }

    public bool Exists(string tag)
    {
        var count = ExecuteScalar<int>(
            "SELECT COUNT(*) FROM PrtsData WHERE Tag = @Tag",
            new { Tag = tag });

        return count > 0;
    }
}
