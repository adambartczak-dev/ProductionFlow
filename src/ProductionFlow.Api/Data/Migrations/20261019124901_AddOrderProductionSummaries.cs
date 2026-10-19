using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductionFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderProductionSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderProductionSummaries",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PlannedQuantity = table.Column<int>(type: "integer", nullable: false),
                    GoodQuantity = table.Column<int>(type: "integer", nullable: false),
                    ScrapQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReportsCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderProductionSummaries", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_OrderProductionSummaries_ProductionOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderProductionSummaries");
        }
    }
}
