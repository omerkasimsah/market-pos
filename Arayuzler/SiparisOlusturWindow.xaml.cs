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
                string sorgu = $"SELECT * FROM Urunler WHERE StokMiktari <= {MarketPOS.Servisler.AyarServisi.OkuDecimal(MarketPOS.Servisler.AyarServisi.KritikStokSeviyesi, 5).ToString(System.Globalization.CultureInfo.InvariantCulture)} ORDER BY Firma, UrunAdi";
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

            var sb = new System.Text.StringBuilder();
            string cizgi = new string('-', YaziciServisi.FisGenisligiKarakter);
            sb.AppendLine(YaziciServisi.OrtalaSatir("TOPTANCI SİPARİŞ LİSTESİ"));
            sb.AppendLine($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine(cizgi);
            foreach (var grup in _kritikUrunler.GroupBy(u => string.IsNullOrWhiteSpace(u.Firma) ? "Firma belirtilmemiş" : u.Firma))
            {
                sb.AppendLine($"[{grup.Key}]");
                foreach (var u in grup)
                {
                    sb.AppendLine(YaziciServisi.IkiSutun("  " + u.UrunAdi, $"stok {u.StokMiktari:0.###}"));
                }
            }
            sb.AppendLine(cizgi);

            if (new YaziciServisi().MetinYazdir(sb.ToString()))
                MessageBox.Show("Kritik Stok Sipariş Listesi yazıcıya gönderildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("Liste yazdırılamadı. Ayarlar ekranından fiş yazıcısını seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
