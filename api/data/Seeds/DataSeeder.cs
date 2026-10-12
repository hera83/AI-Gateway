namespace api.Data.Seeds;

public static class DataSeeder
{
    // Runs on every startup, after migrations, in every environment. Add new seeders here.
    private static readonly ISeeder[] Seeders = [];

    public static async Task SeedAsync(ApiDbContext dbContext, CancellationToken cancellationToken = default)
    {
        foreach (var seeder in Seeders)
        {
            await seeder.SeedAsync(dbContext, cancellationToken);
        }
    }
}
