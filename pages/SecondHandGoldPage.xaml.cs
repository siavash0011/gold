using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using GoldShop.Models;
using System.Net.Http;
using System.Text.Json;

namespace GoldShop.Pages
{
    public partial class SecondHandGoldPage : UserControl
    {
        private static readonly CultureInfo PersianCulture = new CultureInfo("fa-IR");
        private bool _isFormatting;

        public SecondHandGoldPage()
        {
            InitializeComponent();
            LoadTransactions();
            UpdateCalculations();
        }

        private void LoadTransactions()
        {
            using (var db = new AppDbContext())
            {
                var trans = db.SecondHandGoldTransactions
                    .OrderByDescending(t => t.Date)
                    .ToList()
                    .Select(t => new
                    {
                        t.Id,
                        TypeDisplay = t.Type == "Buy" ? "خرید مشتری" : "فروش مشتری",
                        t.CustomerName,
                        t.PhoneNumber,
                        t.Weight,
                        t.Purity,
                        t.Equivalent750Weight,
                        t.CostPerGram,
                        t.SellerPercentage,
                        t.TotalAmount,
                        Profit = t.Equivalent750Weight * (t.SellerPercentage / 100m) * t.CostPerGram,
                        t.Date,
                        t.Note
                    })
                    .ToList();
                TransactionsGrid.ItemsSource = trans;
            }
        }

        private void UpdateCalculations()
        {
            if (WeightBox == null || PurityCombo == null || CostPerGramBox == null ||
                SellerPercentageBox == null || TotalAmountText == null || Equivalent750Text == null ||
                ProfitText == null)
                return;

            decimal weight = GetNumber(WeightBox.Text);
            int purity = int.Parse((PurityCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "750");
            decimal cost = GetNumber(CostPerGramBox.Text);
            decimal percent = GetNumber(SellerPercentageBox.Text);

            decimal equivalent = weight * purity / 750m;
            Equivalent750Text.Text = equivalent.ToString("N3", PersianCulture);

            string type = (TransactionTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() == "خرید مشتری" ? "Buy" : "Sell";

            decimal total = type == "Buy"
                ? equivalent * (cost + (percent / 100m) * cost)
                : equivalent * (cost - (percent / 100m) * cost);
            TotalAmountText.Text = total.ToString("N0", PersianCulture);

            decimal profit = equivalent * (percent / 100m) * cost;
            ProfitText.Text = profit.ToString("N0", PersianCulture);
        }

        private void InputChanged(object sender, TextChangedEventArgs e) => UpdateCalculations();
        private void TypeCombo_Changed(object sender, SelectionChangedEventArgs e) => UpdateCalculations();
        private void PurityCombo_Changed(object sender, SelectionChangedEventArgs e) => UpdateCalculations();

        private void CostPerGram_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormatting) return;
            var textBox = (TextBox)sender;
            string currentText = textBox.Text;
            int caretPos = textBox.SelectionStart;

            string raw = new string(currentText.Where(c => char.IsDigit(c) || c == '.').ToArray());

            if (string.IsNullOrEmpty(raw))
            {
                if (textBox.Text != "")
                {
                    _isFormatting = true;
                    textBox.Text = "";
                    _isFormatting = false;
                }
                UpdateCalculations();
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

                    _isFormatting = true;
                    textBox.Text = formatted;
                    textBox.SelectionStart = newCaret;
                    _isFormatting = false;
                }
            }

            UpdateCalculations();
        }

        private async void RefreshPrice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(
                        "https://api.brsapi.ir/Market/Gold_Currency.php?key=B2EfAe7bmDycHFFj8gpTHgLKJZcF5wfF");
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();

                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("gold", out var goldArray) &&
                            goldArray.ValueKind == JsonValueKind.Array &&
                            goldArray.GetArrayLength() > 0)
                        {
                            string targetSymbol = "IR_GOLD_18K";


                            JsonElement? chosen = null;
                            foreach (var element in goldArray.EnumerateArray())
                            {
                                if (element.TryGetProperty("symbol", out var sym) &&
                                    sym.GetString() == targetSymbol)
                                {
                                    chosen = element;
                                    break;
                                }
                            }
                            if (chosen == null)
                                chosen = goldArray[0];

                            if (chosen.HasValue &&
                                chosen.Value.TryGetProperty("price", out var priceElement) &&
                                priceElement.TryGetDecimal(out decimal price))
                            {
                                CostPerGramBox.Text = price.ToString("0", CultureInfo.InvariantCulture);
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

        private void SaveTransaction_Click(object sender, RoutedEventArgs e)
        {
            decimal weight = GetNumber(WeightBox.Text);
            if (weight <= 0) { MessageBox.Show("وزن نامعتبر است."); return; }

            int purity = int.Parse((PurityCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "750");
            decimal cost = GetNumber(CostPerGramBox.Text);
            if (cost <= 0) { MessageBox.Show("نرخ نامعتبر است."); return; }

            decimal percent = GetNumber(SellerPercentageBox.Text);
            decimal equivalent = weight * purity / 750m;
            string type = (TransactionTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() == "خرید مشتری" ? "Buy" : "Sell";

            decimal total = type == "Buy"
                ? equivalent * (cost + (percent / 100m) * cost)
                : equivalent * (cost - (percent / 100m) * cost);

            using (var db = new AppDbContext())
            {
                var stock = db.GoldStocks.FirstOrDefault();
                if (stock != null)
                {
                    if (type == "Sell")
                        stock.CurrentStock += equivalent;
                    else if (type == "Buy")
                    {
                        if (equivalent > stock.CurrentStock)
                        {
                            MessageBox.Show("موجودی کافی نیست.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        stock.CurrentStock -= equivalent;
                    }
                }

                var transaction = new SecondHandGoldTransaction
                {
                    Type = type,
                    CustomerName = CustomerNameBox.Text.Trim(),
                    PhoneNumber = PhoneNumberBox.Text.Trim(),
                    Weight = weight,
                    Purity = purity,
                    CostPerGram = cost,
                    SellerPercentage = percent,
                    TotalAmount = total,
                    Equivalent750Weight = equivalent,
                    Date = TransactionDate.SelectedDate ?? DateTime.Now,
                    Note = NoteBox.Text.Trim()
                };

                db.SecondHandGoldTransactions.Add(transaction);
                db.SaveChanges();
            }

            CustomerNameBox.Clear();
            PhoneNumberBox.Clear();
            WeightBox.Clear();
            CostPerGramBox.Clear();
            SellerPercentageBox.Clear();
            NoteBox.Clear();
            TotalAmountText.Text = "";
            Equivalent750Text.Text = "";
            ProfitText.Text = "";
            LoadTransactions();

            MessageBox.Show("✅ تراکنش ثبت شد.");
        }

        private void DeleteTransaction_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            int transactionId = Convert.ToInt32(button.Tag);

            var result = MessageBox.Show("آیا مطمئن هستید که این تراکنش حذف شود؟",
                                         "تأیید حذف",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            using (var db = new AppDbContext())
            {
                var transaction = db.SecondHandGoldTransactions.Find(transactionId);
                if (transaction == null) return;

                var stock = db.GoldStocks.FirstOrDefault();
                if (stock != null)
                {
                    // Reverse the effect
                    if (transaction.Type == "Sell")
                        stock.CurrentStock -= transaction.Equivalent750Weight;
                    else if (transaction.Type == "Buy")
                        stock.CurrentStock += transaction.Equivalent750Weight;
                }

                db.SecondHandGoldTransactions.Remove(transaction);
                db.SaveChanges();
            }

            LoadTransactions();
            MessageBox.Show("✅ تراکنش حذف شد و موجودی اصلاح شد.", "موفق",
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PrintHistory_Click(object sender, RoutedEventArgs e)
        {
            if (TransactionsGrid.Items.Count == 0)
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
                Text = "گزارش طلای دسته دوم",
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
                Text = $"تاریخ چاپ: {DateTime.Now:yyyy/MM/dd HH:mm}",
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

            // Updated column widths – added name and phone
            double[] widths =
            {
        80,    // نوع
        120,   // نام مشتری
        100,   // تلفن
        90,    // وزن
        60,    // عیار
        90,    // معادل 750
        100,   // نرخ
        70,    // درصد
        110,   // مبلغ کل
        100,   // سود
        120,   // تاریخ
        200    // یادداشت
    };

            foreach (double w in widths)
            {
                table.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(w, GridUnitType.Pixel)
                });
            }

            // Updated headers – added name and phone
            string[] headers =
            {
        "نوع", "نام مشتری", "تلفن", "وزن", "عیار", "معادل 750",
        "نرخ", "درصد", "مبلغ کل", "سود", "تاریخ", "یادداشت"
    };

            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            for (int i = 0; i < headers.Length; i++)
            {
                Border headerBorder = CreateCell(headers[i], true);
                Grid.SetColumn(headerBorder, i);
                Grid.SetRow(headerBorder, 0);
                table.Children.Add(headerBorder);
            }

            int rowIndex = 1;
            foreach (var item in TransactionsGrid.Items)
            {
                dynamic row = item;
                table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(27) });

                // Updated data – added name and phone
                string[] data =
                {
            row.TypeDisplay,
            row.CustomerName ?? "",
            row.PhoneNumber ?? "",
            row.Weight.ToString("N3"),
            row.Purity.ToString(),
            row.Equivalent750Weight.ToString("N3"),
            row.CostPerGram.ToString("N0"),
            row.SellerPercentage.ToString("N2"),
            row.TotalAmount.ToString("N0"),
            row.Profit.ToString("N0"),
            row.Date.ToString("yyyy/MM/dd HH:mm"),
            row.Note ?? ""
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

            printDialog.PrintDocument(document.DocumentPaginator, "گزارش طلای دسته دوم");
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
    }
}