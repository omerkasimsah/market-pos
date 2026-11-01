using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Dapper;
using MarketPOS.Modeller;
using MarketPOS.Servisler;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public class UrunSatismRaporuKalemi
    {
        public string Barkod { get; set; }
        public string UrunAdi { get; set; }
        public decimal SatilanAdet { get; set; }
        public decimal ToplamCiro { get; set; }
        public decimal ToplamKar { get; set; }
    }

    public class FisSatisModeli
    {
        public int Id { get; set; }
        public string FisNo { get; set; }
        public decimal ToplamTutar { get; set; }
        public string OdemeTuru { get; set; }
        public DateTime Tarih { get; set; }
        public bool IptalEdildi { get; set; }
    }

    public partial class RaporlarWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        private readonly YaziciServisi _yaziciServisi;
        private FisSatisModeli _seciliFis;
        private List<SatisDetay> _seciliFisDetaylari;

        public RaporlarWindow()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
            _yaziciServisi = new YaziciServisi();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dpBaslangic.SelectedDate = DateTime.Today;
            dpBitis.SelectedDate = DateTime.Today;
            UrunRaporunuYukle();
        }

        #region TAB 1: Ürün Bazlı Satış Raporu

        private void BtnRaporBugun_Click(object sender, RoutedEventArgs e)
        {
            dpBaslangic.SelectedDate = DateTime.Today;
            dpBitis.SelectedDate = DateTime.Today;
            UrunRaporunuYukle();
        }

        private void BtnRaporHafta_Click(object sender, RoutedEventArgs e)
        {
            dpBaslangic.SelectedDate = DateTime.Today.AddDays(-7);
            dpBitis.SelectedDate = DateTime.Today;
            UrunRaporunuYukle();
        }

        private void BtnRaporAy_Click(object sender, RoutedEventArgs e)
        {
            dpBaslangic.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpBitis.SelectedDate = DateTime.Today;
            UrunRaporunuYukle();
        }

        private void BtnRaporSorgula_Click(object sender, RoutedEventArgs e)
        {
            UrunRaporunuYukle();
        }

        private void UrunRaporunuYukle()
        {
            DateTime baslangic = dpBaslangic.SelectedDate ?? DateTime.Today;
            DateTime bitis = (dpBitis.SelectedDate ?? DateTime.Today).AddDays(1).AddTicks(-1);

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();

                    string sorgu = @"
                        SELECT 
                            u.Barkod,
                            sd.UrunAdi,
                            SUM(sd.Adet) AS SatilanAdet,
                            SUM(sd.ToplamFiyat) AS ToplamCiro,
                            SUM(sd.ToplamFiyat - (sd.Adet * COALESCE(u.AlisFiyati, 0))) AS ToplamKar
                        FROM SatisDetaylari sd
                        INNER JOIN Satislar s ON sd.SatisId = s.Id
                        LEFT JOIN Urunler u ON sd.UrunId = u.Id
                        WHERE s.Tarih >= @Baslangic AND s.Tarih <= @Bitis AND s.IptalEdildi = 0
                        GROUP BY u.Barkod, sd.UrunAdi
                        ORDER BY SatilanAdet DESC";

                    var liste = baglanti.Query<UrunSatismRaporuKalemi>(sorgu, new { Baslangic = baslangic, Bitis = bitis }).ToList();
                    dgUrunRaporu.ItemsSource = liste;

                    decimal toplamAdet = liste.Sum(x => x.SatilanAdet);
                    decimal toplamCiro = liste.Sum(x => x.ToplamCiro);
                    decimal toplamKar = liste.Sum(x => x.ToplamKar);

                    txtRaporToplamAdet.Text = $"{toplamAdet:N2} Adet";
                    txtRaporToplamCiro.Text = $"{toplamCiro:N2} ₺";
                    txtRaporToplamKar.Text = $"{toplamKar:N2} ₺";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rapor yüklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCsvAktar_Click(object sender, RoutedEventArgs e)
        {
            if (dgUrunRaporu.ItemsSource is IEnumerable<UrunSatismRaporuKalemi> liste && liste.Any())
            {
                try
                {
                    string masaustu = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string dosyaYolu = Path.Combine(masaustu, $"Urun_Satis_Raporu_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Barkod;Ürün Adı;Satılan Adet;Toplam Ciro (TL);Tahmini Net Kar (TL)");

                    foreach (var item in liste)
                    {
                        sb.AppendLine($"{item.Barkod};{item.UrunAdi};{item.SatilanAdet};{item.ToplamCiro};{item.ToplamKar}");
                    }

                    File.WriteAllText(dosyaYolu, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Rapor başarıyla masaüstüne kaydedildi:\n{dosyaYolu}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"CSV dışa aktarılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Dışa aktarılacak rapor verisi bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region TAB 2: Fiş Bulma & İade

        private void BtnFisAra_Click(object sender, RoutedEventArgs e)
        {
            string fisNo = txtFisNoArama.Text.Trim();
            if (string.IsNullOrEmpty(fisNo))
            {
                MessageBox.Show("Lütfen aranacak Fiş Numarasını giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();

                    _seciliFis = baglanti.QueryFirstOrDefault<FisSatisModeli>("SELECT * FROM Satislar WHERE FisNo = @FisNo", new { FisNo = fisNo });

                    if (_seciliFis != null)
                    {
                        _seciliFisDetaylari = baglanti.Query<SatisDetay>("SELECT * FROM SatisDetaylari WHERE SatisId = @SatisId", new { SatisId = _seciliFis.Id }).ToList();

                        lblFisNo.Text = $"Fiş No: {_seciliFis.FisNo}";
                        lblFisTarih.Text = $"Tarih: {_seciliFis.Tarih:dd.MM.yyyy HH:mm}";
                        lblFisOdemeTuru.Text = $"Ödeme Türü: {_seciliFis.OdemeTuru}";
                        lblFisToplamTutar.Text = $"Toplam Tutar: {_seciliFis.ToplamTutar:N2} ₺";
                        lblFisDurum.Text = _seciliFis.IptalEdildi ? "DURUM: İPTAL / İADE EDİLDİ" : "DURUM: GEÇERLİ SATIŞ";
                        lblFisDurum.Foreground = _seciliFis.IptalEdildi ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Green;

                        dgFisDetay.ItemsSource = _seciliFisDetaylari;

                        btnFisYazdir.IsEnabled = true;
                        btnFisIade.IsEnabled = !_seciliFis.IptalEdildi;
                    }
                    else
                    {
                        MessageBox.Show("Belirtilen Fiş Numarasına ait kayıt bulunamadı.", "Bulunamadı", MessageBoxButton.OK, MessageBoxImage.Information);
                        TemizleFisDetaylari();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fiş aranırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TemizleFisDetaylari()
        {
            _seciliFis = null;
            _seciliFisDetaylari = null;
            lblFisNo.Text = "Fiş No: -";
            lblFisTarih.Text = "Tarih: -";
            lblFisOdemeTuru.Text = "Ödeme Türü: -";
            lblFisToplamTutar.Text = "Toplam Tutar: 0.00 ₺";
            lblFisDurum.Text = "Durum: -";
            dgFisDetay.ItemsSource = null;
            btnFisYazdir.IsEnabled = false;
            btnFisIade.IsEnabled = false;
        }

        private void BtnFisYazdir_Click(object sender, RoutedEventArgs e)
        {
            if (_seciliFis != null)
            {
                var fis = new SatisServisi().FisBilgisiGetir(_seciliFis.Id);
                if (fis != null && _yaziciServisi.FisYazdir(fis))
                    MessageBox.Show("Fiş tekrar yazıcıya gönderildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("Fiş yazdırılamadı. Ayarlar ekranından fiş yazıcısının seçili olduğunu kontrol edin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnFisIade_Click(object sender, RoutedEventArgs e)
        {
            if (_seciliFis == null || _seciliFis.IptalEdildi) return;

            var cevap = MessageBox.Show($"Fiş No: {_seciliFis.FisNo}\nToplam Tutar: {_seciliFis.ToplamTutar:N2} ₺\n\nBu satışı tamamen İPTAL etmek istediğinize emin misiniz? Ürün stokları geri eklenecek, veresiye ise müşteri borcundan düşülecektir.", "Satış İptali Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (cevap == MessageBoxResult.Yes)
            {
                try
                {
                    new SatisServisi().SatisIptalEt(_seciliFis.Id);
                    MessageBox.Show("Satış iptal edildi ve stoklar geri yüklendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    BtnFisAra_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Satış iade edilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        private void BtnKapat_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
