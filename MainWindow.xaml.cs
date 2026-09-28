using System.ComponentModel;
using System.Windows;
using MarketPOS.Arayuzler;
using MarketPOS.Servisler;

namespace MarketPOS
{
    public partial class MainWindow : Window
    {
        // Singleton sayfalar (Hızlı geçiş ve durumu korumak için)
        private SatisUserControl _satisEkrani;
        private readonly OtomatikYedeklemeServisi _yedekServisi;
        
        public MainWindow()
        {
            InitializeComponent();
            _yedekServisi = new OtomatikYedeklemeServisi();
            _yedekServisi.ZamanlayiciyiBaslat();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Başlangıçta satış ekranını yükle
            _satisEkrani = new SatisUserControl();
            AnaIcerik.Content = _satisEkrani;
        }

        private void NavSatis_Click(object sender, RoutedEventArgs e)
        {
            if (_satisEkrani == null) _satisEkrani = new SatisUserControl();
            AnaIcerik.Content = _satisEkrani;
        }

        private StokYonetimUserControl _stokEkrani;

        private void NavStok_Click(object sender, RoutedEventArgs e)
        {
            if (_stokEkrani == null) _stokEkrani = new StokYonetimUserControl();
            _stokEkrani.UrunleriYukle();
            AnaIcerik.Content = _stokEkrani;
        }

        private MusteriTakipUserControl _musteriEkrani;

        private void NavMusteri_Click(object sender, RoutedEventArgs e)
        {
            if (_musteriEkrani == null) _musteriEkrani = new MusteriTakipUserControl();
            // Ekran açıldığında verileri yenile
            _musteriEkrani.MusterileriYukle(); 
            AnaIcerik.Content = _musteriEkrani;
        }

        private KasaRaporUserControl _kasaEkrani;

        private void NavKasa_Click(object sender, RoutedEventArgs e)
        {
            if (_kasaEkrani == null) _kasaEkrani = new KasaRaporUserControl();
            _kasaEkrani.RaporlariYukle();
            AnaIcerik.Content = _kasaEkrani;
        }

        private ToptanciUserControl _toptanciEkrani;

        private void NavToptanci_Click(object sender, RoutedEventArgs e)
        {
            if (_toptanciEkrani == null) _toptanciEkrani = new ToptanciUserControl();
            _toptanciEkrani.ToptancilariYukle();
            AnaIcerik.Content = _toptanciEkrani;
        }

        private GiderUserControl _giderEkrani;

        private void NavGider_Click(object sender, RoutedEventArgs e)
        {
            if (_giderEkrani == null) _giderEkrani = new GiderUserControl();
            _giderEkrani.GiderleriYukle();
            AnaIcerik.Content = _giderEkrani;
        }

        private AyarlarUserControl _ayarlarEkrani;

        private void NavAyarlar_Click(object sender, RoutedEventArgs e)
        {
            if (_ayarlarEkrani == null) _ayarlarEkrani = new AyarlarUserControl();
            AnaIcerik.Content = _ayarlarEkrani;
        }

        private void NavHesapMakinesi_Click(object sender, RoutedEventArgs e)
        {
            HesapMakinesiAc();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F6)
            {
                HesapMakinesiAc();
                e.Handled = true;
            }
        }

        private void HesapMakinesiAc()
        {
            HesapMakinesiWindow pencere = new HesapMakinesiWindow();
            pencere.Owner = this;
            pencere.ShowDialog();
        }

        private void NavCikis_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Uygulama kapatılırken veritabanı yedeği al
            _yedekServisi.YedekAl();
        }
    }
}