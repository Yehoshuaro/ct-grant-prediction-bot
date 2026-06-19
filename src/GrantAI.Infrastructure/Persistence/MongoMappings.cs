using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace GrantAI.Infrastructure.Persistence;

/// <summary>
/// Registers MongoDB class maps for the domain entities. The domain stays free
/// of persistence attributes (no <c>[BsonId]</c> etc.), so the mapping lives
/// here instead. The natural string key is mapped onto <c>_id</c> and the season
/// enum is stored as a readable string rather than an integer.
/// </summary>
public static class MongoMappings
{
    private static readonly object Gate = new();
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;
        lock (Gate)
        {
            if (_registered) return;

            BsonClassMap.RegisterClassMap<AdmissionRecord>(cm =>
            {
                cm.AutoMap();
                cm.MapIdMember(r => r.Id);
                cm.MapMember(r => r.Season).SetSerializer(new EnumSerializer<Season>(BsonType.String));
                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<ImportLog>(cm =>
            {
                cm.AutoMap();
                cm.MapIdMember(l => l.Id);
                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<ImportRowError>(cm => cm.AutoMap());

            BsonClassMap.RegisterClassMap<GrantCutoffRecord>(cm =>
            {
                cm.AutoMap();
                cm.MapIdMember(r => r.Id);
                cm.MapMember(r => r.MasterType).SetSerializer(new EnumSerializer<MasterType>(BsonType.String));
                cm.SetIgnoreExtraElements(true);
            });

            _registered = true;
        }
    }
}
