using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityParking.Infrastructure.Persistence.Migrations;

public partial class AddStudentRegistrationStatuses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("ck_users_status", "users");
        migrationBuilder.AddCheckConstraint("ck_users_status", "users",
            "status IN ('ACTIVE', 'PENDING', 'INACTIVE', 'REJECTED')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reject an unsafe downgrade instead of remapping or deleting registration data.
        migrationBuilder.Sql("""
            DO $migration$
            BEGIN
                IF EXISTS (SELECT 1 FROM users WHERE status NOT IN ('ACTIVE', 'INACTIVE')) THEN
                    RAISE EXCEPTION 'No se puede revertir: existen usuarios PENDING o REJECTED.';
                END IF;
            END;
            $migration$;
            """);
        migrationBuilder.DropCheckConstraint("ck_users_status", "users");
        migrationBuilder.AddCheckConstraint("ck_users_status", "users", "status IN ('ACTIVE', 'INACTIVE')");
    }
}
