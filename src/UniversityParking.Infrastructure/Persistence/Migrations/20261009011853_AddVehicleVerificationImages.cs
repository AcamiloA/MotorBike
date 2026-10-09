using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityParking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleVerificationImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicle_verification_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_verification_images", x => x.id);
                    table.CheckConstraint("ck_vehicle_verification_images_mime", "content_type IN ('image/jpeg', 'image/png')");
                    table.CheckConstraint("ck_vehicle_verification_images_size", "size_bytes > 0 AND size_bytes <= 5242880");
                    table.CheckConstraint("ck_vehicle_verification_images_type", "type IN ('TRANSIT_LICENSE_FRONT', 'BICYCLE_PHOTO')");
                    table.ForeignKey(
                        name: "FK_vehicle_verification_images_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_vehicle_verification_images_vehicle",
                table: "vehicle_verification_images",
                column: "vehicle_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $migration$
                BEGIN
                    IF EXISTS (SELECT 1 FROM vehicle_verification_images) THEN
                        RAISE EXCEPTION 'No se puede revertir: existen evidencias de verificación.';
                    END IF;
                END;
                $migration$;
                """);
            migrationBuilder.DropTable(
                name: "vehicle_verification_images");
        }
    }
}
