using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Dapper;
using MarketPOS.Veritabani;
using MarketPOS.Modeller;

namespace MarketPOS.Arayuzler
{
    public partial class FiyatGorWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        private DispatcherTimer _kapanmaZamanlayici;

        public FiyatGorWindow(VeritabaniYoneticisi db)
        {
            InitializeComponent();
            _db = db;

            // 5 saniye sonra ekranı temizle
            _kapanmaZamanlayici = new DispatcherTimer();
            _kapanmaZamanlayici.Interval = TimeSpan.FromSeconds(5);
            _kapanmaZamanlayici.Tick += KapanmaZamanlayici_Tick;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtBarkod.Focus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.F9)
            {
                this.Close();
            }
        }

        private void TxtBarkod_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string barkod = txtBarkod.Text.Trim();
                txtBarkod.Clear();

                if (!string.IsNullOrEmpty(barkod))
                {
                    FiyatSorgula(barkod);
                }
            }
        }

        private void FiyatSorgula(string barkod)
        {
            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();

                    // Terazi barkodu kontrolü (27,28,29)
                    string orjinalBarkod = barkod;
                    var onekler = MarketPOS.Servisler.AyarServisi.Oku(MarketPOS.Servisler.AyarServisi.TeraziOnekleri).Split(',');
                    if (barkod.Length == 13 && Array.Exists(onekler, o => o.Trim().Length == 2 && barkod.StartsWith(o.Trim())))
                    {
                        barkod = barkod.Substring(2, 5);
                    }

                    var urun = baglanti.QueryFirstOrDefault<Urun>(
                        "SELECT * FROM Urunler WHERE Barkod = @Barkod OR Barkod = @OrjinalBarkod",
                        new { Barkod = barkod, OrjinalBarkod = orjinalBarkod });

                    if (urun != null)
                    {
                        txtUrunAdi.Text = urun.UrunAdi;
                        txtFiyat.Text = $"{urun.SatisFiyati:N2} ₺";
                        txtFiyat.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#2ECC71"); // Yeşil
                    }
                    else
                    {
                        txtUrunAdi.Text = "ÜRÜN BULUNAMADI!";
                        txtFiyat.Text = "---";
                        txtFiyat.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#E74C3C"); // Kırmızı
                    }

                    _kapanmaZamanlayici.Stop();
                    _kapanmaZamanlayici.Start();
                }
            }
            catch (Exception ex)
            {
                MarketPOS.Servisler.LogServisi.Hata("Fiyat sorgulama hatası", ex);
                txtUrunAdi.Text = "HATA OLUŞTU!";
                txtFiyat.Text = "";
            }
        }

        private void KapanmaZamanlayici_Tick(object sender, EventArgs e)
        {
            _kapanmaZamanlayici.Stop();
            txtUrunAdi.Text = "Barkod Bekleniyor...";
            txtFiyat.Text = "0,00 ₺";
            txtFiyat.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#2ECC71");
            txtBarkod.Focus();
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
