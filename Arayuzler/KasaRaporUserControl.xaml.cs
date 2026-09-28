using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;
using Dapper;

using MarketPOS.Servisler;

namespace MarketPOS.Arayuzler
{
    public partial class KasaRaporUserControl : UserControl
    {
        private readonly VeritabaniYoneticisi _db;
        private readonly YaziciServisi _yaziciServisi;

        public KasaRaporUserControl()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
            _yaziciServisi = new YaziciServisi();
        }

        private void BtnAnaMenu_Click(object sender, RoutedEventArgs e)
        {
            AnaMenuWindow.MenuyuGoster();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            RaporlariYukle();
        }

        public void RaporlariYukle()
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();
                
                var bugun = DateTime.Today;
                var yarin = bugun.AddDays(1);

                // 1. Satışları getir
                string satisSorgu = "SELECT * FROM Satislar WHERE Tarih >= @Bugun AND Tarih < @Yarin ORDER BY Tarih DESC";
                var satislar = baglanti.Query<Satis>(satisSorgu, new { Bugun = bugun, Yarin = yarin }).ToList();
                dgSatislar.ItemsSource = satislar;

                // 2. Kasa özetlerini hesapla (İptal edilenler hariç)
                var gecerliSatislar = satislar.Where(s => !s.IptalEdildi).ToList();
                decimal ciro = gecerliSatislar.Sum(s => s.ToplamTutar);
                decimal nakit = gecerliSatislar.Where(s => s.OdemeTuru == "Nakit").Sum(s => s.ToplamTutar);
                decimal krediKarti = gecerliSatislar.Where(s => s.OdemeTuru == "Kredi Kartı").Sum(s => s.ToplamTutar);
                decimal veresiye = gecerliSatislar.Where(s => s.OdemeTuru == "Veresiye").Sum(s => s.ToplamTutar);

                txtCiro.Text = $"{ciro:N2} ₺";
                txtNakit.Text = $"{nakit:N2} ₺";
                txtKrediKarti.Text = $"{krediKarti:N2} ₺";
                txtVeresiye.Text = $"{veresiye:N2} ₺";

                // 3. Bugünkü Giderleri ve Net Kar/Zarar hesabını yap
                string giderSorgu = "SELECT SUM(Tutar) FROM Giderler WHERE Tarih >= @Bugun AND Tarih < @Yarin";
                decimal bugunkuGiderler = baglanti.ExecuteScalar<decimal?>(giderSorgu, new { Bugun = bugun, Yarin = yarin }) ?? 0m;

                decimal netKarZarar = ciro - bugunkuGiderler;
                txtNetKarZarar.Text = $"{netKarZarar:N2} ₺";
            }
            
            dgSatisDetay.ItemsSource = null;
        }

        private void dgSatislar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgSatislar.SelectedItem is Satis seciliSatis)
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    string sorgu = "SELECT * FROM SatisDetaylari WHERE SatisId = @SatisId";
                    var detaylar = baglanti.Query<SatisDetay>(sorgu, new { SatisId = seciliSatis.Id }).ToList();
                    dgSatisDetay.ItemsSource = detaylar;
                }
            }
            else
            {
                dgSatisDetay.ItemsSource = null;
            }
        }

        private void BtnFisYazdir_Click(object sender, RoutedEventArgs e)
        {
            if (dgSatislar.SelectedItem is Satis seciliSatis)
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    string sorgu = "SELECT * FROM SatisDetaylari WHERE SatisId = @SatisId";
                    var detaylar = baglanti.Query<SatisDetay>(sorgu, new { SatisId = seciliSatis.Id }).ToList();

                    bool basarili = _yaziciServisi.FisYazdir(seciliSatis, detaylar);
                    if (basarili)
                    {
                        MessageBox.Show("Fiş tekrar yazıcıya gönderildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Fiş yazdırılırken bir sorun oluştu.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            else
            {
                MessageBox.Show("Lütfen fiş yazdırmak için bir satış seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnIade_Click(object sender, RoutedEventArgs e)
        {
            if (dgSatislar.SelectedItem is Satis seciliSatis)
            {
                if (seciliSatis.IptalEdildi)
                {
                    MessageBox.Show("Bu satış zaten iptal edilmiş.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var cevap = MessageBox.Show($"'{seciliSatis.FisNo}' numaralı satışı iptal/iade etmek istediğinize emin misiniz? Bu işlem stokları geri yükleyecektir.", "Satış İptal", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (cevap == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var baglanti = _db.BaglantiGetir())
                        {
                            baglanti.Open();
                            using (var islem = baglanti.BeginTransaction())
                            {
                                // 1. Detayları bul ve stokları geri ekle
                                var detaylar = baglanti.Query<SatisDetay>("SELECT * FROM SatisDetaylari WHERE SatisId = @SatisId", new { SatisId = seciliSatis.Id }).ToList();
                                foreach(var detay in detaylar)
                                {
                                    baglanti.Execute("UPDATE Urunler SET StokMiktari = StokMiktari + @Adet WHERE Id = @UrunId", new { Adet = detay.Adet, UrunId = detay.UrunId }, islem);
                                }

                                // 2. Veresiye ise müşteri borcunu düş ve işlemi iptal et
                                if (seciliSatis.OdemeTuru == "Veresiye")
                                {
                                    // SatisId ile ilişkili işlemi bul
                                    var musteriIslemi = baglanti.QueryFirstOrDefault<Islem>("SELECT * FROM Islemler WHERE SatisId = @SatisId", new { SatisId = seciliSatis.Id }, islem);
                                    if (musteriIslemi != null && musteriIslemi.MusteriId.HasValue)
                                    {
                                        baglanti.Execute("UPDATE Musteriler SET BorcMiktari = BorcMiktari - @Tutar WHERE Id = @MusteriId", new { Tutar = seciliSatis.ToplamTutar, MusteriId = musteriIslemi.MusteriId.Value }, islem);
                                        // İşleme iade açıklamasıyla ters kayıt at veya işlemi sil
                                        baglanti.Execute("DELETE FROM Islemler WHERE Id = @Id", new { Id = musteriIslemi.Id }, islem);
                                    }
                                }

                                // 3. Satışı İptal Edildi olarak işaretle
                                baglanti.Execute("UPDATE Satislar SET IptalEdildi = 1 WHERE Id = @SatisId", new { SatisId = seciliSatis.Id }, islem);

                                islem.Commit();
                            }
                        }
                        MessageBox.Show("Satış başarıyla iptal edildi ve stoklar geri yüklendi.", "İptal Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                        RaporlariYukle();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"İptal işlemi sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Lütfen iptal etmek için bir satış seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
