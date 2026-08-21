using System.Windows;
using GoldShop.Models;

namespace GoldShop.Pages
{
    public partial class ClientEditDialog : Window
    {
        public Client Client { get; private set; } = new Client();

        public ClientEditDialog(Client? existingClient = null)
        {
            InitializeComponent();

            if (existingClient != null)
            {
                Client = existingClient;
                NameBox.Text = existingClient.Name;
                PhoneBox.Text = existingClient.Phone;
                Title = "ویرایش مشتری";
            }
            else
            {
                Title = "افزودن مشتری";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("نام مشتری را وارد کنید.");
                return;
            }

            Client.Name = NameBox.Text.Trim();
            Client.Phone = PhoneBox.Text.Trim();
            // Note remains unchanged when editing; for new clients it remains empty.
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}