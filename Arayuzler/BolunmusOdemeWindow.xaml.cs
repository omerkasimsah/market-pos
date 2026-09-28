using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public partial class BolunmusOdemeWindow : Window
    {
        private readonly decimal _toplamTutar;
        private readonly VeritabaniYoneticisi _db;

        public decimal NakitTutar { get; private set; }
        public decimal KartTutar { get; private set; }
        public decimal VeresiyeTutar { get; private set; }
        public int? SecilenMusteriId { get; private set; }
        public string SecilenMusteriAdi { get; private set; }

        public BolunmusOdemeWindow(decimal toplamTutar, VeritabaniYoneticisi db)
        {
            InitializeComponent();
            _toplamTutar = toplamTutar;
            _db = db;
            txtToplamTutar.Text = $"{_toplamTutar:N2} ₺";
            txtKalanTutar.Text = $"{_toplamTutar:N2} ₺";
            txtNakitTutar.Focus();
        }

        private void OdemeTutarlari_Changed(object sender, TextChangedEventArgs e)
        {
            decimal nakit = 0, kart = 0, veresiye = 0;
            // CultureInfo ve NumberStyles.Any kullanarak virgül ve nokta karışıklığını önle (Para bölme fix)
            decimal.TryParse(txtNakitTutar.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out nakit);
            decimal.TryParse(txtKartTutar.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out kart);
            decimal.TryParse(txtVeresiyeTutar.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out veresiye);

            decimal kalan = _toplamTutar - nakit - kart - veresiye;
            txtKalanTutar.Text = $"{kalan:N2} ₺";

            if (kalan < -0.01m)
            {
                txtKalanTutar.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            }
            else if (Math.Abs(kalan) < 0.01m)
            {
                txtKalanTutar.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96));
                txtKalanTutar.Text = "✅ 0.00 ₺ (Tamam)";
            }
            else
            {
                txtKalanTutar.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            }

            bool veresiyeGecerli = veresiye <= 0 || SecilenMusteriId.HasValue;
            btnOnayla.IsEnabled = Math.Abs(kalan) < 0.01m && veresiyeGecerli && nakit >= 0 && kart >= 0 && veresiye >= 0;
        }

        private void BtnTumuNakit_Click(object sender, RoutedEventArgs e)
        {
            decimal kart = 0, veresiye = 0;
            decimal.TryParse(txtKartTutar.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out kart);
            decimal.TryParse(txtVeresiyeTutar.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out veresiye);
            decimal kalanNakit = _toplamTutar - kart - veresiye;
            if (kalanNakit < 0) kalanNakit = 0;
            txtNakitTutar.Text = kalanNakit.ToString("F2", CultureInfo.InvariantCulture);
        }

        private void BtnTumuKart_Click(object sender, RoutedEventArgs e)
        {
            decimal nakit = 0, veresiye = 0;
            decimal.TryParse(txtNakitTutar.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out nakit);
            decimal.TryParse(txtVeresiyeTutar.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out veresiye);
            decimal kalanKart = _toplamTutar - nakit - veresiye;
            if (kalanKart < 0) kalanKart = 0;
            txtKartTutar.Text = kalanKart.ToString("F2", CultureInfo.InvariantCulture);
        }

        private void HizliNakit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                if (decimal.TryParse(btn.Tag.ToString(), out decimal miktar))
                {
                    if (miktar == 0)
                    {
                        txtNakitTutar.Text = "0";
                    }
                    else
                    {
                        decimal mevcut = 0;
                        decimal.TryParse(txtNakitTutar.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out mevcut);
                        mevcut += miktar;
                        txtNakitTutar.Text = mevcut.ToString("F2", CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        private void BtnMusteriSec_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MusteriSecimWindow pencere = new MusteriSecimWindow(_db);
                var owner = Window.GetWindow(this);
                if (owner != null && owner.IsLoaded) pencere.Owner = owner;

                if (pencere.ShowDialog() == true && pencere.SecilenMusteri != null)
                {
                    SecilenMusteriId = pencere.SecilenMusteri.Id;
                    SecilenMusteriAdi = pencere.SecilenMusteri.AdSoyad;
                    txtSecilenMusteri.Text = $"📒 Veresiye Müşterisi: {SecilenMusteriAdi}";

                    // Kalan tutarı otomatik veresiyeye yaz
                    decimal nakit = 0, kart = 0;
                    decimal.TryParse(txtNakitTutar.Text, out nakit);
                    decimal.TryParse(txtKartTutar.Text, out kart);
                    decimal kalanVeresiye = _toplamTutar - nakit - kart;
                    if (kalanVeresiye > 0)
                    {
                        txtVeresiyeTutar.Text = kalanVeresiye.ToString("F2");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Müşteri seçim hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOnayla_Click(object sender, RoutedEventArgs e)
        {
            decimal nakit = 0, kart = 0, veresiye = 0;
            decimal.TryParse(txtNakitTutar.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out nakit);
            decimal.TryParse(txtKartTutar.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out kart);
            decimal.TryParse(txtVeresiyeTutar.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out veresiye);

            if (veresiye > 0 && !SecilenMusteriId.HasValue)
            {
                MessageBox.Show("Veresiye tutarı girdiniz ancak müşteri seçmediniz!\nLütfen 'MÜŞTERİ SEÇ' butonuna basarak bir müşteri seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NakitTutar = nakit;
            KartTutar = kart;
            VeresiyeTutar = veresiye;

            this.DialogResult = true;
            this.Close();
        }

        private void BtnIptal_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
