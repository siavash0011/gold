using System;
using System.Windows;

namespace GoldShop.Pages
{
    public partial class AdjustGoldStockDialog : Window
    {
        public decimal AdjustmentAmount { get; private set; }
        public bool IsIncrease { get; private set; }

        public AdjustGoldStockDialog(decimal currentStock)
        {
            InitializeComponent();
            CurrentStockText.Text = $"{currentStock:N3} گرم";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(AmountBox.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("لطفاً یک مقدار مثبت وارد کنید.");
                return;
            }

            AdjustmentAmount = amount;
            IsIncrease = OperationCombo.SelectedIndex == 0;  // 0 = increase, 1 = decrease

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}