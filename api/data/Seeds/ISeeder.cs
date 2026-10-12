namespace api.Data.Seeds;

public interface ISeeder
{
    Task SeedAsync(ApiDbContext dbContext, CancellationToken cancellationToken = default);
}
