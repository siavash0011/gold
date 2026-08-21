using System;

namespace GoldShop.Models
{
    public class UnifiedTransaction
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string TypeDisplay { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Phone { get; set; } = "";   
        public decimal Profit { get; set; }
        public string DateDisplay { get; set; } = ""; 

        public decimal Weight { get; set; }
        public decimal TotalAmount { get; set; }
        public string Note { get; set; } = "";
    }
}