using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saas.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBillingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.CreateTable(
                name: "billing_adjustments",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folio = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delta_mxn = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_adjustments", x => x.id);
                    table.CheckConstraint("ck_billing_adjustments_nonzero", "delta_mxn <> 0");
                });

            migrationBuilder.CreateTable(
                name: "charge_sheets",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    total_mxn = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    reviewed_by_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    reviewed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charge_sheets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "specialist_cuts",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folio = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialist_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    period_start_gmt6 = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end_gmt6 = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    efectivo_mxn = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    tarjeta_mxn = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    transferencia_mxn = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_mxn = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    payment_count = table.Column<int>(type: "integer", nullable: false),
                    prepared_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prepared_by_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    prepared_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_by_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    confirmed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_observation_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specialist_cuts", x => x.id);
                    table.CheckConstraint("ck_specialist_cuts_valid_period", "period_start_gmt6 <= period_end_gmt6");
                });

            migrationBuilder.CreateTable(
                name: "charge_line_items",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_sheet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concept_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price_mxn = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charge_line_items", x => x.id);
                    table.CheckConstraint("ck_charge_line_items_price_nonneg", "unit_price_mxn >= 0");
                    table.CheckConstraint("ck_charge_line_items_qty_pos", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_charge_line_items_charge_sheets_charge_sheet_id",
                        column: x => x.charge_sheet_id,
                        principalSchema: "billing",
                        principalTable: "charge_sheets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "charge_sheet_audit_events",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_sheet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charge_sheet_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_charge_sheet_audit_events_charge_sheets_charge_sheet_id",
                        column: x => x.charge_sheet_id,
                        principalSchema: "billing",
                        principalTable: "charge_sheets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_payments",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folio = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_sheet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_mxn = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    method = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    paid_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    collected_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collected_by_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    cut_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_payments", x => x.id);
                    table.CheckConstraint("ck_patient_payments_amount_pos", "amount_mxn > 0");
                    table.ForeignKey(
                        name: "FK_patient_payments_charge_sheets_charge_sheet_id",
                        column: x => x.charge_sheet_id,
                        principalSchema: "billing",
                        principalTable: "charge_sheets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_patient_payments_specialist_cuts_cut_id",
                        column: x => x.cut_id,
                        principalSchema: "billing",
                        principalTable: "specialist_cuts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "specialist_cut_events",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialist_cut_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    actor_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specialist_cut_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_specialist_cut_events_specialist_cuts_specialist_cut_id",
                        column: x => x.specialist_cut_id,
                        principalSchema: "billing",
                        principalTable: "specialist_cuts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_billing_adjustments_target",
                schema: "billing",
                table: "billing_adjustments",
                columns: new[] { "tenant_id", "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ux_billing_adjustments_tenant_folio",
                schema: "billing",
                table: "billing_adjustments",
                columns: new[] { "tenant_id", "folio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_charge_line_items_sheet_order",
                schema: "billing",
                table: "charge_line_items",
                columns: new[] { "charge_sheet_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_charge_sheet_audit_sheet_time",
                schema: "billing",
                table: "charge_sheet_audit_events",
                columns: new[] { "charge_sheet_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_charge_sheets_tenant_patient",
                schema: "billing",
                table: "charge_sheets",
                columns: new[] { "tenant_id", "patient_id" });

            migrationBuilder.CreateIndex(
                name: "ix_charge_sheets_tenant_specialist_status",
                schema: "billing",
                table: "charge_sheets",
                columns: new[] { "tenant_id", "specialist_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_charge_sheets_tenant_encounter",
                schema: "billing",
                table: "charge_sheets",
                columns: new[] { "tenant_id", "encounter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_payments_cut",
                schema: "billing",
                table: "patient_payments",
                column: "cut_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_payments_tenant_spec_paid",
                schema: "billing",
                table: "patient_payments",
                columns: new[] { "tenant_id", "specialist_id", "paid_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_patient_payments_charge_sheet",
                schema: "billing",
                table: "patient_payments",
                column: "charge_sheet_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_patient_payments_tenant_folio",
                schema: "billing",
                table: "patient_payments",
                columns: new[] { "tenant_id", "folio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_specialist_cut_events_cut_time",
                schema: "billing",
                table: "specialist_cut_events",
                columns: new[] { "specialist_cut_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_specialist_cuts_tenant_spec_status",
                schema: "billing",
                table: "specialist_cuts",
                columns: new[] { "tenant_id", "specialist_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_specialist_cuts_tenant_folio",
                schema: "billing",
                table: "specialist_cuts",
                columns: new[] { "tenant_id", "folio" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_adjustments",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "charge_line_items",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "charge_sheet_audit_events",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "patient_payments",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "specialist_cut_events",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "charge_sheets",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "specialist_cuts",
                schema: "billing");
        }
    }
}
