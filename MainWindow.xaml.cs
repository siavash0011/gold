using System.Windows;
using System.Windows.Controls;
using GoldShop.Pages;

namespace GoldShop
{
    public partial class MainWindow : Window
    {

        private Button? _selectedButton;

        public MainWindow()
        {
            using (var db = new GoldShop.Models.AppDbContext())
            {
                db.InitializeDatabase();
            }

            InitializeComponent();
            MainContent.Content = new SellPage();
            SelectButton(BtnBuySell);
        }

        // ==========================================
        // HIGHLIGHT THE ACTIVE BUTTON (Goldish)
        // ==========================================
        private void SelectButton(Button selected)
        {
            if (_selectedButton != null)
            {
                _selectedButton.Background = System.Windows.Media.Brushes.Transparent;
                _selectedButton.Foreground = System.Windows.Media.Brushes.Black;
                _selectedButton.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D4AF37")
                );
                _selectedButton.BorderThickness = new Thickness(1);
            }

            selected.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D4AF37")
            );
            selected.Foreground = System.Windows.Media.Brushes.White;
            selected.BorderThickness = new Thickness(0);

            _selectedButton = selected;
        }

        // ==========================================
        // MENU BUTTONS
        // ==========================================

        private void BuySell_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new SellPage();
            SelectButton(BtnBuySell);
        }


     
        private void Clients_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new ClientsPage();
        }
        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new DashboardPage();
            SelectButton(BtnDashboard);
        }

    }
}