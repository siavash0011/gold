using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GoldShop.Models;
using Microsoft.EntityFrameworkCore;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Collections.Generic;

namespace GoldShop.Pages
{

    public partial class DashboardPage : UserControl
    {
        private readonly PersianCalendar _persianCalendar = new PersianCalendar();

        public DashboardPage()
        {
            InitializeComponent();
            LoadClientTransactions(null, null);
            UpdateProfitSummary();

            // ✅ Subscribe to event from ClientsPage
            ClientsPageControl.TransactionSaved += OnTransactionSaved;
        }

        private void OnTransactionSaved(object? sender, EventArgs e)
        {
            UpdateProfitSummary();
            LoadClientTransactions(null, null);
        }

        private void LoadClientTransactions(DateTime? fromDate, DateTime? toDate)
        {
            using (var db = new AppDbContext())
            {
                IQueryable<ClientTransaction> query = db.ClientTransactions
                    .Include(t => t.Client);

                if (fromDate.HasValue)
                {
                    DateTime from = fromDate.Value.Date;
                    query = query.Where(t => t.Date >= from);
                }
                if (toDate.HasValue)
                {
                    DateTime to = toDate.Value.AddDays(1);
                    query = query.Where(t => t.Date < to);
                }

                var transactions = query
                    .OrderByDescending(t => t.Date)
                    .Take(50)
                    .ToList()
                    .Select(t => new UnifiedTransaction
                    {
                        Id = t.Id,
                        DateDisplay = ToPersianDateTime(t.Date),
                        TypeDisplay = t.Type switch
                        {
                            "Buy" => "خرید مشتری",
                            "Sell" => "فروش مشتری",
                            "Adjust" => "اصلاح موجودی",
                            _ => t.Type
                        },
                        CustomerName = t.Client?.Name ?? "",
                        Phone = t.Client?.Phone ?? "",
                        Weight = t.Weight,
                        TotalAmount = t.TotalAmount,
                        Profit = t.Weight * (t.SellerPercentage / 100m) * t.CostPerGram,
                        Note = t.Note ?? ""
                    })
                    .ToList();

                GeneralTransactionsGrid.ItemsSource = transactions;
            }
        }

        private void UpdateProfitSummary()
        {
            using (var db = new AppDbContext())
            {
                DateTime today = DateTime.Today;
                DateTime weekStart = GetStartOfWeek(today);
                DateTime weekEnd = weekStart.AddDays(6);
                DateTime monthStart = GetStartOfMonth(today);
                DateTime monthEnd = GetEndOfMonth(today);

                var todayProfit = db.ClientTransactions
                    .Where(t => t.IncludeInProfit)
                    .Where(t => t.Date >= today && t.Date < today.AddDays(1))
                    .Sum(t => t.Weight * (t.SellerPercentage / 100m) * t.CostPerGram);

                var weekProfit = db.ClientTransactions
                    .Where(t => t.IncludeInProfit)
                    .Where(t => t.Date >= weekStart && t.Date < weekEnd.AddDays(1))
                    .Sum(t => t.Weight * (t.SellerPercentage / 100m) * t.CostPerGram);

                var monthProfit = db.ClientTransactions
                    .Where(t => t.IncludeInProfit)
                    .Where(t => t.Date >= monthStart && t.Date < monthEnd.AddDays(1))
                    .Sum(t => t.Weight * (t.SellerPercentage / 100m) * t.CostPerGram);

                TodayProfitText.Text = todayProfit.ToString("N0");
                WeekProfitText.Text = weekProfit.ToString("N0");
                MonthProfitText.Text = monthProfit.ToString("N0");

                TodayDateRange.Text = ToPersianDate(today);
                WeekDateRange.Text = $"از {ToPersianDate(weekStart)} تا {ToPersianDate(weekEnd)}";
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
                var transactions = db.ClientTransactions.ToList();
                foreach (var t in transactions)
                {
                    t.IncludeInProfit = false;
                }
                db.SaveChanges();
            }

            UpdateProfitSummary();
            LoadClientTransactions(null, null);
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

        private DateTime GetEndOfMonth(DateTime date)
        {
            int year = _persianCalendar.GetYear(date);
            int month = _persianCalendar.GetMonth(date);
            int daysInMonth = _persianCalendar.GetDaysInMonth(year, month);
            return _persianCalendar.ToDateTime(year, month, daysInMonth, 23, 59, 59, 999);
        }

        private string ToPersianDate(DateTime date)
        {
            int year = _persianCalendar.GetYear(date);
            int month = _persianCalendar.GetMonth(date);
            int day = _persianCalendar.GetDayOfMonth(date);
            return $"{year:0000}/{month:00}/{day:00}";
        }

        private string ToPersianDateTime(DateTime date)
        {
            return $"{ToPersianDate(date)} {date:HH:mm}";
        }

        private void DateRange_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            FromDatePicker.SelectedDate = DateTime.Today;
            ToDatePicker.SelectedDate = DateTime.Today;
            ApplyFilter();
        }

        private void Week_Click(object sender, RoutedEventArgs e)
        {
            DateTime weekStart = GetStartOfWeek(DateTime.Today);
            FromDatePicker.SelectedDate = weekStart;
            ToDatePicker.SelectedDate = weekStart.AddDays(6);
            ApplyFilter();
        }

        private void Month_Click(object sender, RoutedEventArgs e)
        {
            DateTime monthStart = GetStartOfMonth(DateTime.Today);
            FromDatePicker.SelectedDate = monthStart;
            ToDatePicker.SelectedDate = GetEndOfMonth(DateTime.Today);
            ApplyFilter();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            FromDatePicker.SelectedDate = null;
            ToDatePicker.SelectedDate = null;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            DateTime? from = FromDatePicker.SelectedDate;
            DateTime? to = ToDatePicker.SelectedDate;

            if (from.HasValue && to.HasValue && from > to)
            {
                MessageBox.Show("تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد.", "خطا",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadClientTransactions(from, to);
        }
        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (GeneralTransactionsGrid.Items.Count == 0)
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
                Text = "گزارش تراکنش‌ها طلای آب شده ",
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

            double[] columnWidths = { 130, 80, 120, 100, 90, 110, 100, 200 };
            foreach (double width in columnWidths)
            {
                table.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(width, GridUnitType.Pixel)
                });
            }

            string[] headers = { "تاریخ", "نوع", "مشتری", "تلفن", "وزن (گرم)", "مبلغ کل", "سود", "یادداشت" };
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            for (int i = 0; i < headers.Length; i++)
            {
                Border headerBorder = CreateCell(headers[i], true);
                Grid.SetColumn(headerBorder, i);
                Grid.SetRow(headerBorder, 0);
                table.Children.Add(headerBorder);
            }

            int rowIndex = 1;
            foreach (var item in GeneralTransactionsGrid.Items)
            {
                var transaction = item as UnifiedTransaction;
                if (transaction == null) continue;

                table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(27) });

                string[] data =
                {
            transaction.DateDisplay,
            transaction.TypeDisplay,
            transaction.CustomerName,
            transaction.Phone,
            FormatNumber(transaction.Weight, "N3"),
            FormatMoney(transaction.TotalAmount),
            FormatMoney(transaction.Profit),
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

            printDialog.PrintDocument(document.DocumentPaginator, "گزارش آخرین تراکنش‌ها");
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
        private static readonly CultureInfo PersianCulture = new CultureInfo("fa-IR");

    }
}