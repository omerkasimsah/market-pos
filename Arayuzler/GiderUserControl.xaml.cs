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
    public partial class GiderUserControl : UserControl
    {
        private readonly VeritabaniYoneticisi _db;

        public GiderUserControl()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
        }

        private void BtnAnaMenu_Click(object sender, RoutedEventArgs e)
        {
            AnaMenuWindow.MenuyuGoster();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            GiderleriYukle();
        }

        public void GiderleriYukle()
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();
                var giderler = baglanti.Query<Gider>("SELECT * FROM Giderler ORDER BY Tarih DESC").ToList();
                dgGiderler.ItemsSource = giderler;
            }
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtTutar.Text, out decimal tutar) || tutar <= 0)
            {
                MessageBox.Show("Lütfen geçerli bir gider tutarı giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtTutar.Focus();
                return;
            }

            string giderTuru = (cmbGiderTuru.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Diğer Harcama";
            string aciklama = txtAciklama.Text.Trim();

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    using (var islem = baglanti.BeginTransaction())
                    {
                        // 1. Giderler tablosuna ekle
                        string giderSorgu = @"
                            INSERT INTO Giderler (GiderTuru, Aciklama, Tutar, Tarih)
                            VALUES (@GiderTuru, @Aciklama, @Tutar, @Tarih)";

                        baglanti.Execute(giderSorgu, new
                        {
                            GiderTuru = giderTuru,
                            Aciklama = aciklama,
                            Tutar = tutar,
                            Tarih = DateTime.Now
                        }, islem);

                        // 2. Islemler tablosuna da ekle
                        string islemSorgu = @"
                            INSERT INTO Islemler (IslemTipi, Aciklama, Tutar, Tarih)
                            VALUES ('Gider Kaydı', @Aciklama, @Tutar, @Tarih)";

                        baglanti.Execute(islemSorgu, new
                        {
                            Aciklama = $"Gider ({giderTuru}): {aciklama}",
                            Tutar = tutar,
                            Tarih = DateTime.Now
                        }, islem);

                        islem.Commit();
                    }
                }

                txtTutar.Text = "";
                txtAciklama.Text = "";
                GiderleriYukle();
                MessageBox.Show("Gider kaydı başarıyla eklendi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gider eklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
