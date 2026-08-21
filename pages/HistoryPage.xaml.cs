
using GoldShop.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace GoldShop.Pages
{
    public partial class HistoryPage : UserControl
    {
        private readonly PersianCalendar _persianCalendar = new PersianCalendar();

        private static readonly CultureInfo PersianCulture =
            new CultureInfo("fa-IR");

        public HistoryPage()
        {
            InitializeComponent();

            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;

            LoadTransactions();
        }


        // ============================================================
        // LOAD TRANSACTIONS
        // ============================================================

        public void LoadTransactions()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    var query = db.Transactions.AsQueryable();

                    bool hasDateFilter = false;

                    if (StartDatePicker.SelectedDate.HasValue)
                    {
                        query = query.Where(
                            t => t.Date >= StartDatePicker.SelectedDate.Value.Date);
                        hasDateFilter = true;
                    }

                    if (EndDatePicker.SelectedDate.HasValue)
                    {
                        DateTime endDate =
                            EndDatePicker.SelectedDate.Value.Date.AddDays(1);

                        query = query.Where(
                            t => t.Date < endDate);
                        hasDateFilter = true;
                    }

                    query = query.OrderByDescending(t => t.CreatedAt);

                    // Apply limit ONLY when no date filter is active
                    var result = hasDateFilter
                        ? query.ToList()        // all matching records
                        : query.Take(50).ToList(); // recent 50

                    TransactionsGrid.ItemsSource = result;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ خطا در بارگذاری تاریخچه:\n{ex.Message}",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // ============================================================
        // DATE PICKER
        // ============================================================

        private void DatePicker_SelectedDateChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            LoadTransactions();
        }


        // ============================================================
        // QUICK FILTERS
        // ============================================================

        private void Today_Click(
            object sender,
            RoutedEventArgs e)
        {
            DateTime today = DateTime.Today;

            StartDatePicker.SelectedDate = today;
            EndDatePicker.SelectedDate = today;
        }


        private void ThisWeek_Click(
            object sender,
            RoutedEventArgs e)
        {
            DateTime today = DateTime.Today;

            DateTime saturday =
                GetStartOfWeek(today);

            DateTime friday =
                saturday.AddDays(6);

            StartDatePicker.SelectedDate = saturday;
            EndDatePicker.SelectedDate = friday;
        }


        private void ThisMonth_Click(
            object sender,
            RoutedEventArgs e)
        {
            DateTime today = DateTime.Today;

            DateTime firstDay =
                GetStartOfMonth(today);

            DateTime lastDay =
                GetEndOfMonth(today);

            StartDatePicker.SelectedDate = firstDay;
            EndDatePicker.SelectedDate = lastDay;
        }


        // ============================================================
        // PERSIAN CALENDAR
        // ============================================================

        private DateTime GetStartOfWeek(DateTime date)
        {
            int dayOfWeek =
                (int)_persianCalendar.GetDayOfWeek(date)
                - (int)DayOfWeek.Saturday;

            if (dayOfWeek < 0)
                dayOfWeek += 7;

            return date.Date.AddDays(-dayOfWeek);
        }


        private DateTime GetStartOfMonth(DateTime date)
        {
            int year =
                _persianCalendar.GetYear(date);

            int month =
                _persianCalendar.GetMonth(date);

            return _persianCalendar.ToDateTime(
                year,
                month,
                1,
                0,
                0,
                0,
                0);
        }


        private DateTime GetEndOfMonth(DateTime date)
        {
            int year =
                _persianCalendar.GetYear(date);

            int month =
                _persianCalendar.GetMonth(date);

            int daysInMonth =
                _persianCalendar.GetDaysInMonth(
                    year,
                    month);

            return _persianCalendar.ToDateTime(
                year,
                month,
                daysInMonth,
                23,
                59,
                59,
                999);
        }


        // ============================================================
        // DELETE
        // ============================================================

        private void DeleteButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var button = sender as Button;

            if (button == null)
                return;

            int id = Convert.ToInt32(button.Tag);

            var result = MessageBox.Show(
                "آیا مطمئن هستید که این رکورد را حذف می‌کنید؟",
                "تأیید حذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                using (var db = new AppDbContext())
                {
                    var transaction =
                        db.Transactions.Find(id);

                    if (transaction != null)
                    {
                        db.Transactions.Remove(transaction);

                        db.SaveChanges();
                    }
                }

                LoadTransactions();

                MessageBox.Show(
                    "✅ رکورد با موفقیت حذف شد.",
                    "حذف",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ خطا در حذف رکورد:\n{ex.Message}",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // ============================================================
        // PRINT
        // ============================================================

        private void Print_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (TransactionsGrid.Items.Count == 0)
            {
                MessageBox.Show(
                    "هیچ رکوردی برای چاپ وجود ندارد.",
                    "چاپ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            PrintDialog printDialog = new PrintDialog();

            if (printDialog.ShowDialog() != true)
                return;


            // ========================================================
            // LANDSCAPE
            // ========================================================

            double pageWidth =
                printDialog.PrintableAreaWidth;

            double pageHeight =
                printDialog.PrintableAreaHeight;


            // Swap dimensions for landscape
            if (pageWidth < pageHeight)
            {
                double temp = pageWidth;

                pageWidth = pageHeight;
                pageHeight = temp;
            }


            // ========================================================
            // DOCUMENT
            // ========================================================

            FixedDocument document =
                new FixedDocument();

            document.DocumentPaginator.PageSize =
                new Size(pageWidth, pageHeight);


            // ========================================================
            // PAGE
            // ========================================================

            FixedPage page =
                new FixedPage();

            page.Width = pageWidth;
            page.Height = pageHeight;

            page.Background =
                Brushes.White;


            // ========================================================
            // MAIN CONTAINER
            // ========================================================

            Grid container =
                new Grid();

            container.Width =
                pageWidth - 50;

            container.Height =
                pageHeight - 50;

            container.Margin =
                new Thickness(25);

            container.FlowDirection =
                FlowDirection.RightToLeft;


            // ========================================================
            // ROWS
            // ========================================================

            container.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            container.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            container.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });


            // ========================================================
            // TITLE
            // ========================================================

            TextBlock title =
                new TextBlock
                {
                    Text = "گزارش تاریخچه معاملات طلا",

                    FontFamily =
                        new FontFamily("Tahoma"),

                    FontSize = 18,

                    FontWeight =
                        FontWeights.Bold,

                    TextAlignment =
                        TextAlignment.Center,

                    HorizontalAlignment =
                        HorizontalAlignment.Stretch,

                    Margin =
                        new Thickness(0, 0, 0, 10)
                };

            Grid.SetRow(title, 0);

            container.Children.Add(title);


            // ========================================================
            // PRINT DATE
            // ========================================================

            DateTime now =
                DateTime.Now;

            string persianNow =
                ToPersianDateTime(now);


            TextBlock printDate =
                new TextBlock
                {
                    Text =
                        $"تاریخ چاپ: {persianNow}",

                    FontFamily =
                        new FontFamily("Tahoma"),

                    FontSize = 9,

                    TextAlignment =
                        TextAlignment.Center,

                    HorizontalAlignment =
                        HorizontalAlignment.Stretch,

                    Margin =
                        new Thickness(0, 0, 0, 10)
                };

            Grid.SetRow(printDate, 1);

            container.Children.Add(printDate);


            // ========================================================
            // TABLE
            // ========================================================

            Grid table =
                new Grid();

            table.FlowDirection =
                FlowDirection.RightToLeft;

            table.HorizontalAlignment =
                HorizontalAlignment.Stretch;

            table.VerticalAlignment =
                VerticalAlignment.Top;


            // ========================================================
            // COLUMN WIDTHS
            // ========================================================
            //
            // Important columns get more space.
            //
            // Date
            // Customer
            // Phone
            // Weight
            // Price
            // Making
            // Seller
            // Tax
            // Final
            // Time
            //
            // ========================================================
            double[] columnWidths =
       {
    65,   // تاریخ
    105,  // مشتری
    65,   // تلفن
    55,   // وزن
    45,   // عیار
    80,   // نرخ
    40,   // اجرت %
    85,   // اجرت
    40,   // سود %
    85,   // سود
    40,   // مالیات %
    85,   // مالیات
    95,   // بهای نهایی
    145   // زمان ثبت
};

            foreach (double width in columnWidths)
            {
                table.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = new GridLength(
                            width,
                            GridUnitType.Pixel)
                    });
            }


            foreach (double width in columnWidths)
            {
                table.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                width,
                                GridUnitType.Pixel)
                    });
            }


            // ========================================================
            // HEADER
            // ========================================================

            string[] headers =
  {
    "تاریخ فروش",
    "نام مشتری",
    "تلفن",
    "وزن (گرم)",
    "عیار",
    "نرخ",
    "اجرت %",
    "اجرت",
    "سود %",
    "سود",
    "مالیات %",
    "مالیات",
    "بهای نهایی",
    "زمان ثبت"
};


            table.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        new GridLength(32)
                });


            for (int i = 0; i < headers.Length; i++)
            {
                Border headerBorder =
                    CreateCell(
                        headers[i],
                        true);

                Grid.SetColumn(
                    headerBorder,
                    i);

                Grid.SetRow(
                    headerBorder,
                    0);

                table.Children.Add(
                    headerBorder);
            }


            // ========================================================
            // DATA
            // ========================================================

            int rowIndex = 1;


            foreach (var item in TransactionsGrid.Items)
            {
                dynamic row = item;


                table.RowDefinitions.Add(
                    new RowDefinition
                    {
                        Height =
                            new GridLength(27)
                    });


                string[] data =
{
    ToPersianDate(row.Date),

    row.CustomerName?.ToString() ?? "",

    row.PhoneNumber?.ToString() ?? "",

    FormatNumber(row.Weight, "N2"),

    // PURITY
    row.Purity?.ToString() ?? "",

    FormatNumber(row.CostPerGram, "N0"),

    FormatNumber(row.MakingPercentage, "N2"),

    FormatMoney(row.MakingCost),

    FormatNumber(row.SellerPercentage, "N2"),

    FormatMoney(row.SellerCost),

    FormatNumber(row.TaxPercentage, "N2"),

    FormatMoney(row.Tax),

    FormatMoney(row.FinalCost),

    ToPersianDateTime(row.CreatedAt)

};


                for (int i = 0; i < data.Length; i++)
                {
                    Border cell =
                        CreateCell(
                            data[i],
                            false);


                    // Alternate rows
                    if (rowIndex % 2 == 0)
                    {
                        cell.Background =
                            new SolidColorBrush(
                                Color.FromRgb(
                                    248,
                                    248,
                                    248));
                    }


                    Grid.SetColumn(
                        cell,
                        i);

                    Grid.SetRow(
                        cell,
                        rowIndex);

                    table.Children.Add(
                        cell);
                }


                rowIndex++;
            }


            Grid.SetRow(
                table,
                2);

            container.Children.Add(
                table);


            // ========================================================
            // ADD CONTAINER TO PAGE
            // ========================================================

            FixedPage.SetLeft(
                container,
                0);

            FixedPage.SetTop(
                container,
                0);

            page.Children.Add(
                container);


            // ========================================================
            // ADD PAGE TO DOCUMENT
            // ========================================================

            PageContent pageContent =
                new PageContent();

            ((IAddChild)pageContent)
                .AddChild(page);

            document.Pages.Add(
                pageContent);


            // ========================================================
            // PRINT
            // ========================================================

            printDialog.PrintDocument(
                document.DocumentPaginator,
                "گزارش تاریخچه معاملات طلا");
        }


        // ============================================================
        // CREATE PRINT CELL
        // ============================================================

        private Border CreateCell(
            string text,
            bool isHeader)
        {
            TextBlock textBlock =
                new TextBlock
                {
                    Text = text,

                    FontFamily =
                        new FontFamily("Tahoma"),

                    FontSize =
                        isHeader ? 8.5 : 7.5,

                    FontWeight =
                        isHeader
                            ? FontWeights.Bold
                            : FontWeights.Normal,

                    TextAlignment =
                        TextAlignment.Center,

                    VerticalAlignment =
                        VerticalAlignment.Center,

                    HorizontalAlignment =
                        HorizontalAlignment.Stretch,

                        TextWrapping = TextWrapping.Wrap,

                    Padding =
                        new Thickness(2),

                    FlowDirection =
                        FlowDirection.RightToLeft
                };


            Border border =
                new Border
                {
                    BorderBrush =
                        Brushes.Gray,

                    BorderThickness =
                        new Thickness(
                            0.5),

                    Background =
                        isHeader
                            ? Brushes.LightGray
                            : Brushes.White,

                    Child =
                        textBlock
                };


            return border;
        }


        // ============================================================
        // MONEY FORMAT
        // ============================================================

        private string FormatMoney(
            object? value)
        {
            if (value == null)
                return "0";

            try
            {
                decimal number =
                    Convert.ToDecimal(value);

                return number.ToString(
                    "N0",
                    PersianCulture);
            }
            catch
            {
                return "0";
            }
        }


        // ============================================================
        // NUMBER FORMAT
        // ============================================================

        private string FormatNumber(
            object? value,
            string format)
        {
            if (value == null)
                return "0";

            try
            {
                decimal number =
                    Convert.ToDecimal(value);

                return number.ToString(
                    format,
                    PersianCulture);
            }
            catch
            {
                return "0";
            }
        }


        // ============================================================
        // PERSIAN DATE
        // ============================================================

        private string ToPersianDate(
            DateTime date)
        {
            int year =
                _persianCalendar.GetYear(date);

            int month =
                _persianCalendar.GetMonth(date);

            int day =
                _persianCalendar.GetDayOfMonth(date);


            return
                $"{year:0000}/{month:00}/{day:00}";
        }


        // ============================================================
        // PERSIAN DATE + TIME
        // ============================================================

        private string ToPersianDateTime(
            DateTime date)
        {
            int year =
                _persianCalendar.GetYear(date);

            int month =
                _persianCalendar.GetMonth(date);

            int day =
                _persianCalendar.GetDayOfMonth(date);


            return
                $"{year:0000}/{month:00}/{day:00} " +
                $"{date:HH:mm}";
        }
    }
}
