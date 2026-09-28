using System;
using System.Windows;
using Dapper;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public partial class ToptanciIslemWindow : Window
    {
        private readonly Toptanci _toptanci;
        private readonly VeritabaniYoneticisi _db;

        public ToptanciIslemWindow(Toptanci toptanci, VeritabaniYoneticisi db)
        {
            InitializeComponent();
            _toptanci = toptanci;
            _db = db;

            txtToptanciBilgi.Text = $"Firma: {_toptanci.FirmaAdi} ({_toptanci.Yetkili})\nGüncel Borç / Bakiye: {_toptanci.BorcMiktari:N2} ₺";
            txtAciklama.Text = "Toptancı İşlemi";
        }

        private void Onayla_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtTutar.Text, out decimal tutar) || tutar <= 0)
            {
                MessageBox.Show("Lütfen geçerli bir tutar giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtTutar.Focus();
                return;
            }

            bool odeme = cmbIslemTuru.SelectedIndex == 0;

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    using (var islem = baglanti.BeginTransaction())
                    {
                        string islemTipi = odeme ? "Toptancı Ödemesi" : "Mal Alımı (Borç)";
                        string aciklama = $"{txtAciklama.Text.Trim()} ({_toptanci.FirmaAdi})";

                        // 1. Islemler tablosuna ekle
                        string islemSorgu = @"
                            INSERT INTO Islemler (IslemTipi, Aciklama, Tutar, Tarih)
                            VALUES (@IslemTipi, @Aciklama, @Tutar, @Tarih)";

                        baglanti.Execute(islemSorgu, new
                        {
                            IslemTipi = islemTipi,
                            Aciklama = aciklama,
                            Tutar = tutar,
                            Tarih = DateTime.Now
                        }, islem);

                        // 2. Toptancı borcunu güncelle
                        decimal degisim = odeme ? -tutar : tutar;
                        string guncelleSorgu = "UPDATE Toptancilar SET BorcMiktari = BorcMiktari + @Degisim WHERE Id = @Id";
                        baglanti.Execute(guncelleSorgu, new { Degisim = degisim, Id = _toptanci.Id }, islem);

                        // 3. Eğer mal alımı veya ödeme ise Giderler tablosuna da loglayalım (Seçenek)
                        if (odeme)
                        {
                            string giderSorgu = @"
                                INSERT INTO Giderler (GiderTuru, Aciklama, Tutar, Tarih)
                                VALUES ('Toptancı Ödemesi', @Aciklama, @Tutar, @Tarih)";
                            baglanti.Execute(giderSorgu, new { Aciklama = aciklama, Tutar = tutar, Tarih = DateTime.Now }, islem);
                        }

                        islem.Commit();
                    }
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İşlem kaydedilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Iptal_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
