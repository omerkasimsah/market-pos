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

        private void BtnYenile_Click(object sender, RoutedEventArgs e) => RaporlariYukle();

        private void BtnKismiIade_Click(object sender, RoutedEventArgs e)
        {
            var pencere = new IadeIslemWindow(dgSatislar.SelectedItem is Satis secili ? (secili.SatisNumarasi ?? secili.FisNo) : null);
            pencere.Owner = Window.GetWindow(this);
            pencere.ShowDialog();
            RaporlariYukle();
        }

        private void BtnZRaporu_Click(object sender, RoutedEventArgs e)
        {
            var pencere = new ZRaporuWindow { Owner = Window.GetWindow(this) };
            pencere.ShowDialog();
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

            }

            // 2. Kasa özetleri (bölünmüş ödemeler, iadeler ve maliyet dahil - Z raporu ile aynı hesap)
            var ozet = new KasaServisi().Hesapla(DateTime.Today);
            txtCiro.Text = $"{ozet.NetCiro:N2} ₺";
            txtNakit.Text = $"{ozet.Nakit - ozet.NakitIade:N2} ₺";
            txtKrediKarti.Text = $"{ozet.Kart:N2} ₺";
            txtVeresiye.Text = $"{ozet.Veresiye:N2} ₺";

            // 3. Net Kâr = Net Ciro - Satılan Malın Maliyeti - Giderler
            txtNetKarZarar.Text = $"{ozet.NetKar:N2} ₺";
            txtNetKarZarar.ToolTip = $"Net ciro {ozet.NetCiro:N2} - maliyet {ozet.Maliyet:N2} - gider {ozet.Gider:N2}";
            
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
                var fis = new SatisServisi().FisBilgisiGetir(seciliSatis.Id);
                if (fis != null && _yaziciServisi.FisYazdir(fis))
                {
                    MessageBox.Show("Fiş tekrar yazıcıya gönderildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Fiş yazdırılamadı. Ayarlar ekranından fiş yazıcısının seçili olduğunu kontrol edin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                var cevap = MessageBox.Show($"'{seciliSatis.SatisNumarasi ?? seciliSatis.FisNo}' numaralı satışı tamamen iptal etmek istediğinize emin misiniz? Stoklar geri yüklenecek, veresiye kısmı müşteri borcundan düşülecektir.", "Satış İptal", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (cevap == MessageBoxResult.Yes)
                {
                    try
                    {
                        new SatisServisi().SatisIptalEt(seciliSatis.Id);
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
