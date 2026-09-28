using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MarketPOS.Arayuzler
{
    public class BekleyenSatisGorunumModeli
    {
        public string SaatMetni { get; set; }
        public string OzetBaslik { get; set; }
        public string UrunDetayMetni { get; set; }
        public string TutarMetni { get; set; }
        public ParkEdilenSatis OrijinalNesne { get; set; }
    }

    public partial class BekleyenSatislarWindow : Window
    {
        private readonly List<ParkEdilenSatis> _liste;
        public ParkEdilenSatis SecilenSatis { get; private set; }

        public BekleyenSatislarWindow(List<ParkEdilenSatis> bekleyenler)
        {
            InitializeComponent();
            _liste = bekleyenler;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ListeyiYukle();
        }

        private void ListeyiYukle()
        {
            var gorunumListesi = _liste.Select(x =>
            {
                decimal toplam = x.Urunler.Sum(u => u.ToplamFiyat) - x.Iskonto;
                if (toplam < 0) toplam = 0;

                string detaylar = string.Join(", ", x.Urunler.Select(u => $"{u.Adet:G}x {u.UrunAdi}"));

                return new BekleyenSatisGorunumModeli
                {
                    SaatMetni = x.Tarih.ToString("HH:mm:ss"),
                    OzetBaslik = $"{x.Urunler.Count} Kalem Ürün",
                    UrunDetayMetni = detaylar,
                    TutarMetni = $"Tutar: {toplam:N2} ₺",
                    OrijinalNesne = x
                };
            }).ToList();

            lstBekleyenler.ItemsSource = gorunumListesi;
        }

        private void BtnYukle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BekleyenSatisGorunumModeli model)
            {
                SecilenSatis = model.OrijinalNesne;
                _liste.Remove(model.OrijinalNesne);
                this.DialogResult = true;
                this.Close();
            }
        }

        private void BtnSil_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BekleyenSatisGorunumModeli model)
            {
                var cevap = MessageBox.Show("Bu bekleyen satışı silmek istediğinize emin misiniz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (cevap == MessageBoxResult.Yes)
                {
                    _liste.Remove(model.OrijinalNesne);
                    ListeyiYukle();
                }
            }
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
