using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;
using Dapper;

namespace MarketPOS.Arayuzler
{
    public partial class MusteriTakipUserControl : UserControl
    {
        private readonly VeritabaniYoneticisi _db;
        private List<Musteri> _tumMusteriler;
        private Musteri _seciliMusteri;

        public MusteriTakipUserControl()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
            _tumMusteriler = new List<Musteri>();
        }

        private void BtnAnaMenu_Click(object sender, RoutedEventArgs e)
        {
            AnaMenuWindow.MenuyuGoster();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            MusterileriYukle();
        }

        public void MusterileriYukle(string aramaKelimesi = "")
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();
                string sorgu = "SELECT * FROM Musteriler ORDER BY AdSoyad";
                _tumMusteriler = baglanti.Query<Musteri>(sorgu).ToList();
            }

            AramayiUygula(aramaKelimesi);
        }

        private void AramayiUygula(string aramaKelimesi)
        {
            if (string.IsNullOrWhiteSpace(aramaKelimesi))
            {
                dgMusteriler.ItemsSource = _tumMusteriler;
            }
            else
            {
                aramaKelimesi = aramaKelimesi.ToLower();
                dgMusteriler.ItemsSource = _tumMusteriler.Where(m => 
                    (m.AdSoyad != null && m.AdSoyad.ToLower().Contains(aramaKelimesi)) || 
                    (m.Telefon != null && m.Telefon.Contains(aramaKelimesi))
                ).ToList();
            }
        }

        private void txtArama_TextChanged(object sender, TextChangedEventArgs e)
        {
            AramayiUygula(txtArama.Text);
        }

        private void dgMusteriler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _seciliMusteri = dgMusteriler.SelectedItem as Musteri;
            
            if (_seciliMusteri != null)
            {
                txtSeciliMusteriAd.Text = _seciliMusteri.AdSoyad;
                txtSeciliMusteriBorc.Text = $"{_seciliMusteri.BorcMiktari:N2} ₺";
                btnBorcEkle.IsEnabled = true;
                btnTahsilatYap.IsEnabled = true;

                HareketleriYukle(_seciliMusteri.Id);
            }
            else
            {
                txtSeciliMusteriAd.Text = "Müşteri Seçilmedi";
                txtSeciliMusteriBorc.Text = "0,00 ₺";
                btnBorcEkle.IsEnabled = false;
                btnTahsilatYap.IsEnabled = false;
                dgHareketler.ItemsSource = null;
            }
        }

        public void HareketleriYukle(int musteriId)
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();
                // Musteri_Id kolonu Islemler tablosunda var mı?
                // Önceki tasarımda Islemler tablosunda (id, islem_tipi, aciklama, tutar, tarih) vardı ama musteri id yoktu!
                // Müşteri hareketlerini tutmak için Islemler tablosuna MusteriId eklemek gerekiyor veya Müşteri detayını açıklamadan okuyacağız.
                // İleride veritabanı şemasını güncelleyeceğiz. Şimdilik açıklama içinde arama yapabiliriz veya "MusteriId" sütunu ekleyebiliriz.
                
                try
                {
                    // Eğer tabloya MusteriId sütunu eklendiyse:
                    string sorgu = "SELECT * FROM Islemler WHERE MusteriId = @Id ORDER BY Tarih DESC";
                    var hareketler = baglanti.Query<Islem>(sorgu, new { Id = musteriId }).ToList();
                    dgHareketler.ItemsSource = hareketler;
                }
                catch
                {
                    // Eğer tabloya eklenmediyse şimdilik boş göster, veritabanı güncellemesi yapıldığında düzelecek.
                    dgHareketler.ItemsSource = new List<Islem>();
                }
            }
        }

        private void BtnYeniMusteri_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                YeniMusteriEkleWindow pencere = new YeniMusteriEkleWindow(_db);
                var owner = Window.GetWindow(this);
                if (owner != null && owner.IsLoaded) pencere.Owner = owner;
                if (pencere.ShowDialog() == true)
                {
                    MusterileriYukle(txtArama.Text);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Müşteri ekleme penceresi açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBorcEkle_Click(object sender, RoutedEventArgs e)
        {
            if (_seciliMusteri == null) return;
            try
            {
                TahsilatWindow pencere = new TahsilatWindow(_seciliMusteri, "Borç Ekle", _db);
                var owner = Window.GetWindow(this);
                if (owner != null && owner.IsLoaded) pencere.Owner = owner;
                if (pencere.ShowDialog() == true)
                {
                    MusterileriYukle(txtArama.Text);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Borç ekleme penceresi açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnTahsilatYap_Click(object sender, RoutedEventArgs e)
        {
            if (_seciliMusteri == null) return;
            try
            {
                TahsilatWindow pencere = new TahsilatWindow(_seciliMusteri, "Tahsilat", _db);
                var owner = Window.GetWindow(this);
                if (owner != null && owner.IsLoaded) pencere.Owner = owner;
                if (pencere.ShowDialog() == true)
                {
                    MusterileriYukle(txtArama.Text);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Tahsilat penceresi açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
