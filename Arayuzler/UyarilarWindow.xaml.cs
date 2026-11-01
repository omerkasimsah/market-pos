using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public class SktUyariModeli
    {
        public int HedefId { get; set; }
        public string UrunAdi { get; set; }
        public string PartiNo { get; set; }
        public decimal StokMiktari { get; set; }
        public DateTime SktTarihi { get; set; }
        public int UrunId { get; set; }
    }

    public class UyariKalemi
    {
        public string Kategori { get; set; } // SKT, STOK, VERESİYE
        public string Baslik { get; set; }
        public string Aciklama { get; set; }
        public bool YuksekRisk { get; set; }
        public string EylemMetni { get; set; }
        public int HedefId { get; set; }
    }

    public partial class UyarilarWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        private List<UyariKalemi> _tumUyarilar = new List<UyariKalemi>();
        private string _aktifFiltre = "Tumu";

        public UyarilarWindow()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UyarilariYukle();
        }

        private void UyarilariYukle()
        {
            _tumUyarilar.Clear();

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();

                    // 1. SKT Uyarıları (Son 15 gün veya geçmiş)
                    string sktSorgu = @"
                        SELECT up.Id AS HedefId, u.UrunAdi, up.PartiNo, up.StokMiktari, up.SktTarihi, u.Id AS UrunId
                        FROM UrunPartileri up
                        INNER JOIN Urunler u ON up.UrunId = u.Id
                        WHERE up.SktTarihi <= @SktSinir AND up.StokMiktari > 0
                        ORDER BY up.SktTarihi ASC";

                    var sktListesi = baglanti.Query<SktUyariModeli>(sktSorgu, new { SktSinir = DateTime.Today.AddDays(15) });

                    foreach (var item in sktListesi)
                    {
                        DateTime skt = item.SktTarihi;
                        int kalanGun = (skt.Date - DateTime.Today).Days;
                        bool tarihiGecti = kalanGun < 0;

                        _tumUyarilar.Add(new UyariKalemi
                        {
                            Kategori = "SKT",
                            Baslik = $"{item.UrunAdi} (Parti: {item.PartiNo})",
                            Aciklama = tarihiGecti ? $"🚨 SON KULLANMA TARİHİ {Math.Abs(kalanGun)} GÜN ÖNCE GEÇTİ! (SKT: {skt:dd.MM.yyyy} - Miktar: {item.StokMiktari} Adet)" : $"⏳ SKT'ye son {kalanGun} gün kaldı. (SKT: {skt:dd.MM.yyyy} - Miktar: {item.StokMiktari} Adet)",
                            YuksekRisk = tarihiGecti,
                            EylemMetni = "İndirim Yap / Fiyat Güncelle",
                            HedefId = item.UrunId
                        });
                    }

                    // 2. Kritik & Eksi Stok Uyarıları (Ayarlardaki kritik seviye)
                    string stokSorgu = $"SELECT Id, UrunAdi, StokMiktari FROM Urunler WHERE StokMiktari <= {MarketPOS.Servisler.AyarServisi.OkuDecimal(MarketPOS.Servisler.AyarServisi.KritikStokSeviyesi, 5).ToString(System.Globalization.CultureInfo.InvariantCulture)} ORDER BY StokMiktari ASC";
                    var stokListesi = baglanti.Query<Urun>(stokSorgu);

                    foreach (var item in stokListesi)
                    {
                        bool eksiMi = item.StokMiktari <= 0;

                        _tumUyarilar.Add(new UyariKalemi
                        {
                            Kategori = "STOK",
                            Baslik = item.UrunAdi,
                            Aciklama = eksiMi ? $"🚨 STOK TÜKENDİ / EKSİDE! Mevcut Stok: {item.StokMiktari:G} Adet" : $"⚠️ KRİTİK STOK SEVİYESİ! Mevcut Stok: {item.StokMiktari:G} Adet",
                            YuksekRisk = eksiMi,
                            EylemMetni = "Stok Düzelt / Düzenle",
                            HedefId = item.Id
                        });
                    }

                    // 3. Gecikmiş Veresiye Borç Uyarıları (30 günü geçmiş borçlular)
                    string veresiyeSorgu = "SELECT Id, AdSoyad, BorcMiktari, KayitTarihi FROM Musteriler WHERE BorcMiktari > 0 AND KayitTarihi <= @BorcSinir ORDER BY BorcMiktari DESC";
                    var veresiyeListesi = baglanti.Query<Musteri>(veresiyeSorgu, new { BorcSinir = DateTime.Today.AddDays(-30) });

                    foreach (var item in veresiyeListesi)
                    {
                        _tumUyarilar.Add(new UyariKalemi
                        {
                            Kategori = "VERESİYE",
                            Baslik = item.AdSoyad,
                            Aciklama = $"🚨 30 GÜNÜ GEÇMİŞ VERESİYE BORCU! Toplam Borç: {item.BorcMiktari:N2} ₺",
                            YuksekRisk = true,
                            EylemMetni = "Müşteri Hesabına Git",
                            HedefId = item.Id
                        });
                    }
                }

                FiltreleVeGoster();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Uyarılar yüklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FiltreleVeGoster()
        {
            var liste = _tumUyarilar.AsEnumerable();

            if (_aktifFiltre == "SKT") liste = liste.Where(x => x.Kategori == "SKT");
            else if (_aktifFiltre == "STOK") liste = liste.Where(x => x.Kategori == "STOK");
            else if (_aktifFiltre == "VERESİYE") liste = liste.Where(x => x.Kategori == "VERESİYE");

            lstUyarilar.ItemsSource = liste.ToList();
        }

        private void BtnFiltreTumu_Click(object sender, RoutedEventArgs e) { _aktifFiltre = "Tumu"; FiltreleVeGoster(); }
        private void BtnFiltreSkt_Click(object sender, RoutedEventArgs e) { _aktifFiltre = "SKT"; FiltreleVeGoster(); }
        private void BtnFiltreStok_Click(object sender, RoutedEventArgs e) { _aktifFiltre = "STOK"; FiltreleVeGoster(); }
        private void BtnFiltreVeresiye_Click(object sender, RoutedEventArgs e) { _aktifFiltre = "VERESİYE"; FiltreleVeGoster(); }

        private void BtnEylem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UyariKalemi uyari)
            {
                if (uyari.Kategori == "SKT" || uyari.Kategori == "STOK")
                {
                    using (var baglanti = _db.BaglantiGetir())
                    {
                        baglanti.Open();
                        var urun = baglanti.QueryFirstOrDefault<Urun>("SELECT * FROM Urunler WHERE Id = @Id", new { Id = uyari.HedefId });
                        if (urun != null)
                        {
                            UrunEkleDuzenleWindow pencere = new UrunEkleDuzenleWindow(_db, urun);
                            pencere.Owner = this;
                            if (pencere.ShowDialog() == true)
                            {
                                UyarilariYukle();
                            }
                        }
                    }
                }
                else if (uyari.Kategori == "VERESİYE")
                {
                    MusteriWindow pencere = new MusteriWindow();
                    pencere.Show();
                }
            }
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
