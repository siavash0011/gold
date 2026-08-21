using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GoldShop.Models;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Net.Http;
using System.Text.Json;

namespace GoldShop.Pages

{
    public partial class ClientsPage : UserControl
    {
        private bool _isCostFormatting;
        private static readonly CultureInfo PersianCulture = new CultureInfo("fa-IR");
        private readonly PersianCalendar _persianCalendar = new PersianCalendar();
        private Client? _selectedClient;

        public event EventHandler? TransactionSaved;

        public ClientsPage()
        {
            InitializeComponent();
            UpdateTransactionTotal();
        }

        private decimal GetClientStock(int clientId)
        {
            using (var db = new AppDbContext())
            {
                return db.ClientTransactions
                    .Where(t => t.ClientId == clientId)
                    .Sum(t =>
                        t.Type == "Buy" ? t.Weight :
                        t.Type == "Sell" ? -t.Weight :
                        t.Type == "Adjust" ? t.Weight : 0);
            }
        }

        private void SelectClient_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ClientSelectDialog();
            dialog.Owner = Window.GetWindow(this);
            dialog.ClientDeleted += OnClientDeleted;   // subscribe

            if (dialog.ShowDialog() == true && dialog.SelectedClient != null)
            {
                _selectedClient = dialog.SelectedClient;
                ClientNameText.Text = _selectedClient.Name;
                ClientPhoneText.Text = _selectedClient.Phone;
                ClientNoteText.Text = _selectedClient.Note;
                ClientStockText.Text = $"موجودی: {GetClientStock(_selectedClient.Id):N3} گرم";
                LoadClientTransactions(_selectedClient.Id);
            }

            // After dialog closes, verify selected client still exists
            if (_selectedClient != null && !ClientExists(_selectedClient.Id))
            {
                ClearClientDashboard();
            }
        }

        private bool ClientExists(int clientId)
        {
            using (var db = new AppDbContext())
            {
                return db.Clients.Any(c => c.Id == clientId);
            }
        }

        private void OnClientDeleted(object? sender, int clientId)
        {
            if (_selectedClient != null && _selectedClient.Id == clientId)
            {
                ClearClientDashboard();
            }
        }

        private void LoadClientTransactions(int clientId)
        {
            using (var db = new AppDbContext())
            {
                var transList = db.ClientTransactions
                    .Where(t => t.ClientId == clientId)
                    .OrderBy(t => t.Date)
                    .ThenBy(t => t.Id)
                    .ToList();

                decimal running = 0;
                var result = new List<ClientTransactionDisplay>();

                foreach (var t in transList)
                {
                    decimal delta = t.Type switch
                    {
                        "Buy" => t.Weight,
                        "Sell" => -t.Weight,
                        "Adjust" => t.Weight,
                        _ => 0
                    };
                    running += delta;

                    result.Add(new ClientTransactionDisplay
                    {
                        Id = t.Id,
                        TypeDisplay = t.Type switch
                        {
                            "Buy" => "خرید مشتری",
                            "Sell" => "فروش مشتری",
                            "Adjust" => "اصلاح موجودی",
                            _ => t.Type
                        },
                        Weight = t.Weight,
                        WeightDisplay = t.Type == "Adjust" ? "" : t.Weight.ToString("N3"),
                        CostPerGram = t.CostPerGram,
                        SellerPercentage = t.SellerPercentage,
                        TotalAmount = t.TotalAmount,
                        Date = t.Date,
                        DateDisplay = ToPersianDateTime(t.Date),
                        Note = t.Note ?? "",
                        RunningStock = running
                    });
                }

                var display = result.OrderByDescending(x => x.Date).ToList();
                ClientTransactionsGrid.ItemsSource = display;
            }
        }

        private void SaveTransaction_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null)
            {
                MessageBox.Show("لطفاً یک مشتری انتخاب کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!EnsureClientExists()) return;

            decimal weight = GetNumber(TransactionWeightBox.Text);
            if (weight <= 0) { MessageBox.Show("وزن نامعتبر است."); return; }

            decimal cost = GetNumber(TransactionCostBox.Text);
            if (cost <= 0) { MessageBox.Show("نرخ نامعتبر است."); return; }

            decimal percent = GetNumber(TransactionPercentageBox.Text);
            if (percent < 0) { MessageBox.Show("درصد سود نمی‌تواند منفی باشد."); return; }

            string type = TransactionTypeCombo.SelectedIndex == 0 ? "Buy" : "Sell";

            if (type == "Sell")
            {
                decimal currentStock = GetClientStock(_selectedClient.Id);
                if (weight > currentStock)
                {
                    MessageBox.Show($"موجودی کافی نیست! موجودی فعلی: {currentStock:N3} گرم", "خطا",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            decimal total = type == "Buy"
                ? weight * (cost + (percent / 100m) * cost)
                : weight * (cost - (percent / 100m) * cost);

            var transaction = new ClientTransaction
            {
                ClientId = _selectedClient.Id,
                Type = type,
                Weight = weight,
                CostPerGram = cost,
                SellerPercentage = percent,
                TotalAmount = total,
                Date = TransactionDatePicker.SelectedDate ?? DateTime.Now,
                Note = TransactionNoteBox.Text.Trim(),
                IncludeInProfit = true
            };

            using (var db = new AppDbContext())
            {
                db.ClientTransactions.Add(transaction);
                db.SaveChanges();
            }

            ClientStockText.Text = $"موجودی: {GetClientStock(_selectedClient.Id):N3} گرم";
            LoadClientTransactions(_selectedClient.Id);

            TransactionWeightBox.Clear();
            TransactionCostBox.Clear();
            TransactionPercentageBox.Clear();
            TransactionNoteBox.Clear();
            UpdateTransactionTotal();

            TransactionSaved?.Invoke(this, EventArgs.Empty);

            MessageBox.Show("✅ تراکنش ثبت شد.");
        }

        private void AdjustInventory_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null)
            {
                MessageBox.Show("لطفاً یک مشتری انتخاب کنید.");
                return;
            }
            if (!EnsureClientExists()) return;

            var dialog = new AdjustInventoryDialog(_selectedClient.Id, GetClientStock(_selectedClient.Id));
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                ClientStockText.Text = $"موجودی: {GetClientStock(_selectedClient.Id):N3} گرم";
                LoadClientTransactions(_selectedClient.Id);
                TransactionSaved?.Invoke(this, EventArgs.Empty);
            }
        }

        private void TransactionInput_Changed(object sender, TextChangedEventArgs e) => UpdateTransactionTotal();

        private void TransactionTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateTransactionTotal();

        private void TransactionCostBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isCostFormatting) return;
            var textBox = (TextBox)sender;
            string currentText = textBox.Text;
            int caretPos = textBox.SelectionStart;

            string raw = new string(currentText.Where(c => char.IsDigit(c) || c == '.').ToArray());

            if (string.IsNullOrEmpty(raw))
            {
                if (textBox.Text != "")
                {
                    _isCostFormatting = true;
                    textBox.Text = "";
                    _isCostFormatting = false;
                }
                UpdateTransactionTotal();
                return;
            }

            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
            {
                string formatted = value.ToString("#,##0.########", CultureInfo.InvariantCulture);
                if (textBox.Text != formatted)
                {
                    int digitCountBeforeCaret = currentText.Take(caretPos).Count(c => char.IsDigit(c));
                    int newCaret = 0, digitCount = 0;
                    for (int i = 0; i < formatted.Length; i++)
                    {
                        if (char.IsDigit(formatted[i])) digitCount++;
                        if (digitCount >= digitCountBeforeCaret)
                        {
                            newCaret = i + 1;
                            break;
                        }
                    }
                    if (newCaret == 0 && digitCountBeforeCaret > 0)
                        newCaret = formatted.Length;

                    newCaret = Math.Min(newCaret, formatted.Length);

                    _isCostFormatting = true;
                    textBox.Text = formatted;
                    textBox.SelectionStart = newCaret;
                    _isCostFormatting = false;
                }
            }

            UpdateTransactionTotal();
        }

        private void UpdateTransactionTotal()
        {
            if (TransactionTotalText == null || TransactionProfitText == null) return;

            decimal weight = GetNumber(TransactionWeightBox.Text);
            decimal cost = GetNumber(TransactionCostBox.Text);
            decimal percent = GetNumber(TransactionPercentageBox.Text);

            string type = TransactionTypeCombo.SelectedIndex == 0 ? "Buy" : "Sell";

            decimal total = type == "Buy"
                ? weight * (cost + (percent / 100m) * cost)
                : weight * (cost - (percent / 100m) * cost);

            decimal profit = weight * (percent / 100m) * cost;

            TransactionTotalText.Text = total.ToString("N0", PersianCulture);
            TransactionProfitText.Text = profit.ToString("N0", PersianCulture);
        }

        private decimal GetNumber(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal v)) return v;
            if (decimal.TryParse(text, NumberStyles.Number, PersianCulture, out v)) return v;
            return 0;
        }

        private void PositiveNumber_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
                e.Handled = true;
        }

        private string ToPersianDateTime(DateTime date)
        {
            int year = _persianCalendar.GetYear(date);
            int month = _persianCalendar.GetMonth(date);
            int day = _persianCalendar.GetDayOfMonth(date);
            return $"{year:0000}/{month:00}/{day:00} {date:HH:mm}";
        }
        private void PrintClientHistory_Click(object sender, RoutedEventArgs e)
        {
            if (ClientTransactionsGrid.Items.Count == 0)
            {
                MessageBox.Show("هیچ تراکنشی برای چاپ وجود ندارد.", "چاپ",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true) return;

            double pageWidth = printDialog.PrintableAreaWidth;
            double pageHeight = printDialog.PrintableAreaHeight;
            if (pageWidth < pageHeight)
            {
                double temp = pageWidth;
                pageWidth = pageHeight;
                pageHeight = temp;
            }

            FixedDocument document = new FixedDocument();
            document.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);

            FixedPage page = new FixedPage
            {
                Width = pageWidth,
                Height = pageHeight,
                Background = Brushes.White
            };

            Grid container = new Grid
            {
                Width = pageWidth - 50,
                Height = pageHeight - 50,
                Margin = new Thickness(25),
                FlowDirection = FlowDirection.RightToLeft
            };

            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock title = new TextBlock
            {
                Text = "گزارش تراکنش‌های مشتری -طلای آب شده",
                FontFamily = new FontFamily("Tahoma"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(title, 0);
            container.Children.Add(title);

            TextBlock printDate = new TextBlock
            {
                Text = $"تاریخ چاپ: {ToPersianDateTime(DateTime.Now)}",
                FontFamily = new FontFamily("Tahoma"),
                FontSize = 9,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(printDate, 1);
            container.Children.Add(printDate);

            Grid table = new Grid
            {
                FlowDirection = FlowDirection.RightToLeft,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };

            double[] columnWidths = { 80, 80, 100, 70, 110, 130, 120, 200 };
            foreach (double width in columnWidths)
            {
                table.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(width, GridUnitType.Pixel)
                });
            }

            string[] headers = { "نوع", "وزن", "نرخ", "سود %", "مبلغ کل", "موجودی بعد", "تاریخ", "یادداشت" };
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            for (int i = 0; i < headers.Length; i++)
            {
                Border headerBorder = CreateCell(headers[i], true);
                Grid.SetColumn(headerBorder, i);
                Grid.SetRow(headerBorder, 0);
                table.Children.Add(headerBorder);
            }

            int rowIndex = 1;
            foreach (var item in ClientTransactionsGrid.Items)
            {
                var transaction = item as ClientTransactionDisplay;
                if (transaction == null) continue;

                table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(27) });

                string[] data =
                {
            transaction.TypeDisplay,
            transaction.WeightDisplay,
            FormatNumber(transaction.CostPerGram, "N0"),
            FormatNumber(transaction.SellerPercentage, "N2"),
            FormatMoney(transaction.TotalAmount),
            FormatNumber(transaction.RunningStock, "N3"),
            transaction.DateDisplay,
            transaction.Note ?? ""
        };

                for (int i = 0; i < data.Length; i++)
                {
                    Border cell = CreateCell(data[i], false);
                    if (rowIndex % 2 == 0)
                        cell.Background = new SolidColorBrush(Color.FromRgb(248, 248, 248));
                    Grid.SetColumn(cell, i);
                    Grid.SetRow(cell, rowIndex);
                    table.Children.Add(cell);
                }
                rowIndex++;
            }

            Grid.SetRow(table, 2);
            container.Children.Add(table);

            FixedPage.SetLeft(container, 0);
            FixedPage.SetTop(container, 0);
            page.Children.Add(container);

            PageContent pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(page);
            document.Pages.Add(pageContent);

            printDialog.PrintDocument(document.DocumentPaginator, "گزارش تراکنش‌های مشتری");
        }

        private Border CreateCell(string text, bool isHeader)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Tahoma"),
                FontSize = isHeader ? 8.5 : 7.5,
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(2),
                FlowDirection = FlowDirection.RightToLeft
            };

            Border border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0.5),
                Background = isHeader ? Brushes.LightGray : Brushes.White,
                Child = textBlock
            };

            return border;
        }

        private string FormatMoney(object? value)
        {
            if (value == null) return "0";
            try { return Convert.ToDecimal(value).ToString("N0", PersianCulture); }
            catch { return "0"; }
        }

        private string FormatNumber(object? value, string format)
        {
            if (value == null) return "0";
            try { return Convert.ToDecimal(value).ToString(format, PersianCulture); }
            catch { return "0"; }
        }

        private async void RefreshPrice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(
                        "https://api.brsapi.ir/Market/Gold_Currency.php?key=BK28NgtwemzhEUDJwZkRQRJDtznBMC9j");

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();

                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("gold", out var goldArray) &&
                            goldArray.ValueKind == JsonValueKind.Array &&
                            goldArray.GetArrayLength() > 0)
                        {
                            var firstGold = goldArray[0];
                            if (firstGold.TryGetProperty("price", out var priceElement) &&
                                priceElement.TryGetDecimal(out decimal price))
                            {
                                // Show with thousands separators (e.g., 19,066,700)
                                TransactionCostBox.Text = price.ToString("#,##0", CultureInfo.InvariantCulture);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ خطا در دریافت قیمت:\n{ex.Message}", "خطا",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        private void ClearClientDashboard()
        {
            ClientNameText.Text = "";
            ClientPhoneText.Text = "";
            ClientNoteText.Text = "";
            ClientStockText.Text = "";
            ClientTransactionsGrid.ItemsSource = null;
            _selectedClient = null;
        }

        private bool EnsureClientExists()
        {
            if (_selectedClient == null) return false;

            using (var db = new AppDbContext())
            {
                if (db.Clients.Any(c => c.Id == _selectedClient.Id))
                    return true;
            }

            // Client no longer exists
            ClearClientDashboard();
            MessageBox.Show("مشتری انتخاب شده حذف شده است.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    internal class ClientTransactionDisplay
    {
        public int Id { get; set; }
        public string TypeDisplay { get; set; } = "";
        public decimal Weight { get; set; }
        public string WeightDisplay { get; set; } = "";
        public decimal CostPerGram { get; set; }
        public decimal SellerPercentage { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime Date { get; set; }
        public string DateDisplay { get; set; } = "";
        public string Note { get; set; } = "";
        public decimal RunningStock { get; set; }
    }
}