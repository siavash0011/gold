using System.Collections.Generic;

namespace GoldShop.Models
{
    public class Client
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Note { get; set; } = "";

        public ICollection<ClientTransaction> Transactions { get; set; }
            = new List<ClientTransaction>();
    }
}