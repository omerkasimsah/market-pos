using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MarketPOS.Veritabani;
using Dapper;

namespace MarketPOS.Arayuzler
{
    public class ZRaporSatisModel
    {
        public string SatisNumarasi { get; set; }
        public DateTime Tarih { get; set; }
        public decimal ToplamTutar { get; set; }
        public string OdemeTuru { get; set; }
        public decimal NakitTutar { get; set; }
        public decimal KartTutar { get; set; }
        public decimal VeresiyeTutar { get; set; }
    }

    public partial class ZRaporuWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        private List<ZRaporSatisModel> _satislar;

        public ZRaporuWindow()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
            dpRaporTarihi.SelectedDate = DateTime.Today;
        }

        private void DpRaporTarihi_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Tarih değiştiğinde otomatik rapor oluştur
            if (dpRaporTarihi.SelectedDate.HasValue)
            {
                RaporuOlustur(dpRaporTarihi.SelectedDate.Value);
            }
        }

        private void BtnRaporOlustur_Click(object sender, RoutedEventArgs e)
        {
            if (dpRaporTarihi.SelectedDate.HasValue)
            {
                RaporuOlustur(dpRaporTarihi.SelectedDate.Value);
            }
            else
            {
                MessageBox.Show("Lütfen bir tarih seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RaporuOlustur(DateTime tarih)
        {
            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();

                    txtRaporTarihi.Text = $"Rapor Tarihi: {tarih:dd.MM.yyyy dddd}";

                    // Z Rapor numarası
                    int zSayisi = baglanti.ExecuteScalar<int>("SELECT COUNT(*) FROM ZRaporlari") + 1;
                    txtRaporNo.Text = $"Z{zSayisi:D3}";

                    // Satışları getir
                    string sql = @"SELECT SatisNumarasi, Tarih, ToplamTutar, OdemeTuru, 
                                   COALESCE(NakitTutar, 0) as NakitTutar, 
                                   COALESCE(KartTutar, 0) as KartTutar, 
                                   COALESCE(VeresiyeTutar, 0) as VeresiyeTutar 
                                   FROM Satislar 
                                   WHERE Date(Tarih) = @Tarih AND IptalEdildi = 0
                                   ORDER BY Tarih ASC";
                    _satislar = baglanti.Query<ZRaporSatisModel>(sql, new { Tarih = tarih.ToString("yyyy-MM-dd") }).ToList();

                    dgSatislar.ItemsSource = _satislar;

                    // Genel istatistikler
                    decimal toplamCiro = _satislar.Sum(x => x.ToplamTutar);
                    int satisAdedi = _satislar.Count;
                    decimal ortalamaSepet = satisAdedi > 0 ? toplamCiro / satisAdedi : 0;

                    txtToplamCiro.Text = $"{toplamCiro:N2} ₺";
                    txtSatisAdedi.Text = satisAdedi.ToString();
                    txtOrtalamaSepet.Text = $"{ortalamaSepet:N2} ₺";

                    // Ödeme dağılımı
                    decimal nakitToplam = _satislar.Sum(x => x.NakitTutar);
                    decimal kartToplam = _satislar.Sum(x => x.KartTutar);
                    decimal veresiyeToplam = _satislar.Sum(x => x.VeresiyeTutar);

                    // Eski satışlarda NakitTutar vs sütunları olmayabilir, OdemeTuru'nden hesapla
                    if (nakitToplam == 0 && kartToplam == 0 && veresiyeToplam == 0)
                    {
                        nakitToplam = _satislar.Where(x => x.OdemeTuru == "Nakit").Sum(x => x.ToplamTutar);
                        kartToplam = _satislar.Where(x => x.OdemeTuru == "Kredi Kartı").Sum(x => x.ToplamTutar);
                        veresiyeToplam = _satislar.Where(x => x.OdemeTuru == "Veresiye").Sum(x => x.ToplamTutar);
                    }

                    txtNakitToplam.Text = $"{nakitToplam:N2} ₺";
                    txtKartToplam.Text = $"{kartToplam:N2} ₺";
                    txtVeresiyeToplam.Text = $"{veresiyeToplam:N2} ₺";

                    // İade toplamı
                    decimal iadeToplam = baglanti.ExecuteScalar<decimal>(
                        "SELECT COALESCE(SUM(IadeTutari), 0) FROM Iadeler WHERE Date(IadeTarihi) = @Tarih",
                        new { Tarih = tarih.ToString("yyyy-MM-dd") });
                    txtIadeToplam.Text = $"{iadeToplam:N2} ₺";

                    // İskonto toplamı (Toplam satış - net tutar farkı, basit hesaplama)
                    txtIskontoToplam.Text = "0.00 ₺"; // İskonto ayrı kaydedilmiyorsa
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rapor oluşturulurken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (_satislar == null || _satislar.Count == 0)
            {
                MessageBox.Show("Kaydedilecek rapor verisi yok. Önce rapor oluşturun.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();

                    int zSayisi = baglanti.ExecuteScalar<int>("SELECT COUNT(*) FROM ZRaporlari") + 1;
                    string raporNo = $"Z{zSayisi:D3}";

                    decimal toplamCiro = _satislar.Sum(x => x.ToplamTutar);
                    decimal nakitToplam = _satislar.Sum(x => x.NakitTutar);
                    decimal kartToplam = _satislar.Sum(x => x.KartTutar);
                    decimal veresiyeToplam = _satislar.Sum(x => x.VeresiyeTutar);

                    if (nakitToplam == 0 && kartToplam == 0 && veresiyeToplam == 0)
                    {
                        nakitToplam = _satislar.Where(x => x.OdemeTuru == "Nakit").Sum(x => x.ToplamTutar);
                        kartToplam = _satislar.Where(x => x.OdemeTuru == "Kredi Kartı").Sum(x => x.ToplamTutar);
                        veresiyeToplam = _satislar.Where(x => x.OdemeTuru == "Veresiye").Sum(x => x.ToplamTutar);
                    }

                    string sql = @"INSERT INTO ZRaporlari (RaporNo, Tarih, ToplamSatisSayisi, ToplamCiro, NakitToplam, KartToplam, VeresiyeToplam, IadeToplam, IskontoToplam, KasaAcilis, KasaKapanis, PersonelAdi)
                                   VALUES (@RaporNo, @Tarih, @ToplamSatisSayisi, @ToplamCiro, @NakitToplam, @KartToplam, @VeresiyeToplam, 0, 0, 0, @KasaKapanis, @PersonelAdi)";

                    baglanti.Execute(sql, new
                    {
                        RaporNo = raporNo,
                        Tarih = dpRaporTarihi.SelectedDate.Value,
                        ToplamSatisSayisi = _satislar.Count,
                        ToplamCiro = toplamCiro,
                        NakitToplam = nakitToplam,
                        KartToplam = kartToplam,
                        VeresiyeToplam = veresiyeToplam,
                        KasaKapanis = nakitToplam,
                        PersonelAdi = Modeller.AktifOturum.AdSoyad ?? "Yönetici"
                    });

                    MessageBox.Show($"Z Raporu {raporNo} başarıyla kaydedildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("UNIQUE"))
                {
                    MessageBox.Show("Bu tarih için Z Raporu zaten kaydedilmiş!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show($"Z Raporu kaydedilirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnYazdir_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Z Raporu yazdırma özelliği, yazıcı ayarlarından yapılandırıldıktan sonra kullanılabilir olacaktır.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
