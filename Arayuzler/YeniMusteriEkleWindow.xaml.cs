using System;
using System.Windows;
using System.Windows.Threading;
using MarketPOS.Veritabani;
using Dapper;

namespace MarketPOS.Arayuzler
{
    public partial class YeniMusteriEkleWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;

        public YeniMusteriEkleWindow(VeritabaniYoneticisi db)
        {
            InitializeComponent();
            _db = db;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                txtAdSoyad.Focus();
            }));
        }

        private void Kaydet_Click(object sender, RoutedEventArgs e)
        {
            string adSoyad = txtAdSoyad.Text.Trim();
            string telefon = txtTelefon.Text.Trim();
            // Not: Adresi şimdilik veritabanında kolon olmadığı için telefon içine not alabilir veya sonradan ekleyebiliriz.
            // Fakat plana sadık kalmak adına şimdilik Müşteri tablosunda Adres yoksa bile sadece AdSoyad/Telefon kaydedelim.
            
            if (string.IsNullOrWhiteSpace(adSoyad))
            {
                MessageBox.Show("Lütfen müşteri adını giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtAdSoyad.Focus();
                return;
            }

            decimal borc = 0;
            decimal.TryParse(txtBorc.Text, out borc);

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    using (var islem = baglanti.BeginTransaction())
                    {
                        string sorgu = @"
                            INSERT INTO Musteriler (AdSoyad, Telefon, BorcMiktari, KayitTarihi) 
                            VALUES (@AdSoyad, @Telefon, @BorcMiktari, @KayitTarihi);
                            SELECT last_insert_rowid();";

                        int musteriId = baglanti.ExecuteScalar<int>(sorgu, new 
                        {
                            AdSoyad = adSoyad,
                            Telefon = telefon,
                            BorcMiktari = borc,
                            KayitTarihi = DateTime.Now
                        }, islem);

                        if (borc > 0)
                        {
                            string islemSorgu = @"
                                INSERT INTO Islemler (IslemTipi, Aciklama, Tutar, Tarih, MusteriId) 
                                VALUES ('Borçlandırma', 'Açılış Bakiyesi', @Tutar, @Tarih, @MusteriId)";
                                
                            baglanti.Execute(islemSorgu, new
                            {
                                Tutar = borc,
                                Tarih = DateTime.Now,
                                MusteriId = musteriId
                            }, islem);
                        }

                        islem.Commit();
                    }
                }
                
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Müşteri kaydedilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Iptal_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
