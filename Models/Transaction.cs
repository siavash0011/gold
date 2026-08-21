using System;

namespace GoldShop.Models
{
    public class Transaction
    {
        public bool IncludeInProfit { get; set; } = true;

        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int Purity { get; set; } // 750, 849, or 999
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }  
        public decimal Weight { get; set; }
        public decimal CostPerGram { get; set; }
        public decimal MakingPercentage { get; set; }
        public decimal SellerPercentage { get; set; }
        public decimal TaxPercentage { get; set; }
        public decimal MakingCost { get; set; }
        public decimal SellerCost { get; set; }
        public decimal Tax { get; set; }
        public decimal FinalCost { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Note { get; set; }

    }
}