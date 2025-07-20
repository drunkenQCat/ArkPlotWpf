using System;
using System.Runtime.Serialization;

namespace ArkPlotWpf.Data.Exceptions;

/// <summary>
/// 数据库操作异常基类
/// </summary>
[Serializable]
public class DatabaseException : Exception
{
    public DatabaseException() : base() { }
    
    public DatabaseException(string message) : base(message) { }
    
    public DatabaseException(string message, Exception innerException) 
        : base(message, innerException) { }
    
    protected DatabaseException(SerializationInfo info, StreamingContext context) 
        : base(info, context) { }
}

/// <summary>
/// 数据库连接异常
/// </summary>
[Serializable]
public class DatabaseConnectionException : DatabaseException
{
    public string ConnectionString { get; }

    public DatabaseConnectionException(string connectionString, string message = "数据库连接失败") 
        : base(message)
    {
        ConnectionString = connectionString;
    }
    
    public DatabaseConnectionException(string connectionString, Exception innerException) 
        : base("数据库连接失败", innerException)
    {
        ConnectionString = connectionString;
    }
    
    protected DatabaseConnectionException(SerializationInfo info, StreamingContext context) 
        : base(info, context)
    {
        ConnectionString = info.GetString(nameof(ConnectionString)) ?? string.Empty;
    }
    
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ConnectionString), ConnectionString);
    }
}

/// <summary>
/// 数据库查询异常
/// </summary>
[Serializable]
public class DatabaseQueryException : DatabaseException
{
    public string Sql { get; }
    public object? Parameters { get; }

    public DatabaseQueryException(string sql, object? parameters, string message = "数据库查询失败") 
        : base(message)
    {
        Sql = sql;
        Parameters = parameters;
    }
    
    public DatabaseQueryException(string sql, object? parameters, Exception innerException) 
        : base("数据库查询失败", innerException)
    {
        Sql = sql;
        Parameters = parameters;
    }
    
    protected DatabaseQueryException(SerializationInfo info, StreamingContext context) 
        : base(info, context)
    {
        Sql = info.GetString(nameof(Sql)) ?? string.Empty;
        Parameters = info.GetValue(nameof(Parameters), typeof(object));
    }
    
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(Sql), Sql);
        info.AddValue(nameof(Parameters), Parameters);
    }
}

/// <summary>
/// 数据库事务异常
/// </summary>
[Serializable]
public class DatabaseTransactionException : DatabaseException
{
    public DatabaseTransactionException(string message = "数据库事务失败") : base(message) { }
    
    public DatabaseTransactionException(Exception innerException) 
        : base("数据库事务失败", innerException) { }
    
    protected DatabaseTransactionException(SerializationInfo info, StreamingContext context) 
        : base(info, context) { }
}

/// <summary>
/// 数据验证异常
/// </summary>
[Serializable]
public class DataValidationException : DatabaseException
{
    public string PropertyName { get; }
    public object? InvalidValue { get; }

    public DataValidationException(string propertyName, object? invalidValue, string message) 
        : base(message)
    {
        PropertyName = propertyName;
        InvalidValue = invalidValue;
    }
    
    protected DataValidationException(SerializationInfo info, StreamingContext context) 
        : base(info, context)
    {
        PropertyName = info.GetString(nameof(PropertyName)) ?? string.Empty;
        InvalidValue = info.GetValue(nameof(InvalidValue), typeof(object));
    }
    
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(PropertyName), PropertyName);
        info.AddValue(nameof(InvalidValue), InvalidValue);
    }
}

/// <summary>
/// 数据不存在异常
/// </summary>
[Serializable]
public class DataNotFoundException : DatabaseException
{
    public string EntityType { get; }
    public object? Identifier { get; }

    public DataNotFoundException(string entityType, object? identifier, string message = "数据不存在") 
        : base(message)
    {
        EntityType = entityType;
        Identifier = identifier;
    }
    
    protected DataNotFoundException(SerializationInfo info, StreamingContext context) 
        : base(info, context)
    {
        EntityType = info.GetString(nameof(EntityType)) ?? string.Empty;
        Identifier = info.GetValue(nameof(Identifier), typeof(object));
    }
    
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(EntityType), EntityType);
        info.AddValue(nameof(Identifier), Identifier);
    }
} 