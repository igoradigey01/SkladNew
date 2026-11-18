namespace SkladApi.Services;

/// <summary>
/// Канонические значения AuditLog.EntityType. В URL принимаются короткие
/// английские имена (invoice/payment), в базе хранятся эти константы.
/// </summary>
public static class AuditEntityTypes
{
    public const string Invoice = "Invoice";
    public const string Payment = "Payment";
    public const string SupplierInvoice = "SupplierInvoice";
    public const string SupplierPayment = "SupplierPayment";

    public static string? Normalize(string? entityType) =>
        entityType?.Trim().ToLowerInvariant() switch
        {
            "invoice" or "накладная" => Invoice,
            "payment" or "платеж" or "платёж" => Payment,
            "supplier-invoice" or "приход" => SupplierInvoice,
            "supplier-payment" or "выплата" => SupplierPayment,
            _ => null,
        };
}

/// <summary>Действия, которые пишутся в AuditLog.</summary>
public static class AuditActions
{
    public const string Update = "Update";
}
