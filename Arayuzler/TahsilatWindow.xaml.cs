using System;
using System.Windows;
using System.Windows.Threading;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;
using Dapper;

namespace MarketPOS.Arayuzler
{
    public partial class TahsilatWindow : Window
    {
        private readonly Musteri _musteri;
        private readonly string _islemTipi;
        private readonly VeritabaniYoneticisi _db;

        public TahsilatWindow(Musteri musteri, string islemTipi, VeritabaniYoneticisi db)
        {
            InitializeComponent();
            _musteri = musteri;
            _islemTipi = islemTipi;
            _db = db;

            txtBaslik.Text = islemTipi == "Borç Ekle" ? "Müşteriye Borç Ekle" : "Müşteriden Tahsilat Yap";
            txtMusteriBilgi.Text = $"Müşteri: {_musteri.AdSoyad}\nGüncel Bakiye: {_musteri.BorcMiktari:N2} ₺";
            
            if (islemTipi == "Tahsilat")
            {
                txtAciklama.Text = "Nakit Tahsilat";
            }
            else
            {
                txtAciklama.Text = "Elden Borç Verildi";
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                txtTutar.Focus();
            }));
        }

        private void Onayla_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtTutar.Text, out decimal tutar) || tutar <= 0)
            {
                MessageBox.Show("Lütfen geçerli bir tutar giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtTutar.Focus();
                return;
            }

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    using (var islem = baglanti.BeginTransaction())
                    {
                        // 1. İşlem kaydı oluştur
                        string islemSorgu = @"
                            INSERT INTO Islemler (IslemTipi, Aciklama, Tutar, Tarih, MusteriId) 
                            VALUES (@IslemTipi, @Aciklama, @Tutar, @Tarih, @MusteriId)";
                        
                        baglanti.Execute(islemSorgu, new
                        {
                            IslemTipi = _islemTipi == "Borç Ekle" ? "Borçlandırma" : "Tahsilat",
                            Aciklama = txtAciklama.Text.Trim(),
                            Tutar = tutar,
                            Tarih = DateTime.Now,
                            MusteriId = _musteri.Id
                        }, islem);

                        // 2. Müşteri bakiyesini güncelle
                        decimal yeniBakiye = _islemTipi == "Borç Ekle" 
                            ? _musteri.BorcMiktari + tutar 
                            : _musteri.BorcMiktari - tutar;

                        string musteriSorgu = "UPDATE Musteriler SET BorcMiktari = @YeniBakiye WHERE Id = @Id";
                        baglanti.Execute(musteriSorgu, new { YeniBakiye = yeniBakiye, Id = _musteri.Id }, islem);

                        islem.Commit();
                    }
                }

                MessageBox.Show("İşlem başarıyla kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İşlem kaydedilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Iptal_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
