using System;
using System.Windows;
using System.Windows.Threading;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;
using Dapper;

namespace MarketPOS.Arayuzler
{
    public partial class UrunDuzenleWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        private readonly Urun _mevcutUrun;
        private readonly bool _duzenlemeModu;

        public UrunDuzenleWindow(VeritabaniYoneticisi db, Urun urun = null)
        {
            InitializeComponent();
            _db = db;
            _mevcutUrun = urun;
            _duzenlemeModu = urun != null;

            if (_duzenlemeModu)
            {
                txtBaslik.Text = "Ürünü Düzenle";
                txtBarkod.Text = _mevcutUrun.Barkod;
                txtUrunAdi.Text = _mevcutUrun.UrunAdi;
                txtSatisFiyati.Text = _mevcutUrun.SatisFiyati.ToString();
                txtStokMiktari.Text = _mevcutUrun.StokMiktari.ToString();
                txtKategori.Text = _mevcutUrun.Kategori ?? "";
                txtFirma.Text = _mevcutUrun.Firma ?? "";
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (_duzenlemeModu)
                {
                    txtUrunAdi.Focus();
                }
                else
                {
                    txtBarkod.Focus();
                }
            }));
        }

        private void Kaydet_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBarkod.Text))
            {
                MessageBox.Show("Lütfen barkod giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtBarkod.Focus();
                return;
            }

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

            if (!decimal.TryParse(txtStokMiktari.Text, out decimal stok) || stok < 0)
            {
                MessageBox.Show("Geçerli bir stok miktarı giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtStokMiktari.Focus();
                return;
            }

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();

                    if (_duzenlemeModu)
                    {
                        // Mevcut ürünü güncelle
                        string sorgu = @"
                            UPDATE Urunler 
                            SET Barkod = @Barkod, UrunAdi = @UrunAdi, SatisFiyati = @SatisFiyati, StokMiktari = @StokMiktari, Kategori = @Kategori, Firma = @Firma, GuncellenmeTarihi = @GuncellenmeTarihi
                            WHERE Id = @Id";
                            
                        baglanti.Execute(sorgu, new {
                            Barkod = txtBarkod.Text.Trim(),
                            UrunAdi = txtUrunAdi.Text.Trim(),
                            SatisFiyati = fiyat,
                            StokMiktari = stok,
                            Kategori = txtKategori.Text.Trim(),
                            Firma = txtFirma.Text.Trim(),
                            GuncellenmeTarihi = DateTime.Now,
                            Id = _mevcutUrun.Id
                        });
                    }
                    else
                    {
                        // Aynı barkod var mı kontrol et
                        int sayi = baglanti.ExecuteScalar<int>("SELECT COUNT(*) FROM Urunler WHERE Barkod = @Barkod", new { Barkod = txtBarkod.Text.Trim() });
                        if (sayi > 0)
                        {
                            MessageBox.Show("Bu barkoda sahip bir ürün zaten mevcut!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                            txtBarkod.Focus();
                            return;
                        }

                        // Yeni ekle
                        string sorgu = @"
                            INSERT INTO Urunler (Barkod, UrunAdi, SatisFiyati, StokMiktari, Kategori, Firma, OlusturulmaTarihi, GuncellenmeTarihi) 
                            VALUES (@Barkod, @UrunAdi, @SatisFiyati, @StokMiktari, @Kategori, @Firma, @OlusturulmaTarihi, @GuncellenmeTarihi)";
                            
                        baglanti.Execute(sorgu, new {
                            Barkod = txtBarkod.Text.Trim(),
                            UrunAdi = txtUrunAdi.Text.Trim(),
                            SatisFiyati = fiyat,
                            StokMiktari = stok,
                            Kategori = txtKategori.Text.Trim(),
                            Firma = txtFirma.Text.Trim(),
                            OlusturulmaTarihi = DateTime.Now,
                            GuncellenmeTarihi = DateTime.Now
                        });
                    }
                }
                
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ürün kaydedilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Iptal_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
