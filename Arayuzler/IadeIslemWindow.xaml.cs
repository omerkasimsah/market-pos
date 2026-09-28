using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using MarketPOS.Veritabani;
using Dapper;

namespace MarketPOS.Arayuzler
{
    public class IadeUrunSatiri : INotifyPropertyChanged
    {
        private bool _iadeSecildi;
        private decimal _iadeAdedi;

        public int UrunId { get; set; }
        public string UrunAdi { get; set; }
        public decimal Adet { get; set; }
        public decimal BirimFiyat { get; set; }
        public decimal ToplamFiyat => Adet * BirimFiyat;

        public bool IadeSecildi
        {
            get => _iadeSecildi;
            set { _iadeSecildi = value; OnPropertyChanged(nameof(IadeSecildi)); }
        }

        public decimal IadeAdedi
        {
            get => _iadeAdedi;
            set { _iadeAdedi = value; OnPropertyChanged(nameof(IadeAdedi)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class SatisDto
    {
        public int Id { get; set; }
        public string FisNo { get; set; }
        public string SatisNumarasi { get; set; }
        public decimal ToplamTutar { get; set; }
        public string OdemeTuru { get; set; }
    }

    public class SatisDetayDto
    {
        public int UrunId { get; set; }
        public string UrunAdi { get; set; }
        public decimal Adet { get; set; }
        public decimal BirimFiyat { get; set; }
        public decimal ToplamFiyat { get; set; }
    }

    public partial class IadeIslemWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        private int _bulunanSatisId;
        private List<IadeUrunSatiri> _iadeUrunleri = new List<IadeUrunSatiri>();

        public IadeIslemWindow()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
        }

        private void BtnAra_Click(object sender, RoutedEventArgs e)
        {
            string aramaDegeri = txtAramaSatisNo.Text.Trim();
            if (string.IsNullOrEmpty(aramaDegeri))
            {
                MessageBox.Show("Lütfen Satış Numarası veya Fiş Numarası girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();

                    // Satışı bul
                    var satis = baglanti.QueryFirstOrDefault<SatisDto>(
                        "SELECT Id, FisNo, SatisNumarasi, ToplamTutar, OdemeTuru FROM Satislar WHERE SatisNumarasi = @Arama OR FisNo = @Arama",
                        new { Arama = aramaDegeri });

                    if (satis == null)
                    {
                        MessageBox.Show($"'{aramaDegeri}' numaralı satış bulunamadı.", "Bulunamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _bulunanSatisId = satis.Id;

                    // Satış detaylarını getir
                    var detaylar = baglanti.Query<SatisDetayDto>(
                        "SELECT UrunId, UrunAdi, Adet, BirimFiyat, ToplamFiyat FROM SatisDetaylari WHERE SatisId = @SatisId",
                        new { SatisId = _bulunanSatisId }).ToList();

                    _iadeUrunleri = detaylar.Select(d => new IadeUrunSatiri
                    {
                        UrunId = d.UrunId,
                        UrunAdi = d.UrunAdi,
                        Adet = d.Adet,
                        BirimFiyat = d.BirimFiyat,
                        IadeAdedi = 0,
                        IadeSecildi = false
                    }).ToList();

                    dgIadeUrunleri.ItemsSource = _iadeUrunleri;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Arama hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnIadeOnayla_Click(object sender, RoutedEventArgs e)
        {
            var secilenler = _iadeUrunleri.Where(x => x.IadeSecildi && x.IadeAdedi > 0).ToList();

            if (secilenler.Count == 0)
            {
                MessageBox.Show("İade edilecek ürün seçmediniz veya iade adedi girmediniz.\nÜrünlerin yanındaki checkbox'ı işaretleyip iade adetini girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Doğrulama
            foreach (var urun in secilenler)
            {
                if (urun.IadeAdedi > urun.Adet)
                {
                    MessageBox.Show($"'{urun.UrunAdi}' için iade adedi ({urun.IadeAdedi}), satış adedinden ({urun.Adet}) fazla olamaz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            decimal toplamIadeTutari = secilenler.Sum(x => x.IadeAdedi * x.BirimFiyat);
            string iadeSebebi = ((System.Windows.Controls.ComboBoxItem)cmbIadeSebebi.SelectedItem).Content.ToString();
            string iadeTuru = ((System.Windows.Controls.ComboBoxItem)cmbIadeTuru.SelectedItem).Content.ToString();

            var cevap = MessageBox.Show(
                $"İade Detayları:\n\n" +
                $"Ürün Sayısı: {secilenler.Count}\n" +
                $"İade Tutarı: {toplamIadeTutari:N2} ₺\n" +
                $"Sebep: {iadeSebebi}\n" +
                $"İade Türü: {iadeTuru}\n\n" +
                "İadeyi onaylıyor musunuz?",
                "İade Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (cevap != MessageBoxResult.Yes) return;

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    using (var islem = baglanti.BeginTransaction())
                    {
                        foreach (var urun in secilenler)
                        {
                            // İade kaydı
                            baglanti.Execute(@"INSERT INTO Iadeler (SatisId, UrunId, UrunAdi, IadeAdedi, IadeTutari, IadeSebebi, IadeTarihi, IadeTuru) 
                                              VALUES (@SatisId, @UrunId, @UrunAdi, @IadeAdedi, @IadeTutari, @IadeSebebi, @IadeTarihi, @IadeTuru)",
                                new
                                {
                                    SatisId = _bulunanSatisId,
                                    UrunId = urun.UrunId,
                                    UrunAdi = urun.UrunAdi,
                                    IadeAdedi = urun.IadeAdedi,
                                    IadeTutari = urun.IadeAdedi * urun.BirimFiyat,
                                    IadeSebebi = iadeSebebi,
                                    IadeTarihi = DateTime.Now,
                                    IadeTuru = iadeTuru
                                }, islem);

                            // Stok geri ekle
                            baglanti.Execute("UPDATE Urunler SET StokMiktari = StokMiktari + @Adet WHERE Id = @Id",
                                new { Adet = urun.IadeAdedi, Id = urun.UrunId }, islem);
                        }

                        // Satış kaydına iade tutarını ekle
                        baglanti.Execute("UPDATE Satislar SET IadeTutari = COALESCE(IadeTutari, 0) + @Tutar WHERE Id = @Id",
                            new { Tutar = toplamIadeTutari, Id = _bulunanSatisId }, islem);

                        islem.Commit();
                    }
                }

                MessageBox.Show($"İade işlemi başarıyla tamamlandı!\nToplam İade: {toplamIadeTutari:N2} ₺", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İade işlemi sırasında hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnIptal_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
