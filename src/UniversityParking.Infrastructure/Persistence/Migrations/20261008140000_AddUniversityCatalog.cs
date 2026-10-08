using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityParking.Infrastructure.Persistence.Migrations;

// Unreleased migration: reference data and legacy-user transition are one transaction.
public partial class AddUniversityCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "universities",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_universities", x => x.id);
                table.CheckConstraint("ck_universities_code", "code <> '' AND code = upper(btrim(code))");
                table.CheckConstraint("ck_universities_name", "btrim(name) <> ''");
                table.CheckConstraint("ck_universities_dates", "updated_at >= created_at");
            });
        migrationBuilder.CreateIndex("ux_universities_code", "universities", "code", unique: true);
        var created = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        migrationBuilder.InsertData("universities",
            new[] { "id", "code", "name", "is_active", "created_at", "updated_at" },
            new object[,]
            {
                { new Guid("a1100000-0000-4000-8000-000000000001"), "ETITC", "ETITC", true, created, created },
                { new Guid("a1100000-0000-4000-8000-000000000002"), "CMC", "Colegio Mayor de Cundinamarca", true, created, created },
                { new Guid("a1100000-0000-4000-8000-000000000003"), "UPN", "U. Pedagógica", true, created, created }
            });
        migrationBuilder.AddColumn<Guid>("university_id", "users", type: "uuid", nullable: true);
        migrationBuilder.Sql("""
            UPDATE users AS u
            SET university_id = i.id
            FROM universities AS i
            WHERE lower(btrim(u.university)) = lower(i.name);
            DO $migration$
            BEGIN
                IF EXISTS (SELECT 1 FROM users WHERE university_id IS NULL) THEN
                    RAISE EXCEPTION 'University legacy no reconocida: revise los valores antes de migrar; no se asigna una universidad por defecto.';
                END IF;
            END;
            $migration$;
            """);
        migrationBuilder.AlterColumn<Guid>("university_id", "users", type: "uuid", nullable: false,
            oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.CreateIndex("ix_users_university_id", "users", "university_id");
        migrationBuilder.AddForeignKey("FK_users_universities_university_id", "users", "university_id",
            "universities", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.DropColumn("university", "users");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("university", "users", type: "character varying(200)", maxLength: 200, nullable: true);
        migrationBuilder.Sql("UPDATE users AS u SET university = i.name FROM universities AS i WHERE u.university_id = i.id;");
        migrationBuilder.AlterColumn<string>("university", "users", type: "character varying(200)", maxLength: 200,
            nullable: false, oldClrType: typeof(string), oldType: "character varying(200)", oldMaxLength: 200, oldNullable: true);
        migrationBuilder.DropForeignKey("FK_users_universities_university_id", "users");
        migrationBuilder.DropIndex("ix_users_university_id", "users");
        migrationBuilder.DropColumn("university_id", "users");
        migrationBuilder.DropTable("universities");
    }
}
