using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductionFlow.Api.Data.IdentityMigrations
{
    /// <inheritdoc />
    public partial class AssignOperatorWorkCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedWorkCenterId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AssignedWorkCenterId",
                table: "AspNetUsers",
                column: "AssignedWorkCenterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AssignedWorkCenterId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AssignedWorkCenterId",
                table: "AspNetUsers");
        }
    }
}
