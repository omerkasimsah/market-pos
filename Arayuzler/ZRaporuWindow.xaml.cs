using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketPOS.Modeller;
using MarketPOS.Servisler;
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

    public class ZKayit { public string RaporNo { get; set; } public decimal KasaAcilis { get; set; } public decimal SayilanNakit { get; set; } }

    public partial class ZRaporuWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        private readonly KasaServisi _kasaServisi = new KasaServisi();
        private GunOzeti _ozet;
        private bool _kayitli;

        public ZRaporuWindow()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
            dpRaporTarihi.SelectedDate = DateTime.Today;
        }

        private void DpRaporTarihi_Changed(object sender, SelectionChangedEventArgs e)
        {
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

        private static decimal? TutarOku(string metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return null;
            metin = metin.Replace("₺", "").Trim();
            if (decimal.TryParse(metin, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal sonuc)) return sonuc;
            if (decimal.TryParse(metin.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out sonuc)) return sonuc;
            return null;
        }

        private void RaporuOlustur(DateTime tarih)
        {
            try
            {
                _ozet = _kasaServisi.Hesapla(tarih);
                txtRaporTarihi.Text = $"Rapor Tarihi: {tarih:dd.MM.yyyy dddd}";

                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    string gun = tarih.ToString("yyyy-MM-dd");

                    // Bu gün için kaydedilmiş Z raporu var mı?
                    var kayit = baglanti.QueryFirstOrDefault<ZKayit>(
                        "SELECT RaporNo, KasaAcilis, SayilanNakit FROM ZRaporlari WHERE GunTarihi = @Gun OR (GunTarihi IS NULL AND Date(Tarih) = @Gun) LIMIT 1",
                        new { Gun = gun });
                    _kayitli = kayit != null;

                    if (_kayitli)
                    {
                        txtRaporNo.Text = kayit.RaporNo + " ✔";
                        txtKasaAcilis.Text = kayit.KasaAcilis.ToString("N2");
                        decimal sayilan = kayit.SayilanNakit;
                        txtSayilanNakit.Text = sayilan > 0 ? sayilan.ToString("N2") : "";
                    }
                    else
                    {
                        int zSayisi = baglanti.ExecuteScalar<int>("SELECT COUNT(*) FROM ZRaporlari") + 1;
                        txtRaporNo.Text = $"Z{zSayisi:D4}";

                        // Kasa açılışı varsayılan olarak bir önceki Z raporunun sayılan nakdidir (devir)
                        decimal devir = baglanti.ExecuteScalar<decimal?>(
                            "SELECT COALESCE(NULLIF(SayilanNakit,0), KasaKapanis) FROM ZRaporlari WHERE COALESCE(GunTarihi, Date(Tarih)) < @Gun ORDER BY COALESCE(GunTarihi, Date(Tarih)) DESC LIMIT 1",
                            new { Gun = gun }) ?? 0;
                        txtKasaAcilis.Text = devir.ToString("N2");
                        txtSayilanNakit.Text = "";
                    }

                    string sql = @"SELECT SatisNumarasi, Tarih, ToplamTutar, OdemeTuru,
                                   COALESCE(NakitTutar, 0) as NakitTutar,
                                   COALESCE(KartTutar, 0) as KartTutar,
                                   COALESCE(VeresiyeTutar, 0) as VeresiyeTutar
                                   FROM Satislar
                                   WHERE Tarih >= @Bas AND Tarih < @Bit AND IptalEdildi = 0
                                   ORDER BY Tarih ASC";
                    dgSatislar.ItemsSource = baglanti.Query<ZRaporSatisModel>(sql, new { Bas = tarih.Date, Bit = tarih.Date.AddDays(1) }).ToList();
                }

                txtToplamCiro.Text = $"{_ozet.NetCiro:N2} ₺";
                txtSatisAdedi.Text = _ozet.SatisAdedi.ToString();
                txtOrtalamaSepet.Text = $"{_ozet.OrtalamaSepet:N2} ₺";
                txtNakitToplam.Text = $"{_ozet.Nakit:N2} ₺";
                txtKartToplam.Text = $"{_ozet.Kart:N2} ₺";
                txtVeresiyeToplam.Text = $"{_ozet.Veresiye:N2} ₺";
                txtIadeToplam.Text = $"{_ozet.IadeToplam:N2} ₺";
                txtIskontoToplam.Text = $"{_ozet.Iskonto:N2} ₺";

                dgKdv.ItemsSource = _ozet.KdvDagilimi;
                txtKarlilik.Text =
                    $"Brüt Ciro     : {_ozet.BrutCiro,12:N2}\n" +
                    $"İade          : {_ozet.IadeToplam,12:N2}\n" +
                    $"Net Ciro      : {_ozet.NetCiro,12:N2}\n" +
                    $"Mal Maliyeti  : {_ozet.Maliyet,12:N2}\n" +
                    $"Brüt Kâr      : {_ozet.BrutKar,12:N2}\n" +
                    $"Giderler      : {_ozet.Gider,12:N2}\n" +
                    $"NET KÂR       : {_ozet.NetKar,12:N2}\n" +
                    $"İptal Fiş     : {_ozet.IptalAdedi,12}";

                KasaSayiminiGuncelle();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rapor oluşturulurken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void KasaSayim_TextChanged(object sender, TextChangedEventArgs e) => KasaSayiminiGuncelle();

        private void KasaSayiminiGuncelle()
        {
            if (_ozet == null || txtKasaFarki == null) return;

            decimal acilis = TutarOku(txtKasaAcilis.Text) ?? 0;
            decimal beklenen = _ozet.BeklenenNakit(acilis);
            txtBeklenenNakit.Text = $"{beklenen:N2} ₺";
            txtBeklenenDetay.Text = $"Açılış {acilis:N2} + Nakit {_ozet.Nakit:N2} + Tahsilat {_ozet.NakitTahsilat:N2} - İade {_ozet.NakitIade:N2} - Gider {_ozet.Gider:N2}";

            decimal? sayilan = TutarOku(txtSayilanNakit.Text);
            if (sayilan.HasValue)
            {
                decimal fark = sayilan.Value - beklenen;
                txtKasaFarki.Text = fark == 0 ? "0,00 ₺ ✔" : $"{fark:+#,##0.00;-#,##0.00} ₺ {(fark > 0 ? "(fazla)" : "(eksik)")}";
                txtKasaFarki.Foreground = fark == 0 ? Brushes.SeaGreen : Brushes.Firebrick;
            }
            else
            {
                txtKasaFarki.Text = "-";
                txtKasaFarki.Foreground = Brushes.DimGray;
            }
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (_ozet == null || !dpRaporTarihi.SelectedDate.HasValue)
            {
                MessageBox.Show("Önce rapor oluşturun.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_kayitli)
            {
                MessageBox.Show("Bu tarih için Z Raporu zaten kaydedilmiş. Tekrar yazdırabilirsiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal acilis = TutarOku(txtKasaAcilis.Text) ?? 0;
            decimal? sayilan = TutarOku(txtSayilanNakit.Text);
            if (!sayilan.HasValue)
            {
                MessageBox.Show("Gün sonu kapatmadan önce kasadaki nakdi sayıp 'Sayılan Nakit' alanına giriniz.", "Kasa Sayımı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtSayilanNakit.Focus();
                return;
            }

            decimal beklenen = _ozet.BeklenenNakit(acilis);
            decimal fark = sayilan.Value - beklenen;
            if (fark != 0)
            {
                var cevap = MessageBox.Show($"Kasada {Math.Abs(fark):N2} ₺ {(fark > 0 ? "FAZLA" : "EKSİK")} var.\n\nYine de Z raporunu kaydetmek istiyor musunuz?", "Kasa Farkı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (cevap != MessageBoxResult.Yes) return;
            }

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();

                    int zSayisi = baglanti.ExecuteScalar<int>("SELECT COUNT(*) FROM ZRaporlari") + 1;
                    string raporNo = $"Z{zSayisi:D4}";

                    string sql = @"INSERT INTO ZRaporlari (RaporNo, Tarih, GunTarihi, ToplamSatisSayisi, ToplamCiro, NakitToplam, KartToplam, VeresiyeToplam, IadeToplam, IskontoToplam,
                                                          KasaAcilis, KasaKapanis, BeklenenNakit, SayilanNakit, KasaFarki, PersonelAdi)
                                   VALUES (@RaporNo, @Tarih, @GunTarihi, @ToplamSatisSayisi, @ToplamCiro, @NakitToplam, @KartToplam, @VeresiyeToplam, @IadeToplam, @IskontoToplam,
                                           @KasaAcilis, @KasaKapanis, @BeklenenNakit, @SayilanNakit, @KasaFarki, @PersonelAdi)";

                    baglanti.Execute(sql, new
                    {
                        RaporNo = raporNo,
                        Tarih = DateTime.Now,
                        GunTarihi = dpRaporTarihi.SelectedDate.Value.ToString("yyyy-MM-dd"),
                        ToplamSatisSayisi = _ozet.SatisAdedi,
                        ToplamCiro = _ozet.BrutCiro,
                        NakitToplam = _ozet.Nakit,
                        KartToplam = _ozet.Kart,
                        VeresiyeToplam = _ozet.Veresiye,
                        IadeToplam = _ozet.IadeToplam,
                        IskontoToplam = _ozet.Iskonto,
                        KasaAcilis = acilis,
                        KasaKapanis = sayilan.Value,
                        BeklenenNakit = beklenen,
                        SayilanNakit = sayilan.Value,
                        KasaFarki = fark,
                        PersonelAdi = AktifOturum.AdSoyad ?? "Yönetici"
                    });

                    LogServisi.Bilgi($"Z raporu kaydedildi: {raporNo} ciro {_ozet.NetCiro:N2}, kasa farkı {fark:N2}");
                    MessageBox.Show($"Z Raporu {raporNo} başarıyla kaydedildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Gün sonunda otomatik yedek
                new OtomatikYedeklemeServisi().ArkaPlandaYedekle();
                RaporuOlustur(dpRaporTarihi.SelectedDate.Value);
                BtnYazdir_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Z Raporu kaydedilirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnYazdir_Click(object sender, RoutedEventArgs e)
        {
            if (_ozet == null) return;
            if (!YaziciServisi.YaziciTanimli)
            {
                MessageBox.Show("Fiş yazıcısı seçilmemiş. Ayarlar > Donanım Ayarları ekranından yazıcıyı seçin.", "Yazıcı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string metin = _kasaServisi.ZRaporuMetni(_ozet, txtRaporNo.Text.Replace(" ✔", ""), TutarOku(txtKasaAcilis.Text) ?? 0, TutarOku(txtSayilanNakit.Text));
            if (!new YaziciServisi().MetinYazdir(metin))
            {
                MessageBox.Show("Z raporu yazıcıya gönderilemedi.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
