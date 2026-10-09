using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityParking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserVehicleManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_vehicles_frame_number",
                table: "vehicles");

            migrationBuilder.DropIndex(
                name: "ux_vehicles_plate",
                table: "vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicles_identifier",
                table: "vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicles_type",
                table: "vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_verification_images_type",
                table: "vehicle_verification_images");

            migrationBuilder.DropCheckConstraint(
                name: "ck_parking_zones_type",
                table: "parking_zones");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "vehicles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identification_type",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "normalized_email",
                table: "users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "user_type",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "STUDENT");

            migrationBuilder.Sql("""
                UPDATE users u SET user_type = CASE
                    WHEN EXISTS (SELECT 1 FROM user_roles ur JOIN roles r ON r.id=ur.role_id WHERE ur.user_id=u.id AND r.code='ADMIN') THEN 'ADMINISTRATIVE'
                    WHEN EXISTS (SELECT 1 FROM user_roles ur JOIN roles r ON r.id=ur.role_id WHERE ur.user_id=u.id AND r.code='GUARD') THEN 'GUARD'
                    ELSE 'STUDENT' END;
                """);
            migrationBuilder.CreateTable(
                name: "password_challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_challenges", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_challenges_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

migrationBuilder.CreateTable(
                name: "pending_file_deletions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_file_deletions", x => x.id);
                    table.ForeignKey(
                        name: "FK_pending_file_deletions_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_vehicles_frame_number",
                table: "vehicles",
                column: "frame_number",
                unique: true,
                filter: "frame_number IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_vehicles_plate",
                table: "vehicles",
                column: "plate",
                unique: true,
                filter: "plate IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicles_identifier",
                table: "vehicles",
                sql: "(type IN ('CAR', 'MOTORCYCLE') AND plate IS NOT NULL AND frame_number IS NULL) OR (type IN ('BICYCLE', 'SCOOTER') AND plate IS NULL AND frame_number IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicles_type",
                table: "vehicles",
                sql: "type IN ('CAR', 'MOTORCYCLE', 'BICYCLE', 'SCOOTER')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_verification_images_type",
                table: "vehicle_verification_images",
                sql: "type IN ('TRANSIT_LICENSE_FRONT', 'BICYCLE_PHOTO', 'SCOOTER_PHOTO')");

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true,
                filter: "normalized_email IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_institutional_type",
                table: "users",
                sql: "user_type IN ('STUDENT','TEACHER','ADMINISTRATIVE','GUARD')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_normalized_email",
                table: "users",
                sql: "normalized_email IS NULL OR normalized_email = lower(btrim(normalized_email))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_parking_zones_type",
                table: "parking_zones",
                sql: "vehicle_type IN ('CAR', 'MOTORCYCLE', 'BICYCLE', 'SCOOTER')");

            migrationBuilder.CreateIndex(
                name: "IX_password_challenges_user_id_purpose_created_at",
                table: "password_challenges",
                columns: new[] { "user_id", "purpose", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_pending_file_deletions_completed_at",
                table: "pending_file_deletions",
                column: "completed_at");

            migrationBuilder.CreateIndex(
                name: "IX_pending_file_deletions_vehicle_id",
                table: "pending_file_deletions",
                column: "vehicle_id");
            migrationBuilder.Sql("""
                INSERT INTO parking_zones(id,parking_lot_id,name,vehicle_type,status,created_at,updated_at)
                SELECT gen_random_uuid(), p.id, 'Zona de scooters','SCOOTER','ACTIVE',now(),now()
                FROM parking_lots p WHERE NOT EXISTS(SELECT 1 FROM parking_zones z WHERE z.parking_lot_id=p.id AND z.vehicle_type='SCOOTER');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM users WHERE email IS NOT NULL OR phone_number IS NOT NULL OR identification_type IS NOT NULL OR must_change_password OR user_type='TEACHER')
                       OR EXISTS(SELECT 1 FROM vehicles WHERE deleted_at IS NOT NULL OR type='SCOOTER')
                       OR EXISTS(SELECT 1 FROM password_challenges) OR EXISTS(SELECT 1 FROM pending_file_deletions)
                       OR EXISTS(SELECT 1 FROM vehicle_verification_images WHERE type='SCOOTER_PHOTO') THEN
                        RAISE EXCEPTION 'Downgrade blocked: preserve user and vehicle feature data';
                    END IF;
                    DELETE FROM parking_zones WHERE vehicle_type='SCOOTER';
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "password_challenges");

            migrationBuilder.DropTable(
                name: "pending_file_deletions");

            migrationBuilder.DropIndex(
                name: "ux_vehicles_frame_number",
                table: "vehicles");

            migrationBuilder.DropIndex(
                name: "ux_vehicles_plate",
                table: "vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicles_identifier",
                table: "vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicles_type",
                table: "vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_verification_images_type",
                table: "vehicle_verification_images");

            migrationBuilder.DropIndex(
                name: "ux_users_normalized_email",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_institutional_type",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_normalized_email",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_parking_zones_type",
                table: "parking_zones");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "email",
                table: "users");

            migrationBuilder.DropColumn(
                name: "identification_type",
                table: "users");

            migrationBuilder.DropColumn(
                name: "must_change_password",
                table: "users");

            migrationBuilder.DropColumn(
                name: "normalized_email",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "users");

            migrationBuilder.DropColumn(
                name: "user_type",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ux_vehicles_frame_number",
                table: "vehicles",
                column: "frame_number",
                unique: true,
                filter: "frame_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_vehicles_plate",
                table: "vehicles",
                column: "plate",
                unique: true,
                filter: "plate IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicles_identifier",
                table: "vehicles",
                sql: "(type IN ('CAR', 'MOTORCYCLE') AND plate IS NOT NULL AND frame_number IS NULL) OR (type = 'BICYCLE' AND plate IS NULL AND frame_number IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicles_type",
                table: "vehicles",
                sql: "type IN ('CAR', 'MOTORCYCLE', 'BICYCLE')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_verification_images_type",
                table: "vehicle_verification_images",
                sql: "type IN ('TRANSIT_LICENSE_FRONT', 'BICYCLE_PHOTO')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_parking_zones_type",
                table: "parking_zones",
                sql: "vehicle_type IN ('CAR', 'MOTORCYCLE', 'BICYCLE')");
        }
    }
}
