using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GoldShop.Models;

namespace GoldShop.Pages
{
    public partial class ClientSelectDialog : Window
    {
        public Client? SelectedClient { get; private set; }
        public event EventHandler<int>? ClientDeleted;

        public ClientSelectDialog()
        {
            InitializeComponent();
            LoadClients();
        }

        private void LoadClients()
        {
            using (var db = new AppDbContext())
            {
                var clients = db.Clients.ToList();
                ClientsGrid.ItemsSource = clients.Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Phone,
                    CurrentStock = GetClientStock(c.Id)
                }).ToList();
            }
        }

        private decimal GetClientStock(int clientId)
        {
            using (var db = new AppDbContext())
            {
                return db.ClientTransactions
                    .Where(t => t.ClientId == clientId)
                    .Sum(t => t.Type == "Buy" ? t.Weight :
                              t.Type == "Sell" ? -t.Weight :
                              t.Type == "Adjust" ? t.Weight : 0);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string search = SearchBox.Text.Trim();
            using (var db = new AppDbContext())
            {
                var clients = db.Clients
                    .Where(c => c.Name.Contains(search) || c.Phone.Contains(search))
                    .ToList();
                ClientsGrid.ItemsSource = clients.Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Phone,
                    CurrentStock = GetClientStock(c.Id)
                }).ToList();
            }
        }

        private void AddClient_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ClientEditDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                using (var db = new AppDbContext())
                {
                    db.Clients.Add(dialog.Client);
                    db.SaveChanges();
                }
                LoadClients();
            }
        }

        private void EditClient_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsGrid.SelectedItem == null) return;
            dynamic selected = ClientsGrid.SelectedItem;
            int clientId = selected.Id;

            using (var db = new AppDbContext())
            {
                var client = db.Clients.Find(clientId);
                if (client != null)
                {
                    var dialog = new ClientEditDialog(client);
                    dialog.Owner = this;
                    if (dialog.ShowDialog() == true)
                    {
                        db.Clients.Update(dialog.Client);
                        db.SaveChanges();
                        LoadClients();
                    }
                }
            }
        }

        private void DeleteClient_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsGrid.SelectedItem == null) return;
            dynamic selected = ClientsGrid.SelectedItem;
            int clientId = selected.Id;

            var result = MessageBox.Show("آیا مطمئن هستید؟", "حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            using (var db = new AppDbContext())
            {
                var transactions = db.ClientTransactions.Where(t => t.ClientId == clientId);
                db.ClientTransactions.RemoveRange(transactions);
                db.Clients.Remove(db.Clients.Find(clientId)!);
                db.SaveChanges();
            }

            ClientDeleted?.Invoke(this, clientId);
            LoadClients();
        }

        // ==================== SEED DATA ====================
        private async void SeedData_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "آیا مطمئن هستید که 1000 مشتری و 200 تراکنش برای هر مشتری ایجاد شود؟\nاین عمل ممکن است کمی طول بکشد.",
                "تأیید ساخت داده",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var random = new Random();
                var now = DateTime.Now;
                var startDate = now.AddYears(-5);

                using (var db = new AppDbContext())
                {
                    int existingClients = db.Clients.Count();
                    int clientsToAdd = 1000 - existingClients;

                    if (clientsToAdd > 0)
                    {
                        var firstNames = new[]
                        {
                            "علی", "محمد", "رضا", "حسین", "مهدی", "سارا", "زهرا", "مریم", "نگار", "فاطمه",
                            "امیر", "نازنین", "پارسا", "آیدا", "کیانوش", "شیرین", "بهرام", "نرگس", "سامان", "لیلا",
                            "آرش", "پریسا", "مزدک", "نیکی", "رهام", "ترانه", "کاوه", "غزل", "سهراب", "مانا",
                            "آرمین", "رها", "پیمان", "نازلی", "سام", "دلارام", "بردیا", "نیکا", "آوا", "سورنا"
                        };
                        var lastNames = new[]
                        {
                            "رضایی", "حسینی", "کریمی", "موسوی", "احمدی", "محمدی", "صادقی", "قاسمی", "عباسی", "نوری",
                            "تهرانی", "شریفی", "نادری", "رحیمی", "اکبری", "فرهادی", "کاظمی", "یوسفی", "جعفری", "مرادی",
                            "قربانی", "صالحی", "زارعی", "رستمی", "عظیمی", "خسروی", "فرزانه"
                        };

                        var usedPhones = new HashSet<string>();
                        var batchClients = new List<Client>(clientsToAdd);

                        for (int i = 0; i < clientsToAdd; i++)
                        {
                            string phone;
                            do
                            {
                                phone = "09" + random.Next(100000000, 999999999).ToString();
                            } while (!usedPhones.Add(phone));

                            batchClients.Add(new Client
                            {
                                Name = $"{firstNames[random.Next(firstNames.Length)]} {lastNames[random.Next(lastNames.Length)]}",
                                Phone = phone
                            });
                        }

                        db.Clients.AddRange(batchClients);
                        db.SaveChanges();
                    }

                    var clients = db.Clients.ToList();
                    var notes = new[] { "", "پرداخت نقدی", "چک", "مشتری دائمی", "تحویل فوری", "پرداخت کارت" };
                    var allTransactions = new List<ClientTransaction>(clients.Count * 200);

                    foreach (var client in clients)
                    {
                        for (int i = 0; i < 200; i++)
                        {
                            var date = startDate.AddDays(random.Next(0, (now - startDate).Days))
                                               .AddHours(random.Next(0, 24))
                                               .AddMinutes(random.Next(0, 60))
                                               .AddSeconds(random.Next(0, 60));

                            int typeRoll = random.Next(0, 100);
                            string type;
                            decimal weight;
                            decimal cost = Math.Round((decimal)(random.NextDouble() * 500000 + 100000), 0);
                            decimal percent = Math.Round((decimal)(random.Next(0, 15)), 2);

                            if (typeRoll < 45) { type = "Buy"; weight = Math.Round((decimal)(random.NextDouble() * 50 + 0.1), 3); }
                            else if (typeRoll < 90) { type = "Sell"; weight = Math.Round((decimal)(random.NextDouble() * 50 + 0.1), 3); }
                            else { type = "Adjust"; weight = Math.Round((decimal)(random.NextDouble() * 20 - 10), 3); cost = 0; percent = 0; }

                            decimal total = type == "Buy"
                                ? weight * (cost + (percent / 100m) * cost)
                                : type == "Sell" ? weight * (cost - (percent / 100m) * cost) : 0;

                            allTransactions.Add(new ClientTransaction
                            {
                                ClientId = client.Id,
                                Type = type,
                                Weight = weight,
                                CostPerGram = cost,
                                SellerPercentage = percent,
                                TotalAmount = total,
                                Date = date,
                                Note = notes[random.Next(notes.Length)],
                                IncludeInProfit = true
                            });
                        }
                    }

                    int chunkSize = 5000;
                    for (int i = 0; i < allTransactions.Count; i += chunkSize)
                    {
                        var chunk = allTransactions.Skip(i).Take(chunkSize).ToList();
                        db.ClientTransactions.AddRange(chunk);
                        await db.SaveChangesAsync();
                    }
                }

                LoadClients();
                MessageBox.Show("✅ 1000 مشتری و 200 تراکنش برای هر مشتری ایجاد شد.", "موفق",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ خطا: {ex.Message}\n\n{ex.InnerException?.Message}", "خطا",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsGrid.SelectedItem == null)
            {
                MessageBox.Show("لطفاً یک مشتری انتخاب کنید.");
                return;
            }

            dynamic selected = ClientsGrid.SelectedItem;
            int clientId = selected.Id;

            using (var db = new AppDbContext())
            {
                SelectedClient = db.Clients.Find(clientId);
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}