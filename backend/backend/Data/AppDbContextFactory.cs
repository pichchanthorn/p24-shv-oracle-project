using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Oracle.EntityFrameworkCore.Infrastructure;

namespace backend.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<AppDbContextFactory>(optional: true)
                .Build();

            var connectionString = configuration.GetConnectionString("OracleDb");

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            optionsBuilder.UseOracle(
                connectionString,
                oracleOptions => oracleOptions.UseOracleSQLCompatibility(
                    OracleSQLCompatibility.DatabaseVersion21));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}