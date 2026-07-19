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

/// <summary>Maps <see cref="DateOnly"/> to a PostgreSQL <c>date</c> (Dapper has no built-in support).</summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateOnly d => d,
        DateTime dt => DateOnly.FromDateTime(dt),
        _ => DateOnly.FromDateTime(Convert.ToDateTime(value))
    };
}

public static class DapperConfig
{
    private static bool _configured;

    public static void Configure()
    {
        if (_configured) return;
        _configured = true;

        DefaultTypeMap.MatchNamesWithUnderscores = true;

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

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
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.ExternalConnectionStatus>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.ContainerType>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.WorkItemType>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.ImportType>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.ImportStatus>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.ExternalEntityType>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.ImportAction>());
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.ImportRecordStatus>());
    }
}
