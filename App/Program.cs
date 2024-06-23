using Bogus;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace ConsoleApp1;

internal class Program
{
    static void Main(string[] args)
    {
        const string connectionString = "Host=postgres;Port=5432;Database=postgres;Username=postgres;Password=postgres;Include Error Detail=True";

        // Configure DbContext
        var optionsBuilder = new DbContextOptionsBuilder<UserProfilesContext>();
        optionsBuilder.UseNpgsql(new NpgsqlDataSourceBuilder(connectionString)
                                .EnableDynamicJson()
                                .Build()).UseSnakeCaseNamingConvention();
        
        using var dbContext = new UserProfilesContext(optionsBuilder.Options);

        dbContext.Database.EnsureCreated();
        Console.WriteLine("Database created.");
        
        var usersCount = dbContext.Users.Count();
        if (usersCount == 0)
        {
            Console.WriteLine("Generate 1,000,000 user profiles.");
            var profiles = GenerateUserProfiles(1000000);
            var profilesExtended = profiles.Select(o => new UserExtended(o)).ToList();

            Console.WriteLine("Inserting profiles");
            dbContext.Users.AddRange(profilesExtended);
            dbContext.SaveChanges();
        }
        Console.WriteLine("User profiles inserted successfully.");
    }

    static List<User> GenerateUserProfiles(int count)
    {
        // Create an instance of Faker<User>
        var faker = new Faker<User>()
            .RuleFor(u => u.Id, f => f.IndexGlobal + 1)
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.FirstName, u.LastName))
            .RuleFor(u => u.Gender, f => f.Person.Gender.ToString())
            .RuleFor(u => u.IpAddress, f => f.Internet.Ip())
            .RuleFor(u => u.IsActive, f => f.Random.Bool())
            .RuleFor(u => u.Birthdate, f => f.Date.Past(30).ToUniversalTime())
            .RuleFor(u => u.Score, f => f.Random.Number(0, 100))
            .RuleFor(u => u.UniqueId, f => f.Random.Guid())
            .RuleFor(u => u.Created, f => f.Date.Past().ToUniversalTime())
            .RuleFor(u => u.Rank, f => f.Random.Decimal(0, 100))
            .FinishWith((f, u) =>
            {
                u.Tags = f.Make(3, () => f.Commerce.Department()).ToList();
            });

        
      return faker.Generate(count);
    }
}


public class UserProfilesContext : DbContext
{
    public DbSet<UserExtended> Users { get; set; }

    public UserProfilesContext(DbContextOptions<UserProfilesContext> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserExtended>().ToTable("users");
        modelBuilder.Entity<UserExtended>().Property(u => u.Id).HasColumnName("id");
        modelBuilder.Entity<UserExtended>().Property(u => u.UserJsonData).HasColumnName("user_json_data").HasColumnType("jsonb").HasColumnOrder(99);

    }
}