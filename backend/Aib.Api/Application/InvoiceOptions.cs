namespace Aib.Application;

public sealed class InvoiceOptions
{
    public const string SectionName = "Invoice";

    public decimal DefaultRate { get; set; } = 70;
}
