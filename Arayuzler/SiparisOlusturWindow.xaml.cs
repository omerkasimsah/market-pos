using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dapper;
using MarketPOS.Modeller;
using MarketPOS.Servisler;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public partial class SiparisOlusturWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        private List<Urun> _kritikUrunler;

        public SiparisOlusturWindow(VeritabaniYoneticisi db)
        {
            InitializeComponent();
            _db = db;
            _kritikUrunler = new List<Urun>();
            KritikUrunleriYukle();
        }

        private void KritikUrunleriYukle()
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();
                string sorgu = "SELECT * FROM Urunler WHERE StokMiktari <= 5 ORDER BY Firma, UrunAdi";
                _kritikUrunler = baglanti.Query<Urun>(sorgu).ToList();
                dgSiparisler.ItemsSource = _kritikUrunler;
            }
        }

        private void BtnYazdir_Click(object sender, RoutedEventArgs e)
        {
            if (_kritikUrunler.Count == 0)
            {
                MessageBox.Show("Sipariş verilecek kritik stokta ürün bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            YaziciServisi yazici = new YaziciServisi();
            List<SepetUrunu> sepet = new List<SepetUrunu>();
            foreach (var u in _kritikUrunler)
            {
                sepet.Add(new SepetUrunu
                {
                    UrunId = u.Id,
                    UrunAdi = $"[{u.Firma}] {u.UrunAdi}",
                    Adet = 10, // Varsayılan sipariş adedi
                    BirimFiyat = u.SatisFiyati
                });
            }

            yazici.FisYazdir("SIPARIS_" + DateTime.Now.ToString("yyMMdd"), DateTime.Now, sepet, 0, "SIPARIS LISTESI", "TOPTANCI SIPARIS FORMU");
            MessageBox.Show("Kritik Stok Sipariş Listesi yazıcıya gönderildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
