using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThorntonEnergy.Metering.Api.Data;
using ThorntonEnergy.Metering.Api.Dtos;
using ThorntonEnergy.Metering.Api.Mapping;
using ThorntonEnergy.Metering.Api.Models;
using ThorntonEnergy.Metering.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Common;
using ThorntonEnergy.Shared.Data;

namespace ThorntonEnergy.Metering.Api.Tests;

// Meter 1 already has one reading: 1000 on 2026-01-01
public class MeterServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private MeterService CreateService()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        return new MeterService(
            new EfRepository<MeteringDbContext, Meter>(_db.Context), new EfRepository<MeteringDbContext, MeterReading>(_db.Context),
            new Mock<ICurrentUser>().Object, mapper, NullLogger<MeterService>.Instance);
    }

    [Fact]
    public async Task NewReading_LaterAndHigher_IsSaved()
    {
        // Act
        var result = await CreateService().CreateReadingAsync(new CreateMeterReadingRequest(1, new DateOnly(2026, 2, 1), 1250));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1250, result.Value!.ReadingValue);
    }

    [Fact]
    public async Task NewReading_SameValueOnALaterDate_IsAllowed()
    {
        // Arrange: "not lower" means equal is fine (nobody used any power)
        // Act
        var result = await CreateService().CreateReadingAsync(new CreateMeterReadingRequest(1, new DateOnly(2026, 2, 1), 1000));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task NewReading_LowerThanThePreviousOne_IsInvalid()
    {
        // Act
        var result = await CreateService().CreateReadingAsync(new CreateMeterReadingRequest(1, new DateOnly(2026, 2, 1), 999));

        // Assert
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains("lower", result.Error);
    }

    [Theory]
    [InlineData("2026-01-01")] // same day as the previous reading
    [InlineData("2025-12-01")] // before the previous reading
    public async Task NewReading_NotLaterThanThePreviousOne_IsInvalid(string date)
    {
        // Act
        var result = await CreateService().CreateReadingAsync(new CreateMeterReadingRequest(1, DateOnly.Parse(date), 2000));

        // Assert
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains("after", result.Error);
    }

    [Fact]
    public async Task NewReading_ForAnUnknownMeter_IsInvalid()
    {
        // Act
        var result = await CreateService().CreateReadingAsync(new CreateMeterReadingRequest(99, new DateOnly(2026, 2, 1), 5));

        // Assert
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task FirstReadingForAMeter_IsAlwaysAccepted()
    {
        // Arrange: a brand new meter with no readings yet
        _db.Context.Meters.Add(new Meter { Id = 2, CustomerId = 2, MeterNumber = "M-2", InstallationDate = new DateOnly(2026, 1, 1), Status = MeterStatus.Active });
        _db.Context.SaveChanges();

        // Act
        var result = await CreateService().CreateReadingAsync(new CreateMeterReadingRequest(2, new DateOnly(2026, 2, 1), 5));

        // Assert
        Assert.True(result.IsSuccess);
    }
}
