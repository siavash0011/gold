using System;
using System.Windows;
using GoldShop.Models;

namespace GoldShop.Pages
{
    public partial class AdjustInventoryDialog : Window
    {
        private int _clientId;
        private decimal _currentStock;

        public AdjustInventoryDialog(int clientId, decimal currentStock)
        {
            InitializeComponent();
            _clientId = clientId;
            _currentStock = currentStock;
            CurrentStockText.Text = $"{currentStock:N3} گرم";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(NewStockBox.Text, out decimal newStock) || newStock < 0)
            {
                MessageBox.Show("عدد معتبر وارد کنید.");
                return;
            }

            decimal diff = newStock - _currentStock;
            if (diff == 0)
            {
                DialogResult = false;
                return;
            }

            using (var db = new AppDbContext())
            {
                var transaction = new ClientTransaction
                {
                    ClientId = _clientId,
                    Type = "Adjust",
                    Weight = diff, // positive or negative
                    CostPerGram = 0,
                    SellerPercentage = 0,
                    TotalAmount = 0,
                    Date = DateTime.Now,
                    Note = $"اصلاح موجودی از {_currentStock:N3} به {newStock:N3}"
                };
                db.ClientTransactions.Add(transaction);
                db.SaveChanges();
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}