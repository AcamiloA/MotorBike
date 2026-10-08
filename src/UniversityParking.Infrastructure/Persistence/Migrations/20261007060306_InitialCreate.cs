using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityParking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "academic_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_periods", x => x.id);
                    table.CheckConstraint("ck_academic_periods_dates", "starts_on < ends_on");
                    table.CheckConstraint("ck_academic_periods_status", "status IN ('PLANNED', 'ACTIVE', 'CLOSED')");
                });

            migrationBuilder.CreateTable(
                name: "parking_lots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    campus = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    opening_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closing_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parking_lots", x => x.id);
                    table.CheckConstraint("ck_parking_lots_schedule", "opening_time < closing_time");
                    table.CheckConstraint("ck_parking_lots_status", "status IN ('ACTIVE', 'INACTIVE')");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identification_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    university = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    career = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    member_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    card_code = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_member_type", "member_type IN ('STUDENT', 'TEACHER', 'STAFF')");
                    table.CheckConstraint("ck_users_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.CheckConstraint("ck_users_student_career", "member_type <> 'STUDENT' OR (career IS NOT NULL AND btrim(career) <> '')");
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    plate = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    frame_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    color = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.id);
                    table.CheckConstraint("ck_vehicles_identifier", "(type IN ('CAR', 'MOTORCYCLE') AND plate IS NOT NULL AND frame_number IS NULL) OR (type = 'BICYCLE' AND plate IS NULL AND frame_number IS NOT NULL)");
                    table.CheckConstraint("ck_vehicles_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.CheckConstraint("ck_vehicles_type", "type IN ('CAR', 'MOTORCYCLE', 'BICYCLE')");
                });

            migrationBuilder.CreateTable(
                name: "parking_zones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parking_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    vehicle_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parking_zones", x => x.id);
                    table.CheckConstraint("ck_parking_zones_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.CheckConstraint("ck_parking_zones_type", "vehicle_type IN ('CAR', 'MOTORCYCLE', 'BICYCLE')");
                    table.ForeignKey(
                        name: "FK_parking_zones_parking_lots_parking_lot_id",
                        column: x => x.parking_lot_id,
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "news",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news", x => x.id);
                    table.CheckConstraint("ck_news_state", "(status = 'DRAFT' AND published_at IS NULL AND archived_at IS NULL) OR (status = 'PUBLISHED' AND published_at IS NOT NULL AND archived_at IS NULL) OR (status = 'ARCHIVED' AND archived_at IS NOT NULL)");
                    table.CheckConstraint("ck_news_status", "status IN ('DRAFT', 'PUBLISHED', 'ARCHIVED')");
                    table.ForeignKey(
                        name: "FK_news_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_credentials",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    password_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_credentials", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_credentials_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    document_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_documents", x => x.id);
                    table.CheckConstraint("ck_vehicle_documents_dates", "issued_on IS NULL OR expires_on IS NULL OR expires_on >= issued_on");
                    table.CheckConstraint("ck_vehicle_documents_size", "size_bytes > 0");
                    table.CheckConstraint("ck_vehicle_documents_type", "type IN ('VEHICLE_REGISTRATION', 'INSURANCE', 'OWNERSHIP_SUPPORT', 'OTHER')");
                    table.ForeignKey(
                        name: "FK_vehicle_documents_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_ownerships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    transfer_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_ownerships", x => x.id);
                    table.CheckConstraint("ck_vehicle_ownerships_dates", "end_at IS NULL OR end_at >= start_at");
                    table.ForeignKey(
                        name: "FK_vehicle_ownerships_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vehicle_ownerships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vehicle_ownerships_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_photos", x => x.id);
                    table.CheckConstraint("ck_vehicle_photos_size", "size_bytes > 0");
                    table.CheckConstraint("ck_vehicle_photos_type", "type = 'GENERAL'");
                    table.ForeignKey(
                        name: "FK_vehicle_photos_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_registrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_registrations", x => x.id);
                    table.CheckConstraint("ck_vehicle_registrations_cancellation", "(status = 'ACTIVE' AND cancelled_at IS NULL AND cancelled_by IS NULL AND cancel_reason IS NULL) OR (status = 'CANCELLED' AND cancelled_at IS NOT NULL AND cancel_reason IS NOT NULL)");
                    table.CheckConstraint("ck_vehicle_registrations_status", "status IN ('ACTIVE', 'CANCELLED')");
                    table.ForeignKey(
                        name: "FK_vehicle_registrations_academic_periods_academic_period_id",
                        column: x => x.academic_period_id,
                        principalTable: "academic_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vehicle_registrations_users_cancelled_by",
                        column: x => x.cancelled_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vehicle_registrations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vehicle_registrations_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parking_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parking_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parking_zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    check_in_guard_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    check_out_guard_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parking_movements", x => x.id);
                    table.CheckConstraint("ck_parking_movements_state", "(status = 'OPEN' AND check_out_at IS NULL AND check_out_guard_id IS NULL) OR (status = 'CLOSED' AND check_out_at IS NOT NULL AND check_out_guard_id IS NOT NULL AND check_out_at >= check_in_at)");
                    table.CheckConstraint("ck_parking_movements_status", "status IN ('OPEN', 'CLOSED')");
                    table.ForeignKey(
                        name: "FK_parking_movements_parking_lots_parking_lot_id",
                        column: x => x.parking_lot_id,
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parking_movements_parking_zones_parking_zone_id",
                        column: x => x.parking_zone_id,
                        principalTable: "parking_zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parking_movements_users_check_in_guard_id",
                        column: x => x.check_in_guard_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parking_movements_users_check_out_guard_id",
                        column: x => x.check_out_guard_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parking_movements_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parking_movements_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "incidents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parking_movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parking_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_by = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolution = table.Column<string>(type: "text", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidents", x => x.id);
                    table.CheckConstraint("ck_incidents_resolution", "(status = 'RESOLVED' AND resolution IS NOT NULL AND resolved_by IS NOT NULL AND resolved_at IS NOT NULL) OR (status = 'OPEN' AND resolved_by IS NULL AND resolved_at IS NULL) OR status = 'CANCELLED'");
                    table.CheckConstraint("ck_incidents_status", "status IN ('OPEN', 'RESOLVED', 'CANCELLED')");
                    table.CheckConstraint("ck_incidents_type", "type IN ('DAMAGE', 'ACCIDENT', 'SECURITY', 'DOCUMENT', 'BEHAVIOR', 'OTHER')");
                    table.ForeignKey(
                        name: "FK_incidents_parking_lots_parking_lot_id",
                        column: x => x.parking_lot_id,
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incidents_parking_movements_parking_movement_id",
                        column: x => x.parking_movement_id,
                        principalTable: "parking_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incidents_users_reported_by",
                        column: x => x.reported_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incidents_users_resolved_by",
                        column: x => x.resolved_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incidents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incidents_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "incident_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_attachments", x => x.id);
                    table.CheckConstraint("ck_incident_attachments_size", "size_bytes > 0");
                    table.ForeignKey(
                        name: "FK_incident_attachments_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_academic_periods_name",
                table: "academic_periods",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_academic_periods_single_active",
                table: "academic_periods",
                column: "status",
                unique: true,
                filter: "status = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_action",
                table: "audit_logs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_actor_user_id",
                table: "audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created_at",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_incident_attachments_incident_id",
                table: "incident_attachments",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "ix_incidents_occurred_at",
                table: "incidents",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_parking_lot_id",
                table: "incidents",
                column: "parking_lot_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_parking_movement_id",
                table: "incidents",
                column: "parking_movement_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_reported_by",
                table: "incidents",
                column: "reported_by");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_resolved_by",
                table: "incidents",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "ix_incidents_status",
                table: "incidents",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_incidents_type",
                table: "incidents",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_user_id",
                table: "incidents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_vehicle_id",
                table: "incidents",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "IX_news_created_by",
                table: "news",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_news_published_at",
                table: "news",
                column: "published_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_news_status",
                table: "news",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_parking_lots_name_campus",
                table: "parking_lots",
                columns: new[] { "name", "campus" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parking_movements_check_in",
                table: "parking_movements",
                column: "check_in_at");

            migrationBuilder.CreateIndex(
                name: "IX_parking_movements_check_in_guard_id",
                table: "parking_movements",
                column: "check_in_guard_id");

            migrationBuilder.CreateIndex(
                name: "IX_parking_movements_check_out_guard_id",
                table: "parking_movements",
                column: "check_out_guard_id");

            migrationBuilder.CreateIndex(
                name: "IX_parking_movements_parking_lot_id",
                table: "parking_movements",
                column: "parking_lot_id");

            migrationBuilder.CreateIndex(
                name: "IX_parking_movements_parking_zone_id",
                table: "parking_movements",
                column: "parking_zone_id");

            migrationBuilder.CreateIndex(
                name: "ix_parking_movements_status",
                table: "parking_movements",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_parking_movements_user_check_in",
                table: "parking_movements",
                columns: new[] { "user_id", "check_in_at" });

            migrationBuilder.CreateIndex(
                name: "ix_parking_movements_vehicle_check_in",
                table: "parking_movements",
                columns: new[] { "vehicle_id", "check_in_at" });

            migrationBuilder.CreateIndex(
                name: "ux_parking_movements_open_user",
                table: "parking_movements",
                column: "user_id",
                unique: true,
                filter: "status = 'OPEN'");

            migrationBuilder.CreateIndex(
                name: "ux_parking_movements_open_vehicle",
                table: "parking_movements",
                column: "vehicle_id",
                unique: true,
                filter: "status = 'OPEN'");

            migrationBuilder.CreateIndex(
                name: "ux_parking_zones_lot_type",
                table: "parking_zones",
                columns: new[] { "parking_lot_id", "vehicle_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_roles_code",
                table: "roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ux_users_card_code",
                table: "users",
                column: "card_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_identification_number",
                table: "users",
                column: "identification_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_documents_vehicle_id",
                table: "vehicle_documents",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_ownerships_created_by",
                table: "vehicle_ownerships",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_ownerships_user_end_at",
                table: "vehicle_ownerships",
                columns: new[] { "user_id", "end_at" });

            migrationBuilder.CreateIndex(
                name: "ux_vehicle_ownerships_current_vehicle",
                table: "vehicle_ownerships",
                column: "vehicle_id",
                unique: true,
                filter: "end_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_photos_vehicle_id",
                table: "vehicle_photos",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_registrations_academic_period_id",
                table: "vehicle_registrations",
                column: "academic_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_registrations_cancelled_by",
                table: "vehicle_registrations",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_registrations_user_id",
                table: "vehicle_registrations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_registrations_vehicle_period",
                table: "vehicle_registrations",
                columns: new[] { "vehicle_id", "academic_period_id" });

            migrationBuilder.CreateIndex(
                name: "ux_vehicle_registrations_vehicle_user_period",
                table: "vehicle_registrations",
                columns: new[] { "vehicle_id", "user_id", "academic_period_id" },
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "incident_attachments");

            migrationBuilder.DropTable(
                name: "news");

            migrationBuilder.DropTable(
                name: "user_credentials");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "vehicle_documents");

            migrationBuilder.DropTable(
                name: "vehicle_ownerships");

            migrationBuilder.DropTable(
                name: "vehicle_photos");

            migrationBuilder.DropTable(
                name: "vehicle_registrations");

            migrationBuilder.DropTable(
                name: "incidents");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "academic_periods");

            migrationBuilder.DropTable(
                name: "parking_movements");

            migrationBuilder.DropTable(
                name: "parking_zones");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "parking_lots");
        }
    }
}
