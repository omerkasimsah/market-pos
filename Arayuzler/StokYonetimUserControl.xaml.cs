using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;
using Dapper;

namespace MarketPOS.Arayuzler
{
    public partial class StokYonetimUserControl : UserControl
    {
        private readonly VeritabaniYoneticisi _db;
        private List<Urun> _tumUrunler;
        private string _aktifFiltre = "Tumu"; // Tumu, Bugun, Kritik

        public StokYonetimUserControl()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
            _tumUrunler = new List<Urun>();
        }

        private void BtnAnaMenu_Click(object sender, RoutedEventArgs e)
        {
            AnaMenuWindow.MenuyuGoster();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UrunleriYukle();
        }

        public void UrunleriYukle()
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();
                _tumUrunler = baglanti.Query<Urun>("SELECT * FROM Urunler ORDER BY UrunAdi").ToList();
            }
            KategoriVeFirmaFiltreleriniYukle();
            AramayiUygula();
        }

        private void KategoriVeFirmaFiltreleriniYukle()
        {
            var kategoriler = new List<string> { "Tüm Kategoriler" };
            kategoriler.AddRange(_tumUrunler.Select(u => u.Kategori).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().OrderBy(k => k));

            var firmalar = new List<string> { "Tüm Firmalar" };
            firmalar.AddRange(_tumUrunler.Select(u => u.Firma).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().OrderBy(f => f));

            string seciliKat = cmbKategori.SelectedItem as string;
            string seciliFir = cmbFirma.SelectedItem as string;

            cmbKategori.ItemsSource = kategoriler;
            cmbKategori.SelectedItem = kategoriler.Contains(seciliKat) ? seciliKat : "Tüm Kategoriler";

            cmbFirma.ItemsSource = firmalar;
            cmbFirma.SelectedItem = firmalar.Contains(seciliFir) ? seciliFir : "Tüm Firmalar";
        }

        private void AramayiUygula()
        {
            if (_tumUrunler == null) return;

            var filtreliListe = _tumUrunler.AsEnumerable();

            // 1. Durum Filtresi (Bugün / Kritik / SKT)
            if (_aktifFiltre == "Bugun")
            {
                var bugun = DateTime.Today;
                filtreliListe = filtreliListe.Where(u => u.OlusturulmaTarihi.Date == bugun || u.GuncellenmeTarihi.Date == bugun);
            }
            else if (_aktifFiltre == "Kritik")
            {
                filtreliListe = filtreliListe.Where(u => u.StokMiktari <= 5);
            }
            else if (_aktifFiltre == "Skt")
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    var sktUrunIdler = baglanti.Query<int>("SELECT DISTINCT UrunId FROM UrunPartileri WHERE SktTarihi <= @Sinir AND StokMiktari > 0", new { Sinir = DateTime.Today.AddDays(15) }).ToList();
                    filtreliListe = filtreliListe.Where(u => sktUrunIdler.Contains(u.Id));
                }
            }

            // 2. Kategori Filtresi
            string seciliKategori = cmbKategori?.SelectedItem as string;
            if (!string.IsNullOrEmpty(seciliKategori) && seciliKategori != "Tüm Kategoriler")
            {
                filtreliListe = filtreliListe.Where(u => u.Kategori == seciliKategori);
            }

            // 3. Firma Filtresi
            string seciliFirma = cmbFirma?.SelectedItem as string;
            if (!string.IsNullOrEmpty(seciliFirma) && seciliFirma != "Tüm Firmalar")
            {
                filtreliListe = filtreliListe.Where(u => u.Firma == seciliFirma);
            }

            // 4. Metin Arama Filtresi
            string arama = txtArama.Text.ToLower().Trim();
            if (!string.IsNullOrEmpty(arama))
            {
                filtreliListe = filtreliListe.Where(u => 
                    (u.UrunAdi != null && u.UrunAdi.ToLower().Contains(arama)) ||
                    (u.Barkod != null && u.Barkod.Contains(arama))
                );
            }

            dgUrunler.ItemsSource = filtreliListe.ToList();
        }

        private void txtArama_TextChanged(object sender, TextChangedEventArgs e)
        {
            AramayiUygula();
        }

        private void cmbFiltre_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AramayiUygula();
        }

        private void BtnFiltreTumu_Click(object sender, RoutedEventArgs e)
        {
            _aktifFiltre = "Tumu";
            AramayiUygula();
        }

        private void BtnFiltreBugun_Click(object sender, RoutedEventArgs e)
        {
            _aktifFiltre = "Bugun";
            AramayiUygula();
        }

        private void BtnFiltreKritik_Click(object sender, RoutedEventArgs e)
        {
            _aktifFiltre = "Kritik";
            AramayiUygula();
        }

        private void BtnFiltreSkt_Click(object sender, RoutedEventArgs e)
        {
            _aktifFiltre = "Skt";
            AramayiUygula();
        }

        private void BtnFiltreTemizle_Click(object sender, RoutedEventArgs e)
        {
            _aktifFiltre = "Tumu";
            txtArama.Text = "";
            if (cmbKategori.Items.Count > 0) cmbKategori.SelectedIndex = 0;
            if (cmbFirma.Items.Count > 0) cmbFirma.SelectedIndex = 0;
            AramayiUygula();
        }

        private void BtnYeniUrun_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UrunEkleDuzenleWindow pencere = new UrunEkleDuzenleWindow(_db);
                var owner = Window.GetWindow(this);
                if (owner != null && owner.IsLoaded)
                {
                    pencere.Owner = owner;
                }
                if (pencere.ShowDialog() == true)
                {
                    UrunleriYukle();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ürün ekleme penceresi açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDuzenle_Click(object sender, RoutedEventArgs e)
        {
            DuzenleSeciliUrun();
        }

        private void dgUrunler_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DuzenleSeciliUrun();
        }

        private void DuzenleSeciliUrun()
        {
            if (dgUrunler.SelectedItem is Urun seciliUrun)
            {
                try
                {
                    UrunEkleDuzenleWindow pencere = new UrunEkleDuzenleWindow(_db, seciliUrun);
                    var owner = Window.GetWindow(this);
                    if (owner != null && owner.IsLoaded)
                    {
                        pencere.Owner = owner;
                    }
                    if (pencere.ShowDialog() == true)
                    {
                        UrunleriYukle();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ürün düzenleme penceresi açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Lütfen düzenlemek için bir ürün seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnSil_Click(object sender, RoutedEventArgs e)
        {
            if (dgUrunler.SelectedItem is Urun seciliUrun)
            {
                var cevap = MessageBox.Show($"'{seciliUrun.UrunAdi}' ürününü silmek istediğinize emin misiniz?", "Ürün Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (cevap == MessageBoxResult.Yes)
                {
                    using (var baglanti = _db.BaglantiGetir())
                    {
                        baglanti.Open();
                        baglanti.Execute("DELETE FROM Urunler WHERE Id = @Id", new { Id = seciliUrun.Id });
                    }
                    UrunleriYukle();
                }
            }
            else
            {
                MessageBox.Show("Lütfen silmek için bir ürün seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
