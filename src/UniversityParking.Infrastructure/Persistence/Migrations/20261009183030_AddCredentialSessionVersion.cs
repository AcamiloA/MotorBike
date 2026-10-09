using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityParking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentialSessionVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "security_stamp",
                table: "user_credentials",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM user_credentials WHERE security_stamp <> '00000000-0000-0000-0000-000000000000'::uuid) THEN
                        RAISE EXCEPTION 'Downgrade blocked: preserve credential session versions';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "security_stamp",
                table: "user_credentials");
        }
    }
}
