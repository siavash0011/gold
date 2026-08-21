using System;

namespace GoldShop.Models
{
    public class ClientTransaction
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public Client Client { get; set; } = null!;

        // "Buy"   → customer buys from us (we sell) → stock decreases
        // "Sell"  → customer sells to us (we buy)  → stock increases
        // "Adjust"→ manual inventory correction
        public string Type { get; set; } = "";
        public bool IncludeInProfit { get; set; } = true;

        public decimal Weight { get; set; }
        public decimal CostPerGram { get; set; }
        public decimal SellerPercentage { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime Date { get; set; }
        public string Note { get; set; } = "";
    }
}