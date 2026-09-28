using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Dapper;
using MarketPOS.Servisler;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public partial class AnaMenuWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        private readonly OtomatikYedeklemeServisi _yedekServisi;
        private DispatcherTimer _saatTimer;
        private DispatcherTimer _rozetTimer;

        public static AnaMenuWindow AktifPencere { get; private set; }

        public static void MenuyuGoster()
        {
            if (AktifPencere == null || !AktifPencere.IsLoaded)
            {
                AktifPencere = new AnaMenuWindow();
            }

            if (AktifPencere.WindowState == WindowState.Minimized)
            {
                AktifPencere.WindowState = WindowState.Normal;
            }

            AktifPencere.Show();
            AktifPencere.Activate();
        }

        public AnaMenuWindow()
        {
            InitializeComponent();
            AktifPencere = this;
            _db = new VeritabaniYoneticisi();
            _db.VeritabaniniHazirla();
            _db.TestVerileriEkle();

            _yedekServisi = new OtomatikYedeklemeServisi();
            _yedekServisi.ZamanlayiciyiBaslat();

            ZamanlayicilariBaslat();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RozetiGuncelle();
            YetkileriKontrolEt();
        }

        private void YetkileriKontrolEt()
        {
            if (Modeller.AktifOturum.KullaniciId == 0)
            {
                // Test ortamı vb. için oturum açılmamışsa, varsayılan Yönetici gibi davran
                return;
            }

            if (!Modeller.AktifOturum.YoneticiMi)
            {
                // Kasiyer yetkisindeki kullanıcılara yasak olan butonlar
                btnStokYonetimi.IsEnabled = false;
                btnStokYonetimi.ToolTip = "Bu bölüm sadece Yöneticiler içindir.";

                btnMusteriVeresiye.IsEnabled = false;
                btnMusteriVeresiye.ToolTip = "Bu bölüm sadece Yöneticiler içindir.";

                btnToptancilar.IsEnabled = false;
                btnToptancilar.ToolTip = "Bu bölüm sadece Yöneticiler içindir.";

                btnGiderler.IsEnabled = false;
                btnGiderler.ToolTip = "Bu bölüm sadece Yöneticiler içindir.";

                btnKasaRaporlari.IsEnabled = false;
                btnKasaRaporlari.ToolTip = "Bu bölüm sadece Yöneticiler içindir.";

                btnDetayliRaporlar.IsEnabled = false;
                btnDetayliRaporlar.ToolTip = "Bu bölüm sadece Yöneticiler içindir.";

                btnUyariMerkezi.IsEnabled = false;
                btnUyariMerkezi.ToolTip = "Bu bölüm sadece Yöneticiler içindir.";

                btnAyarlar.IsEnabled = false;
                btnAyarlar.ToolTip = "Bu bölüm sadece Yöneticiler içindir.";
            }
        }

        private void ZamanlayicilariBaslat()
        {
            _saatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _saatTimer.Tick += (s, e) =>
            {
                txtTarihSaat.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            };
            _saatTimer.Start();

            // Rozet sayısını her 30 saniyede bir güncelle
            _rozetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _rozetTimer.Tick += (s, e) => RozetiGuncelle();
            _rozetTimer.Start();
        }

        public void RozetiGuncelle()
        {
            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();

                    // 1. SKT Yaklaşan / Geçen ürün sayısı
                    string sktSql = "SELECT COUNT(*) FROM UrunPartileri WHERE SktTarihi <= @SktSinir AND StokMiktari > 0";
                    int sktSayisi = baglanti.ExecuteScalar<int>(sktSql, new { SktSinir = DateTime.Today.AddDays(15) });

                    // 2. Kritik / Eksi stok sayısı
                    string stokSql = "SELECT COUNT(*) FROM Urunler WHERE StokMiktari <= 5";
                    int stokSayisi = baglanti.ExecuteScalar<int>(stokSql);

                    // 3. 30 Günü geçmiş borçlu veresiye müşteri sayısı
                    string veresiyeSql = "SELECT COUNT(*) FROM Musteriler WHERE BorcMiktari > 0 AND KayitTarihi <= @BorcSinir";
                    int veresiyeSayisi = baglanti.ExecuteScalar<int>(veresiyeSql, new { BorcSinir = DateTime.Today.AddDays(-30) });

                    int toplamRisk = sktSayisi + stokSayisi + veresiyeSayisi;

                    if (toplamRisk > 0)
                    {
                        txtBildirimSayisi.Text = toplamRisk.ToString();
                        brdBadge.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        brdBadge.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Rozet Güncelleme Hatası: " + ex.Message);
            }
        }

        #region Navigasyon Butonları (.Show() Bağımsız Pencere Mimarisi)

        private void BtnSatis_Click(object sender, RoutedEventArgs e)
        {
            SatisWindow pencere = new SatisWindow();
            pencere.Show();
        }

        private void BtnStok_Click(object sender, RoutedEventArgs e)
        {
            StokYonetimWindow pencere = new StokYonetimWindow();
            pencere.Show();
        }

        private void BtnMusteri_Click(object sender, RoutedEventArgs e)
        {
            TopluVeresiyeWindow musteriPenceresi = new TopluVeresiyeWindow();
            musteriPenceresi.ShowDialog();
        }

        private void BtnToptanci_Click(object sender, RoutedEventArgs e)
        {
            ToptanciFaturaWindow toptanciPenceresi = new ToptanciFaturaWindow();
            toptanciPenceresi.ShowDialog();
        }

        private void BtnGider_Click(object sender, RoutedEventArgs e)
        {
            GiderWindow pencere = new GiderWindow();
            pencere.Show();
        }

        private void BtnKasa_Click(object sender, RoutedEventArgs e)
        {
            KasaRaporWindow pencere = new KasaRaporWindow();
            pencere.Show();
        }

        private void BtnRaporlar_Click(object sender, RoutedEventArgs e)
        {
            RaporlarWindow pencere = new RaporlarWindow();
            pencere.Show();
        }

        private void BtnUyarilar_Click(object sender, RoutedEventArgs e)
        {
            UyarilarWindow pencere = new UyarilarWindow();
            pencere.Closed += (s, ev) => RozetiGuncelle();
            pencere.Show();
        }

        private void BtnAyarlar_Click(object sender, RoutedEventArgs e)
        {
            AyarlarWindow pencere = new AyarlarWindow();
            pencere.Show();
        }

        private void BtnCikis_Click(object sender, RoutedEventArgs e)
        {
            _yedekServisi.YedekAl();
            Application.Current.Shutdown();
        }

        #endregion
    }
}
