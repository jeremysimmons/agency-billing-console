using System.Text.Json;
using System.Text.Json.Serialization;
using Aib.Domain;

namespace Aib.Api.Serialization;

public sealed class InvoiceStatusJsonConverter : JsonConverter<InvoiceStatus>
{
    public override InvoiceStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Null => null,
            _ => throw new JsonException("Invoice status must be a string."),
        };
        if (!InvoiceStatus.TryParse(value, out var status) && !string.IsNullOrWhiteSpace(value))
            throw new JsonException($"Unknown invoice status '{value}'.");
        return status;
    }

    public override void Write(Utf8JsonWriter writer, InvoiceStatus value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

public sealed class NullableInvoiceStatusJsonConverter : JsonConverter<InvoiceStatus?>
{
    public override InvoiceStatus? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!InvoiceStatus.TryParse(value, out var status))
            throw new JsonException($"Unknown invoice status '{value}'.");
        return status;
    }

    public override void Write(Utf8JsonWriter writer, InvoiceStatus? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value);
    }
}

public sealed class IncludeNonBillableTasksJsonConverter : JsonConverter<IncludeNonBillableTasks>
{
    public override IncludeNonBillableTasks Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Null => null,
            _ => throw new JsonException("Include non-billable tasks must be a string."),
        };
        if (!IncludeNonBillableTasks.TryParse(value, out var mode) && !string.IsNullOrWhiteSpace(value))
            throw new JsonException($"Unknown include non-billable tasks value '{value}'.");
        return mode;
    }

    public override void Write(Utf8JsonWriter writer, IncludeNonBillableTasks value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

public sealed class NullableIncludeNonBillableTasksJsonConverter : JsonConverter<IncludeNonBillableTasks?>
{
    public override IncludeNonBillableTasks? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!IncludeNonBillableTasks.TryParse(value, out var mode))
            throw new JsonException($"Unknown include non-billable tasks value '{value}'.");
        return mode;
    }

    public override void Write(Utf8JsonWriter writer, IncludeNonBillableTasks? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value);
    }
}
