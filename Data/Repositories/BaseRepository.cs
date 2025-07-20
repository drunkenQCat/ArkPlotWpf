using Microsoft.Data.Sqlite;
using Dapper;
using System;
using System.Data;
using System.IO;
using ArkPlotWpf.Data.Exceptions;

namespace ArkPlotWpf.Data.Repositories;

/// <summary>
/// 基础Repository类，提供公共的数据库操作逻辑
/// </summary>
public abstract class BaseRepository
{
    protected readonly string _connectionString;

    protected BaseRepository(string? connectionString = null)
    {
        _connectionString = connectionString ?? GetDefaultConnectionString();
    }

    /// <summary>
    /// 获取默认连接字符串
    /// </summary>
    /// <returns>默认数据库连接字符串</returns>
    protected virtual string GetDefaultConnectionString()
    {
        var rootPath = AppDomain.CurrentDomain.BaseDirectory;
        var dataDir = Path.Combine(rootPath, "Data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        var dbPath = Path.Combine(dataDir, "PlotData.db");
        return $"Data Source={dbPath}";
    }

    /// <summary>
    /// 创建数据库连接
    /// </summary>
    /// <returns>数据库连接</returns>
    protected virtual IDbConnection CreateConnection()
    {
        try
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }
        catch (Exception ex)
        {
            throw new DatabaseConnectionException(_connectionString, ex);
        }
    }

    /// <summary>
    /// 执行数据库操作，自动处理连接生命周期
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="operation">数据库操作</param>
    /// <returns>操作结果</returns>
    protected virtual T ExecuteWithConnection<T>(Func<IDbConnection, T> operation)
    {
        try
        {
            using var connection = CreateConnection();
            return operation(connection);
        }
        catch (DatabaseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseException("数据库操作失败", ex);
        }
    }

    /// <summary>
    /// 执行数据库操作，自动处理连接生命周期（无返回值）
    /// </summary>
    /// <param name="operation">数据库操作</param>
    protected virtual void ExecuteWithConnection(Action<IDbConnection> operation)
    {
        try
        {
            using var connection = CreateConnection();
            operation(connection);
        }
        catch (DatabaseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseException("数据库操作失败", ex);
        }
    }

    /// <summary>
    /// 执行查询并返回单个结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <returns>查询结果</returns>
    protected virtual T? QuerySingleOrDefault<T>(string sql, object? parameters = null)
    {
        try
        {
            return ExecuteWithConnection(connection => 
                connection.QueryFirstOrDefault<T>(sql, parameters));
        }
        catch (DatabaseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseQueryException(sql, parameters, ex);
        }
    }

    /// <summary>
    /// 执行查询并返回多个结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <returns>查询结果列表</returns>
    protected virtual IEnumerable<T> Query<T>(string sql, object? parameters = null)
    {
        try
        {
            return ExecuteWithConnection(connection => 
                connection.Query<T>(sql, parameters));
        }
        catch (DatabaseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseQueryException(sql, parameters, ex);
        }
    }

    /// <summary>
    /// 执行非查询SQL语句
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <returns>受影响的行数</returns>
    protected virtual int Execute(string sql, object? parameters = null)
    {
        try
        {
            return ExecuteWithConnection(connection => 
                connection.Execute(sql, parameters));
        }
        catch (DatabaseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseQueryException(sql, parameters, ex);
        }
    }

    /// <summary>
    /// 执行标量查询
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <returns>标量结果</returns>
    protected virtual T ExecuteScalar<T>(string sql, object? parameters = null)
    {
        try
        {
            return ExecuteWithConnection(connection => 
                connection.ExecuteScalar<T>(sql, parameters));
        }
        catch (DatabaseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseQueryException(sql, parameters, ex);
        }
    }

    /// <summary>
    /// 检查表是否存在
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <returns>表是否存在</returns>
    protected virtual bool TableExists(string tableName)
    {
        var sql = @"
            SELECT COUNT(*) 
            FROM sqlite_master 
            WHERE type='table' AND name=@TableName";
        
        var count = ExecuteScalar<int>(sql, new { TableName = tableName });
        return count > 0;
    }

    /// <summary>
    /// 获取表的记录数
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <returns>记录数</returns>
    protected virtual int GetTableCount(string tableName)
    {
        var sql = $"SELECT COUNT(*) FROM {tableName}";
        return ExecuteScalar<int>(sql);
    }

    /// <summary>
    /// 开始事务
    /// </summary>
    /// <returns>数据库事务</returns>
    protected virtual IDbTransaction BeginTransaction()
    {
        var connection = CreateConnection();
        return connection.BeginTransaction();
    }

    /// <summary>
    /// 执行事务操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="operation">事务操作</param>
    /// <returns>操作结果</returns>
    protected virtual T ExecuteWithTransaction<T>(Func<IDbTransaction, T> operation)
    {
        try
        {
            using var connection = CreateConnection();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                var result = operation(transaction);
                transaction.Commit();
                return result;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (DatabaseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseTransactionException(ex);
        }
    }

    /// <summary>
    /// 执行事务操作（无返回值）
    /// </summary>
    /// <param name="operation">事务操作</param>
    protected virtual void ExecuteWithTransaction(Action<IDbTransaction> operation)
    {
        try
        {
            using var connection = CreateConnection();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                operation(transaction);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (DatabaseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseTransactionException(ex);
        }
    }
} 