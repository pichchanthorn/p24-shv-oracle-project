using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "APP_USERS",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Username = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "NVARCHAR2(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    IsTwoFactorEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TwoFactorSecret = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    FailedLoginCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    IsActive = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APP_USERS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AUTH_AUDIT_LOGS",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    UserId = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    Username = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    EventType = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Success = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IpAddress = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    Details = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUTH_AUDIT_LOGS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "NVARCHAR2(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_APP_USERS_Email",
                table: "APP_USERS",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_APP_USERS_Username",
                table: "APP_USERS",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "APP_USERS");

            migrationBuilder.DropTable(
                name: "AUTH_AUDIT_LOGS");

            migrationBuilder.DropTable(
                name: "Students");
        }
    }
}
