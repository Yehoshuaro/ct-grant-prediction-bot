namespace GrantAI.Infrastructure.Configuration;

/// <summary>MongoDB connection configuration, bound from the "Mongo" section.</summary>
public sealed class MongoDbSettings
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "grantai";
}

/// <summary>Redis connection configuration, bound from the "Redis" section.</summary>
public sealed class RedisSettings
{
    public const string SectionName = "Redis";

    /// <summary>
    /// StackExchange.Redis connection string. <c>abortConnect=false</c> lets the
    /// app start even if Redis is momentarily unavailable and reconnect later.
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379,abortConnect=false";
}
