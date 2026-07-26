using Aib.Api;
using Aib.Api.Startup;
using Aib.Application;
using Aib.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        o.JsonSerializerOptions.Converters.Add(new Aib.Api.Serialization.InvoiceStatusJsonConverter());
        o.JsonSerializerOptions.Converters.Add(new Aib.Api.Serialization.NullableInvoiceStatusJsonConverter());
        o.JsonSerializerOptions.Converters.Add(new Aib.Api.Serialization.IncludeNonBillableTasksJsonConverter());
        o.JsonSerializerOptions.Converters.Add(new Aib.Api.Serialization.NullableIncludeNonBillableTasksJsonConverter());
    });
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddScoped<DataSeeder>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
