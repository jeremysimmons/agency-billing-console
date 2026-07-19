using System.Data;
using Dapper;

namespace Aib.Infrastructure.Persistence;

/// <summary>Stores enum values as their string names and maps snake_case columns.</summary>
public sealed class EnumStringHandler<T> : SqlMapper.TypeHandler<T> where T : struct, Enum
{
    public override void SetValue(IDbDataParameter parameter, T value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString();
    }

    public override T Parse(object value) =>
        value is string s ? Enum.Parse<T>(s, ignoreCase: true) : (T)value;
}

public static class DapperConfig
{
    private static bool _configured;

    public static void Configure()
    {
        if (_configured) return;
        _configured = true;

        DefaultTypeMap.MatchNamesWithUnderscores = true;

        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.UserStatus>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.AccessLevel>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.AuthMethod>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.WorkStatus>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.BillingStatus>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.BillingType>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.RollupMode>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.BillingRollupMode>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.ClientStatus>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.ProjectStatus>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.MagicLinkPurpose>());
    }
}
