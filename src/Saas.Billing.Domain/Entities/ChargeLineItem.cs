namespace Saas.Billing.Domain.Entities;

public sealed class ChargeLineItem
{
    private ChargeLineItem()
    {
    }

    internal ChargeLineItem(
        Guid id,
        Guid chargeSheetId,
        int sortOrder,
        string conceptCode,
        string description,
        string category,
        int quantity,
        decimal unitPriceMxn)
    {
        Id = id;
        ChargeSheetId = chargeSheetId;
        SortOrder = sortOrder;
        ConceptCode = conceptCode;
        Description = description;
        Category = category;
        Quantity = quantity;
        UnitPriceMxn = unitPriceMxn;
    }

    public Guid Id { get; private set; }
    public Guid ChargeSheetId { get; private set; }
    public int SortOrder { get; private set; }
    public string ConceptCode { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPriceMxn { get; private set; }
}
