using System.Data;
using Dapper;

namespace Aib.Infrastructure.Persistence;

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
        SqlMapper.AddTypeHandler(new EnumStringHandler<Domain.ClientStatus>());
    }
}
