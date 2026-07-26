using System.Data;
using Aib.Domain;
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
        SqlMapper.AddTypeHandler(new InvoiceStatusHandler());
        SqlMapper.AddTypeHandler(new IncludeNonBillableTasksHandler());
    }
}

public sealed class InvoiceStatusHandler : SqlMapper.TypeHandler<InvoiceStatus>
{
    public override void SetValue(IDbDataParameter parameter, InvoiceStatus value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.Value;
    }

    public override InvoiceStatus Parse(object value) =>
        InvoiceStatus.Parse(value?.ToString());
}

public sealed class IncludeNonBillableTasksHandler : SqlMapper.TypeHandler<IncludeNonBillableTasks>
{
    public override void SetValue(IDbDataParameter parameter, IncludeNonBillableTasks value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.Value;
    }

    public override IncludeNonBillableTasks Parse(object value) =>
        IncludeNonBillableTasks.Parse(value?.ToString());
}
