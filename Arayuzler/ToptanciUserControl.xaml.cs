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
    public partial class ToptanciUserControl : UserControl
    {
        private readonly VeritabaniYoneticisi _db;
        private List<Toptanci> _tumToptancilar;

        public ToptanciUserControl()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
            _tumToptancilar = new List<Toptanci>();
        }

        private void BtnAnaMenu_Click(object sender, RoutedEventArgs e)
        {
            AnaMenuWindow.MenuyuGoster();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ToptancilariYukle();
        }

        public void ToptancilariYukle()
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();
                _tumToptancilar = baglanti.Query<Toptanci>("SELECT * FROM Toptancilar ORDER BY FirmaAdi").ToList();
            }
            AramayiUygula();
        }

        private void AramayiUygula()
        {
            string arama = txtArama.Text.ToLower().Trim();
            if (string.IsNullOrEmpty(arama))
            {
                dgToptancilar.ItemsSource = _tumToptancilar;
            }
            else
            {
                dgToptancilar.ItemsSource = _tumToptancilar.Where(t =>
                    (t.FirmaAdi != null && t.FirmaAdi.ToLower().Contains(arama)) ||
                    (t.Yetkili != null && t.Yetkili.ToLower().Contains(arama)) ||
                    (t.Telefon != null && t.Telefon.Contains(arama))
                ).ToList();
            }
        }

        private void txtArama_TextChanged(object sender, TextChangedEventArgs e)
        {
            AramayiUygula();
        }

        private void BtnYeniToptanci_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                YeniToptanciEkleWindow pencere = new YeniToptanciEkleWindow(_db);
                var owner = Window.GetWindow(this);
                if (owner != null && owner.IsLoaded) pencere.Owner = owner;
                if (pencere.ShowDialog() == true)
                {
                    ToptancilariYukle();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Toptancı ekleme penceresi açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnIslemYap_Click(object sender, RoutedEventArgs e)
        {
            if (dgToptancilar.SelectedItem is Toptanci secili)
            {
                try
                {
                    ToptanciIslemWindow pencere = new ToptanciIslemWindow(secili, _db);
                    var owner = Window.GetWindow(this);
                    if (owner != null && owner.IsLoaded) pencere.Owner = owner;
                    if (pencere.ShowDialog() == true)
                    {
                        ToptancilariYukle();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Toptancı işlem penceresi açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Lütfen bir toptancı seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnSiparisOlustur_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SiparisOlusturWindow pencere = new SiparisOlusturWindow(_db);
                var owner = Window.GetWindow(this);
                if (owner != null && owner.IsLoaded) pencere.Owner = owner;
                pencere.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sipariş penceresi açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
