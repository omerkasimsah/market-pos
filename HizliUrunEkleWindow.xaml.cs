using System;
using System.Windows;
using System.Windows.Threading;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;
using Dapper;

namespace MarketPOS
{
    public partial class HizliUrunEkleWindow : Window
    {
        public Urun EklenenUrun { get; private set; }
        private readonly string _okutulanBarkod;
        private readonly VeritabaniYoneticisi _db;

        public HizliUrunEkleWindow(string barkod, VeritabaniYoneticisi db)
        {
            InitializeComponent();
            _okutulanBarkod = barkod;
            _db = db;
            txtBarkod.Text = barkod;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Pencere yüklendiğinde imleci otomatik olarak Ürün Adı kutusuna koy.
            // WPF'te bu işlemin düzgün çalışması için Dispatcher kullanmak daha garantilidir.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                txtUrunAdi.Focus();
                if (!string.IsNullOrEmpty(txtUrunAdi.Text))
                {
                    txtUrunAdi.SelectAll();
                }
            }));
        }

        private void Kaydet_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUrunAdi.Text))
            {
                MessageBox.Show("Lütfen ürün adını giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtUrunAdi.Focus();
                return;
            }

            if (!decimal.TryParse(txtSatisFiyati.Text, out decimal fiyat) || fiyat < 0)
            {
                MessageBox.Show("Geçerli bir satış fiyatı giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtSatisFiyati.Focus();
                return;
            }

            decimal stok = 0;
            if (!string.IsNullOrWhiteSpace(txtStokMiktari.Text))
            {
                if (!decimal.TryParse(txtStokMiktari.Text, out stok) || stok < 0)
                {
                    MessageBox.Show("Geçerli bir başlangıç stoku giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtStokMiktari.Focus();
                    return;
                }
            }

            var yeniUrun = new Urun
            {
                Barkod = _okutulanBarkod,
                UrunAdi = txtUrunAdi.Text.Trim(),
                SatisFiyati = fiyat,
                StokMiktari = stok,
                OlusturulmaTarihi = DateTime.Now,
                GuncellenmeTarihi = DateTime.Now
            };

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    string sorgu = @"
                        INSERT INTO Urunler (Barkod, UrunAdi, SatisFiyati, StokMiktari, OlusturulmaTarihi, GuncellenmeTarihi) 
                        VALUES (@Barkod, @UrunAdi, @SatisFiyati, @StokMiktari, @OlusturulmaTarihi, @GuncellenmeTarihi);
                        SELECT last_insert_rowid();";
                        
                    yeniUrun.Id = baglanti.ExecuteScalar<int>(sorgu, yeniUrun);
                }
                
                EklenenUrun = yeniUrun;
                this.DialogResult = true; // Modal pencerenin başarılı kapandığını bildirir
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ürün kaydedilirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Iptal_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
