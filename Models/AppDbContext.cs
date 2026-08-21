using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace GoldShop.Models
{
    public class AppDbContext : DbContext
    {
        public DbSet<GoldStock> GoldStocks { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<ClientTransaction> ClientTransactions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string dbPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "GoldShop.db"
            );

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        // 👇 ADD THIS METHOD
        public void InitializeDatabase()
        {
            Database.EnsureCreated();
        }
    }
}