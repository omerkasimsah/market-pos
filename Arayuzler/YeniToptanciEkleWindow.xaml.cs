using System;
using System.Windows;
using Dapper;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public partial class YeniToptanciEkleWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;

        public YeniToptanciEkleWindow(VeritabaniYoneticisi db)
        {
            InitializeComponent();
            _db = db;
        }

        private void Kaydet_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFirmaAdi.Text))
            {
                MessageBox.Show("Lütfen firma adını giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtFirmaAdi.Focus();
                return;
            }

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    string sorgu = @"
                        INSERT INTO Toptancilar (FirmaAdi, Yetkili, Telefon, BorcMiktari, KayitTarihi)
                        VALUES (@FirmaAdi, @Yetkili, @Telefon, 0, @KayitTarihi)";

                    baglanti.Execute(sorgu, new
                    {
                        FirmaAdi = txtFirmaAdi.Text.Trim(),
                        Yetkili = txtYetkili.Text.Trim(),
                        Telefon = txtTelefon.Text.Trim(),
                        KayitTarihi = DateTime.Now
                    });
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Toptancı kaydedilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Iptal_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
