using Microsoft.EntityFrameworkCore;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Infrastructure.Tests.Persistence;

internal static class PersistenceTestData
{
    internal static Task InsertLegacyUser(AppDbContext db,User u)=>db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO users(id,identification_number,full_name,university_id,career,member_type,card_code,status,created_at,updated_at) VALUES ({u.Id},{u.IdentificationNumber.Value},{u.FullName},{u.UniversityId},{u.Career},{u.MemberType.ToString()},{u.CardCode.Value},{u.Status.ToString()},{u.CreatedAt},{u.UpdatedAt})");
    internal static Task InsertLegacyVehicle(AppDbContext db,Vehicle v)=>db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO vehicles(id,type,plate,frame_number,brand,model,color,status,created_at,updated_at) VALUES ({v.Id},{v.Type.ToString()},{v.Plate!.Value},{(string?)null},{v.Brand},{v.Model},{v.Color},{v.Status.ToString()},{v.CreatedAt},{v.UpdatedAt})");
    internal static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    internal static User User(string? identification = null, string? card = null) => new(
        new IdentificationNumber(identification ?? Guid.NewGuid().ToString("N")), "Usuario de prueba", UniversityParking.Domain.Universities.UniversityIds.Etitc, null,
        MemberType.STAFF, new CardCode(card ?? Guid.NewGuid().ToString("N")), Now);

    internal static Vehicle Vehicle(VehicleType type = VehicleType.MOTORCYCLE, string? identifier = null) => new(type,
        type == VehicleType.BICYCLE ? null : new VehiclePlate(identifier ?? Guid.NewGuid().ToString("N")[..20]),
        type == VehicleType.BICYCLE ? new FrameNumber(identifier ?? Guid.NewGuid().ToString("N")) : null,
        "Marca", "Modelo", "Negro", Now);

    internal static AcademicPeriod Period(string name = "2026-2", bool active = false)
    {
        var period = new AcademicPeriod(name, new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 20), Now);
        if (active) period.Activate();
        return period;
    }

    internal static async Task<(User User, User Guard, Vehicle Vehicle, ParkingLot Lot, ParkingZone Zone)> ParkingAsync(AppDbContext context)
    {
        var user = User();
        var guard = User();
        var vehicle = Vehicle();
        var lot = new ParkingLot("Principal", "Kennedy", new TimeOnly(6, 0), new TimeOnly(22, 0), Now);
        var zone = new ParkingZone(lot.Id, "Zona de motos", VehicleType.MOTORCYCLE, Now);
        context.AddRange(user, guard, vehicle, lot, zone);
        await context.SaveChangesAsync();
        return (user, guard, vehicle, lot, zone);
    }

    internal static ParkingMovement Movement((User User, User Guard, Vehicle Vehicle, ParkingLot Lot, ParkingZone Zone) data,
        Guid? userId = null, Guid? vehicleId = null) => new(userId ?? data.User.Id, vehicleId ?? data.Vehicle.Id,
            data.Lot.Id, data.Zone.Id, Now, data.Guard.Id);
}
