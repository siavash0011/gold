using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GoldShop.Models;
using System.Net.Http;
using System.Text.Json;

namespace GoldShop.Pages
{
    public partial class SellPage : UserControl
    {
        private bool _isInitialized = false;
        private bool _isFormatting;
        private static readonly CultureInfo PersianCulture = new CultureInfo("fa-IR");
        private readonly PersianCalendar _persianCalendar = new PersianCalendar();

        public SellPage()
        {
            InitializeComponent();
            _isInitialized = true;
            InitializeGoldStock();
            UpdateSummary();
            UpdateCurrentStockDisplay();
        }

        // ==================== GOLD STOCK ====================
        private void InitializeGoldStock()
        {
            using (var db = new AppDbContext())
            {
                if (!db.GoldStocks.Any())
                {
                    db.GoldStocks.Add(new GoldStock { CurrentStock = 0 });
                    db.SaveChanges();
                }
            }
        }

        private decimal GetCurrentStock()
        {
            using (var db = new AppDbContext())
            {
                return db.GoldStocks.FirstOrDefault()?.CurrentStock ?? 0;
            }
        }

        private void UpdateCurrentStockDisplay()
        {
            CurrentStockText.Text = $"{GetCurrentStock():N3} گرم";
        }

        private void AdjustStock_Click(object sender, RoutedEventArgs e)
        {
            decimal currentStock = GetCurrentStock();

            var dialog = new AdjustGoldStockDialog(currentStock);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                decimal amount = dialog.AdjustmentAmount;
                bool isIncrease = dialog.IsIncrease;

                // Calculate new stock
                decimal newStock = isIncrease ? currentStock + amount : currentStock - amount;

                if (newStock < 0)
                {
                    MessageBox.Show("کاهش بیشتر از موجودی فعلی مجاز نیست.", "خطا",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (var db = new AppDbContext())
                {
                    // Update the stock
                    var stock = db.GoldStocks.FirstOrDefault();
                    if (stock != null)
                    {
                        stock.CurrentStock = newStock;
                    }

                    // Create adjustment transaction record
                    var adjustmentTransaction = new Transaction
                    {
                        Date = DateTime.Now,
                        CustomerName = "طلافروشی",
                        PhoneNumber = "",
                        Weight = isIncrease ? amount : -amount,   // positive for add, negative for remove
                        CostPerGram = 0,
                        MakingPercentage = 0,
                        SellerPercentage = 0,
                        TaxPercentage = 0,
                        MakingCost = 0,
                        SellerCost = 0,
                        Tax = 0,
                        FinalCost = 0,
                        CreatedAt = DateTime.Now,
                        Purity = 750,
                        Note = $"اصلاح موجودی از {currentStock:N3} گرم به {newStock:N3} گرم",
                        IncludeInProfit = false
                    };

                    db.Transactions.Add(adjustmentTransaction);
                    db.SaveChanges();
                }

                // Refresh UI
                UpdateCurrentStockDisplay();
                UpdateSummary();
                HistoryPageControl.LoadTransactions();
            }
        }

        // ==================== SUMMARY ====================
        private void UpdateSummary()
        {
            using (var db = new AppDbContext())
            {
                DateTime today = DateTime.Today;
                DateTime weekStart = GetStartOfWeek(today);
                DateTime weekEnd = weekStart.AddDays(6);
                DateTime monthStart = GetStartOfMonth(today);
                DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);

                var todayData = db.Transactions
                    .Where(t => t.IncludeInProfit)
                    .Where(t => t.Date >= today && t.Date < today.AddDays(1))
                    .ToList();
                var weekData = db.Transactions
                    .Where(t => t.IncludeInProfit)
                    .Where(t => t.Date >= weekStart && t.Date < weekEnd.AddDays(1))
                    .ToList();
                var monthData = db.Transactions
                    .Where(t => t.IncludeInProfit)
                    .Where(t => t.Date >= monthStart && t.Date < monthEnd.AddDays(1))
                    .ToList();

                TodayWeightText.Text = $"{todayData.Sum(t => t.Weight):N2} گرم";
                TodayProfitText.Text = $"{todayData.Sum(t => t.SellerCost):N0} سود";
                TodayDateRange.Text = ToPersianDate(today);

                WeekWeightText.Text = $"{weekData.Sum(t => t.Weight):N2} گرم";
                WeekProfitText.Text = $"{weekData.Sum(t => t.SellerCost):N0} سود";
                WeekDateRange.Text = $"از {ToPersianDate(weekStart)} تا {ToPersianDate(weekEnd)}";

                MonthWeightText.Text = $"{monthData.Sum(t => t.Weight):N2} گرم";
                MonthProfitText.Text = $"{monthData.Sum(t => t.SellerCost):N0} سود";
                MonthDateRange.Text = $"از {ToPersianDate(monthStart)} تا {ToPersianDate(monthEnd)}";
            }
        }

        private void ResetProfit_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "آیا مطمئن هستید که سود به صفر برسد؟\n(موجودی و تاریخچه تراکنش‌ها حفظ خواهد شد، فقط از محاسبه سود خارج می‌شوند.)",
                "تأیید ریست سود",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            using (var db = new AppDbContext())
            {
                var transactions = db.Transactions.ToList();
                foreach (var t in transactions)
                {
                    t.IncludeInProfit = false;
                }
                db.SaveChanges();
            }

            UpdateSummary();
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            int dayOfWeek = (int)_persianCalendar.GetDayOfWeek(date) - (int)DayOfWeek.Saturday;
            if (dayOfWeek < 0) dayOfWeek += 7;
            return date.Date.AddDays(-dayOfWeek);
        }

        private DateTime GetStartOfMonth(DateTime date)
        {
            int year = _persianCalendar.GetYear(date);
            int month = _persianCalendar.GetMonth(date);
            return _persianCalendar.ToDateTime(year, month, 1, 0, 0, 0, 0);
        }

        private string ToPersianDate(DateTime date)
        {
            int year = _persianCalendar.GetYear(date);
            int month = _persianCalendar.GetMonth(date);
            int day = _persianCalendar.GetDayOfMonth(date);
            return $"{year:0000}/{month:00}/{day:00}";
        }

        // ==================== SEED DATA ====================
        private void SeedData_Click(object sender, RoutedEventArgs e)
        {
            int count = 100;
            var random = new Random();
            var now = DateTime.Now;
            var threeMonthsAgo = now.AddMonths(-3);

            using (var db = new AppDbContext())
            {
                for (int i = 0; i < count; i++)
                {
                    int daysBack = random.Next(0, (now - threeMonthsAgo).Days);
                    DateTime date = now.AddDays(-daysBack);

                    decimal weight = (decimal)(random.Next(50, 500) / 10.0);
                    decimal costPerGram = random.Next(2000000, 6000000) / 100m;
                    decimal makingPct = random.Next(0, 15);
                    decimal sellerPct = random.Next(0, 10);
                    decimal taxPct = random.Next(0, 10);
                    int purity = new[] { 750, 849, 999 }[random.Next(0, 3)];

                    decimal makingCost = (makingPct / 100m) * weight;
                    decimal sellerCost = (sellerPct / 100m) * (weight + makingCost);
                    decimal tax = (taxPct / 100m) * (weight + makingCost + sellerCost);
                    decimal finalCost = (weight + makingCost + sellerCost + tax) * costPerGram;

                    var transaction = new Transaction
                    {
                        Date = date,
                        CustomerName = "مشتری " + (i + 1),
                        PhoneNumber = "0912" + random.Next(1000000, 9999999).ToString("D7"),
                        Weight = weight,
                        CostPerGram = costPerGram,
                        MakingPercentage = makingPct,
                        SellerPercentage = sellerPct,
                        TaxPercentage = taxPct,
                        MakingCost = makingCost * costPerGram,
                        SellerCost = sellerCost * costPerGram,
                        Tax = tax * costPerGram,
                        FinalCost = finalCost,
                        CreatedAt = date.AddHours(random.Next(0, 24)).AddMinutes(random.Next(0, 60)),
                        Purity = purity,
                        IncludeInProfit = true
                    };

                    db.Transactions.Add(transaction);
                }
                db.SaveChanges();
            }

            UpdateSummary();
            UpdateCurrentStockDisplay();
            MessageBox.Show($"✅ {count} رکورد آزمایشی با موفقیت اضافه شد!",
                            "داده‌های آزمایشی",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
        }

        // ==================== INPUT HELPERS ====================
        private void Weight_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormatting) return;
            var textBox = (TextBox)sender;
            string currentText = textBox.Text;
            int caretPos = textBox.SelectionStart;

            string raw = new string(currentText.Where(c => char.IsDigit(c) || c == '.').ToArray());

            int dotIndex = raw.IndexOf('.');
            if (dotIndex != -1)
            {
                string beforeDot = raw.Substring(0, dotIndex);
                string afterDot = raw.Substring(dotIndex + 1);
                afterDot = new string(afterDot.Where(c => char.IsDigit(c)).ToArray());
                raw = beforeDot + "." + afterDot;
            }

            if (raw.StartsWith(".")) raw = "0" + raw;

            if (string.IsNullOrEmpty(raw))
            {
                if (textBox.Text != "")
                {
                    _isFormatting = true;
                    textBox.Text = "";
                    _isFormatting = false;
                }
                Recalculate();
                return;
            }

            if (textBox.Text != raw)
            {
                _isFormatting = true;
                textBox.Text = raw;
                textBox.SelectionStart = Math.Min(caretPos, raw.Length);
                _isFormatting = false;
            }

            Recalculate();
        }

        private void PositiveNumber_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
                e.Handled = true;
        }

        private void CalculateValues(object sender, TextChangedEventArgs e)
        {
            Recalculate();
        }

        private void Recalculate()
        {
            if (Weight == null ||
                CostPerGram == null ||
                MakingPercentage == null ||
                SellerPercentage == null ||
                TaxPercentage == null ||
                FinalCostText == null ||
                MakingCostText == null ||
                SellerCostText == null ||
                TaxText == null)
                return;

            decimal weight = GetNumber(Weight.Text);
            decimal costPerGram = GetNumber(CostPerGram.Text);

            decimal makingPercentage = GetNumber(MakingPercentage.Text) / 100m;
            decimal sellerPercentage = GetNumber(SellerPercentage.Text) / 100m;
            decimal taxPercentage = GetNumber(TaxPercentage.Text) / 100m;

            decimal makingCost = makingPercentage * weight;
            decimal sellerCost = sellerPercentage * (weight + makingCost);
            decimal tax = taxPercentage * (weight + makingCost + sellerCost);
            decimal finalCost = (weight + makingCost + sellerCost + tax) * costPerGram;

            FinalCostText.Text = finalCost.ToString("N0");

            decimal makingCostMoney = makingCost * costPerGram;
            decimal sellerCostMoney = sellerCost * costPerGram;
            decimal taxMoney = tax * costPerGram;

            MakingCostText.Text = $"اجرت ساخت : {makingCostMoney:N0}";
            SellerCostText.Text = $"اجرت فروشنده : {sellerCostMoney:N0}";
            TaxText.Text = $"مالیات : {taxMoney:N0}";
        }

        private decimal GetNumber(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                return value;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("fa-IR"), out value))
                return value;
            return 0;
        }

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
                Recalculate();
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

            Recalculate();
        }

        private void CostPerGram_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox) textBox.SelectAll();
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
                            int purity = GetSelectedPurity();
                            string targetSymbol = GetSymbolForPurity(purity);

                            JsonElement? chosen = null;
                            foreach (var element in goldArray.EnumerateArray())
                            {
                                if (element.TryGetProperty("symbol", out var symbolProp) &&
                                    symbolProp.GetString() == targetSymbol)
                                {
                                    chosen = element;
                                    break;
                                }
                            }

                            // Fallback to first item (18K) if target not found
                            if (chosen == null)
                                chosen = goldArray[0];

                            if (chosen.HasValue &&
                                chosen.Value.TryGetProperty("price", out var priceElement) &&
                                priceElement.TryGetDecimal(out decimal price))
                            {
                                CostPerGram.Text = price.ToString("0", CultureInfo.InvariantCulture);
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

        // ==================== SAVE TRANSACTION ====================
        private void SaveTransaction_Click(object sender, RoutedEventArgs e)
        {
            decimal weight = GetNumber(Weight.Text);
            if (weight <= 0)
            {
                MessageBox.Show("لطفاً وزن را وارد کنید.", "اعتبارسنجی",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                Weight.Focus();
                return;
            }

            decimal costPerGram = GetNumber(CostPerGram.Text);
            if (costPerGram <= 0)
            {
                MessageBox.Show("لطفاً نرخ را وارد کنید.", "اعتبارسنجی",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                CostPerGram.Focus();
                return;
            }

            decimal makingPercentage = GetNumber(MakingPercentage.Text) / 100m;
            decimal sellerPercentage = GetNumber(SellerPercentage.Text) / 100m;
            decimal taxPercentage = GetNumber(TaxPercentage.Text) / 100m;

            decimal makingCost = makingPercentage * weight;
            decimal sellerCost = sellerPercentage * (weight + makingCost);
            decimal tax = taxPercentage * (weight + makingCost + sellerCost);
            decimal finalCost = (weight + makingCost + sellerCost + tax) * costPerGram;

            int purity = 750;
            if (PurityComboBox.SelectedItem is ComboBoxItem item)
            {
                string content = item.Content?.ToString() ?? "";
                int.TryParse(content, out purity);
            }

            var transaction = new Transaction
            {
                Date = TransactionDate.SelectedDate ?? DateTime.Now,
                CustomerName = string.IsNullOrWhiteSpace(CustomerName.Text) ? "مشتری" : CustomerName.Text,
                PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber.Text) ? "" : PhoneNumber.Text,
                Weight = weight,
                CostPerGram = costPerGram,
                MakingPercentage = makingPercentage * 100,
                SellerPercentage = sellerPercentage * 100,
                TaxPercentage = taxPercentage * 100,
                MakingCost = makingCost * costPerGram,
                SellerCost = sellerCost * costPerGram,
                Tax = tax * costPerGram,
                FinalCost = finalCost,
                CreatedAt = DateTime.Now,
                Purity = purity,
                Note = NoteBox.Text.Trim(),
                IncludeInProfit = true
            };

            try
            {
                using (var db = new AppDbContext())
                {
                    db.Transactions.Add(transaction);
                    db.SaveChanges();

                    var stock = db.GoldStocks.FirstOrDefault();
                    if (stock != null)
                    {
                        stock.CurrentStock -= weight;
                        db.SaveChanges();
                    }
                }

                UpdateSummary();
                UpdateCurrentStockDisplay();
                HistoryPageControl.LoadTransactions();

                string persianDate = transaction.Date.ToString("yyyy/MM/dd", PersianCulture);
                MessageBox.Show(
                    $"✅ تراکنش با موفقیت ذخیره شد!\n\n" +
                    $"تاریخ: {persianDate}\n" +
                    $"مشتری: {transaction.CustomerName}\n" +
                    $"وزن: {weight:N2} گرم\n" +
                    $"نرخ: {costPerGram:N0}\n" +
                    $"عیار: {purity}\n" +
                    $"بهای نهایی: {finalCost:N0}",
                    "فروش طلا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ خطا در ذخیره تراکنش:\n{ex.InnerException?.Message ?? ex.Message}",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

        }
        private int GetSelectedPurity()
        {
            if (PurityComboBox.SelectedItem is ComboBoxItem item)
            {
                string content = item.Content?.ToString() ?? "";
                if (int.TryParse(content, out int purity))
                    return purity;
            }
            return 750;
        }

        private string GetSymbolForPurity(int purity)
        {
            return purity switch
            {
                750 => "IR_GOLD_18K",
                849 => "IR_GOLD_21K",   // if API supports it, otherwise we'll fallback
                999 => "IR_GOLD_24K",
                _ => "IR_GOLD_18K"
            };
        }
        private void PurityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;   // ignore initial event
            RefreshPrice_Click(sender, e);
        }
    }
}