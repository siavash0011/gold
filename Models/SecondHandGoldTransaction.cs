using System;

namespace GoldShop.Models
{
    public class SecondHandGoldTransaction
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";        // "Buy" or "Sell"
        public string CustomerName { get; set; } = ""; // 👈 NEW
        public string PhoneNumber { get; set; } = "";  // 👈 NEW
        public decimal Weight { get; set; }           // actual weight
        public int Purity { get; set; }
        public decimal CostPerGram { get; set; }
        public decimal SellerPercentage { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Equivalent750Weight { get; set; }  // converted weight
        public DateTime Date { get; set; }
        public string Note { get; set; } = "";
    }
}