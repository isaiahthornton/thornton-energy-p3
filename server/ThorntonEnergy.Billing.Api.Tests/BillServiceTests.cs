using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThorntonEnergy.Billing.Api.Data;
using ThorntonEnergy.Billing.Api.Dtos;
using ThorntonEnergy.Billing.Api.Mapping;
using ThorntonEnergy.Billing.Api.Models;
using ThorntonEnergy.Billing.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Common;
using ThorntonEnergy.Shared.Data;

namespace ThorntonEnergy.Billing.Api.Tests;

// BillService with a real (in-memory SQLite) database, a fake signed-in user, a fake clock
// and a fake customers service (FakeCustomerDirectory)
public class BillServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private readonly TestDb _db = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IUtilityClock> _clock = new();

    public BillServiceTests()
    {
        _clock.Setup(c => c.Today).Returns(Today);
        _clock.Setup(c => c.Now).Returns(Today.ToDateTime(new TimeOnly(9, 0)));
    }

    public void Dispose() => _db.Dispose();

    // Signed in as a customer: may only touch their own id
    private void SignInAsCustomer(int customerId)
    {
        _user.Setup(u => u.IsAdmin).Returns(false);
        _user.Setup(u => u.CustomerId).Returns(customerId);
        _user.Setup(u => u.CanAccessCustomer(It.IsAny<int>())).Returns((int id) => id == customerId);
    }

    private void SignInAsAdmin()
    {
        _user.Setup(u => u.IsAdmin).Returns(true);
        _user.Setup(u => u.CanAccessCustomer(It.IsAny<int>())).Returns(true);
    }

    private BillService CreateService()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        return new BillService(
            new EfRepository<BillingDbContext, Bill>(_db.Context), new FakeCustomerDirectory(),
            _user.Object, _clock.Object, mapper, NullLogger<BillService>.Instance);
    }

    // ---------- paying ----------

    [Fact]
    public async Task Pay_OwnBill_MarksItPaidWithTodayAsTheDate()
    {
        // Arrange
        SignInAsCustomer(1);

        // Act: the browser also sends a (fake) paid date, which must be ignored
        var result = await CreateService().UpdateAsync(1, new BillPatch(BillStatus.Paid, new DateOnly(1999, 1, 1)));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(BillStatus.Paid, result.Value!.Status);
        Assert.Equal(Today, result.Value.PaidDate);
    }

    [Fact]
    public async Task Pay_SameBillTwice_ReturnsConflict()
    {
        // Arrange
        SignInAsCustomer(1);
        var service = CreateService();
        await service.UpdateAsync(1, new BillPatch(BillStatus.Paid, null));

        // Act
        var second = await service.UpdateAsync(1, new BillPatch(BillStatus.Paid, null));

        // Assert
        Assert.Equal(ResultStatus.Conflict, second.Status);
    }

    [Fact]
    public async Task Pay_AnotherCustomersBill_IsForbiddenAndLeavesItUnpaid()
    {
        // Arrange: Jane (customer 1) tries to pay John's bill (id 2)
        SignInAsCustomer(1);

        // Act
        var result = await CreateService().UpdateAsync(2, new BillPatch(BillStatus.Paid, null));

        // Assert
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal(BillStatus.Unpaid, _db.Context.Bills.Find(2)!.Status);
    }

    [Fact]
    public async Task Customer_CannotChangeABillToAnythingButPaid()
    {
        // Arrange
        SignInAsCustomer(1);

        // Act
        var result = await CreateService().UpdateAsync(1, new BillPatch(BillStatus.Unpaid, null));

        // Assert
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task Admin_CannotSetOverdueByHand()
    {
        // Arrange
        SignInAsAdmin();

        // Act
        var result = await CreateService().UpdateAsync(1, new BillPatch(BillStatus.Overdue, null));

        // Assert
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Update_UnknownBill_IsNotFound()
    {
        // Arrange
        SignInAsAdmin();

        // Act
        var result = await CreateService().UpdateAsync(999, new BillPatch(BillStatus.Paid, null));

        // Assert
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    // ---------- overdue ----------

    [Fact]
    public async Task GetById_UnpaidBillPastItsDueDate_ShowsAsOverdue()
    {
        // Arrange: bill 1 was due 2026-02-21 and "today" is 2026-06-15; the stored status stays Unpaid
        SignInAsCustomer(1);

        // Act
        var result = await CreateService().GetByIdAsync(1);

        // Assert
        Assert.Equal(BillStatus.Overdue, result.Value!.Status);
        Assert.Equal(BillStatus.Unpaid, _db.Context.Bills.Find(1)!.Status);
    }

    [Fact]
    public async Task GetById_UnpaidBillNotYetDue_StaysUnpaid()
    {
        // Arrange
        _db.Context.Bills.Find(1)!.DueDate = Today.AddDays(5);
        _db.Context.SaveChanges();
        SignInAsCustomer(1);

        // Act
        var result = await CreateService().GetByIdAsync(1);

        // Assert
        Assert.Equal(BillStatus.Unpaid, result.Value!.Status);
    }

    [Fact]
    public async Task GetById_AnotherCustomersBill_IsForbidden()
    {
        // Arrange
        SignInAsCustomer(1);

        // Act
        var result = await CreateService().GetByIdAsync(2);

        // Assert
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    // ---------- listing ----------

    [Fact]
    public async Task GetAll_AsCustomer_ReturnsOnlyTheirBills()
    {
        // Arrange
        SignInAsCustomer(1);
        int? scope = 1;
        _user.Setup(u => u.TryGetCustomerScope(It.IsAny<int?>(), out scope)).Returns(true);

        // Act
        var result = await CreateService().GetAllAsync(null);

        // Assert
        Assert.All(result.Value!, b => Assert.Equal(1, b.CustomerId));
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task GetAll_AskingForAnotherCustomer_IsForbidden()
    {
        // Arrange
        int? scope = null;
        _user.Setup(u => u.TryGetCustomerScope(2, out scope)).Returns(false);

        // Act
        var result = await CreateService().GetAllAsync(2);

        // Assert
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    // ---------- creating (admin) ----------

    [Fact]
    public async Task Create_PricesTheBillFromTheCustomersRatePlan()
    {
        // Arrange: plan is 0.10 per kWh + 10.00 monthly fee
        SignInAsAdmin();
        // (period June 2026, so it is not yet due on the fake "today" of 2026-06-15)
        var request = new CreateBillRequest(1, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 500);

        // Act
        var result = await CreateService().CreateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(50.00m, result.Value!.EnergyCharge);
        Assert.Equal(10.00m, result.Value.ServiceCharge);
        Assert.Equal(60.00m, result.Value.AmountDue);
        Assert.Equal(new DateOnly(2026, 7, 21), result.Value.DueDate);
        Assert.Equal(BillStatus.Unpaid, result.Value.Status);
    }

    [Fact]
    public async Task Create_SecondBillForTheSamePeriod_ReturnsConflict()
    {
        // Arrange: customer 1 already has a bill starting 2026-01-01
        SignInAsAdmin();
        var request = new CreateBillRequest(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 100);

        // Act
        var result = await CreateService().CreateAsync(request);

        // Assert
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task Create_ForAnUnknownCustomer_IsInvalid()
    {
        // Arrange
        SignInAsAdmin();

        // Act
        var result = await CreateService().CreateAsync(new CreateBillRequest(99, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), 1));

        // Assert
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Create_PeriodEndingBeforeItStarts_IsInvalid()
    {
        // Arrange
        SignInAsAdmin();

        // Act
        var result = await CreateService().CreateAsync(new CreateBillRequest(1, new DateOnly(2026, 3, 31), new DateOnly(2026, 3, 1), 1));

        // Assert
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }
}
