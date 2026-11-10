using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace GoldShop.Models
{
    public class AppDbContext : DbContext
    {
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<ClientTransaction> ClientTransactions { get; set; }
        public DbSet<GoldStock> GoldStocks { get; set; }
        public DbSet<SecondHandGoldTransaction> SecondHandGoldTransactions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Persistent folder: C:\Users\<User>\AppData\Roaming\GoldShop\
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GoldShop");

            // Create folder if it doesn't exist
            Directory.CreateDirectory(folder);

            string dbPath = Path.Combine(folder, "GoldShop.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        public void InitializeDatabase()
        {
            Database.EnsureCreated();
        }
    }
}