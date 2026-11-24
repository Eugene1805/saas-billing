using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Billing.Domain.Entities;

namespace Saas.Billing.Infrastructure.Persistence.Configurations;

internal sealed class ChargeLineItemConfiguration : IEntityTypeConfiguration<ChargeLineItem>
{
    public void Configure(EntityTypeBuilder<ChargeLineItem> builder)
    {
        builder.ToTable("charge_line_items", t =>
        {
            t.HasCheckConstraint("ck_charge_line_items_qty_pos", "quantity > 0");
            t.HasCheckConstraint("ck_charge_line_items_price_nonneg", "unit_price_mxn >= 0");
        });
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(i => i.ChargeSheetId).HasColumnName("charge_sheet_id").IsRequired();
        builder.Property(i => i.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(i => i.ConceptCode).HasColumnName("concept_code").HasMaxLength(32).IsRequired();
        builder.Property(i => i.Description).HasColumnName("description").HasMaxLength(200).IsRequired();
        builder.Property(i => i.Category).HasColumnName("category").HasMaxLength(32).IsRequired();
        builder.Property(i => i.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(i => i.UnitPriceMxn).HasColumnName("unit_price_mxn").HasPrecision(12, 2).IsRequired();

        builder.HasIndex(i => new { i.ChargeSheetId, i.SortOrder })
            .HasDatabaseName("ix_charge_line_items_sheet_order");
    }
}
