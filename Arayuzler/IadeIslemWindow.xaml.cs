using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MarketPOS.Modeller;
using MarketPOS.Servisler;
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
        public decimal OncekiIade { get; set; }
        public decimal Kalan => Adet - OncekiIade;
        public decimal ToplamFiyat => Adet * BirimFiyat;

        public bool IadeSecildi
        {
            get => _iadeSecildi;
            set { _iadeSecildi = value; OnPropertyChanged(nameof(IadeSecildi)); }
        }

        public decimal IadeAdedi
        {
            get => _iadeAdedi;
            set
            {
                _iadeAdedi = value;
                if (value > 0 && !_iadeSecildi) IadeSecildi = true;
                OnPropertyChanged(nameof(IadeAdedi));
            }
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
        public bool IptalEdildi { get; set; }
        public int? MusteriId { get; set; }
        public string MusteriAdi { get; set; }
        public decimal IskontoTutari { get; set; }
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
        private SatisDto _satis;
        private decimal _iskontoOrani = 1m;
        private List<IadeUrunSatiri> _iadeUrunleri = new List<IadeUrunSatiri>();

        public IadeIslemWindow(string aramaDegeri = null)
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();

            Loaded += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(aramaDegeri))
                {
                    txtAramaSatisNo.Text = aramaDegeri;
                    BtnAra_Click(null, null);
                }
                txtAramaSatisNo.Focus();
            };
        }

        private void TxtAramaSatisNo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnAra_Click(null, null);
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

                    var satis = baglanti.QueryFirstOrDefault<SatisDto>(
                        @"SELECT Id, FisNo, SatisNumarasi, ToplamTutar, OdemeTuru, IptalEdildi, MusteriId, MusteriAdi, COALESCE(IskontoTutari,0) IskontoTutari
                          FROM Satislar WHERE SatisNumarasi = @Arama OR FisNo = @Arama ORDER BY Id DESC",
                        new { Arama = aramaDegeri });

                    if (satis == null)
                    {
                        MessageBox.Show($"'{aramaDegeri}' numaralı satış bulunamadı.", "Bulunamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (satis.IptalEdildi)
                    {
                        MessageBox.Show("Bu satış tamamen iptal edilmiş, iade yapılamaz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _satis = satis;

                    var detaylar = baglanti.Query<SatisDetayDto>(
                        "SELECT UrunId, UrunAdi, Adet, BirimFiyat, ToplamFiyat FROM SatisDetaylari WHERE SatisId = @SatisId",
                        new { SatisId = satis.Id }).ToList();

                    // Fiş altı iskonto varsa iade tutarı da aynı oranda indirimli hesaplanır
                    decimal araToplam = detaylar.Sum(d => d.ToplamFiyat);
                    _iskontoOrani = araToplam > 0 ? satis.ToplamTutar / araToplam : 1m;

                    var oncekiIadeler = baglanti.Query<SatisDetayDto>(
                        "SELECT UrunId, SUM(IadeAdedi) AS Adet FROM Iadeler WHERE SatisId = @SatisId GROUP BY UrunId",
                        new { SatisId = satis.Id }).ToDictionary(x => x.UrunId, x => x.Adet);

                    // Aynı ürün birden çok satırda olabilir (örn. ayrı tartımlar) - ürün bazında birleştirilir
                    _iadeUrunleri = detaylar.GroupBy(d => d.UrunId).Select(g =>
                    {
                        decimal adet = g.Sum(x => x.Adet);
                        return new IadeUrunSatiri
                        {
                            UrunId = g.Key,
                            UrunAdi = g.First().UrunAdi,
                            Adet = adet,
                            BirimFiyat = adet != 0 ? g.Sum(x => x.ToplamFiyat) / adet : 0,
                            OncekiIade = oncekiIadeler.TryGetValue(g.Key, out decimal onceki) ? onceki : 0
                        };
                    }).ToList();

                    foreach (var satir in _iadeUrunleri) satir.PropertyChanged += (s, ev) => TutariGuncelle();
                    dgIadeUrunleri.ItemsSource = _iadeUrunleri;

                    // Müşterisi olmayan satışta veresiyeye iade yapılamaz
                    var veresiyeSecenegi = (ComboBoxItem)cmbIadeTuru.Items[1];
                    veresiyeSecenegi.IsEnabled = satis.MusteriId.HasValue;
                    cmbIadeTuru.SelectedIndex = satis.MusteriId.HasValue && satis.OdemeTuru != "Nakit" && satis.OdemeTuru != "Kredi Kartı" ? 1 : 0;

                    TutariGuncelle();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Arama hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private decimal IadeTutariHesapla(IEnumerable<IadeUrunSatiri> satirlar) =>
            Math.Round(satirlar.Sum(x => x.IadeAdedi * x.BirimFiyat) * _iskontoOrani, 2);

        private void TutariGuncelle()
        {
            txtIadeTutari.Text = $"{IadeTutariHesapla(_iadeUrunleri.Where(x => x.IadeSecildi && x.IadeAdedi > 0)):N2} ₺";
        }

        private void BtnIadeOnayla_Click(object sender, RoutedEventArgs e)
        {
            if (_satis == null)
            {
                MessageBox.Show("Önce iade yapılacak satışı arayın.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var secilenler = _iadeUrunleri.Where(x => x.IadeSecildi && x.IadeAdedi > 0).ToList();

            if (secilenler.Count == 0)
            {
                MessageBox.Show("İade edilecek ürün seçmediniz veya iade adedi girmediniz.\nÜrünlerin yanındaki 'İade Adet' alanına miktarı yazın.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var urun in secilenler)
            {
                if (urun.IadeAdedi > urun.Kalan)
                {
                    MessageBox.Show($"'{urun.UrunAdi}' için en fazla {urun.Kalan:0.###} adet iade edilebilir (satılan {urun.Adet:0.###}, önceden iade {urun.OncekiIade:0.###}).", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            decimal toplamIadeTutari = IadeTutariHesapla(secilenler);
            string iadeSebebi = ((ComboBoxItem)cmbIadeSebebi.SelectedItem).Content.ToString();
            string iadeTuru = ((ComboBoxItem)cmbIadeTuru.SelectedItem).Content.ToString();
            bool veresiyeyeIade = iadeTuru.StartsWith("Veresiye");

            var cevap = MessageBox.Show(
                $"İade Detayları:\n\n" +
                $"Satış: {_satis.SatisNumarasi ?? _satis.FisNo}\n" +
                $"Ürün Sayısı: {secilenler.Count}\n" +
                $"İade Tutarı: {toplamIadeTutari:N2} ₺\n" +
                $"Sebep: {iadeSebebi}\n" +
                $"İade Türü: {iadeTuru}" + (veresiyeyeIade ? $" ({_satis.MusteriAdi})" : "") + "\n\n" +
                "İadeyi onaylıyor musunuz?",
                "İade Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (cevap != MessageBoxResult.Yes) return;
            if (!YoneticiOnayWindow.YetkiDogrula(this, $"İade {_satis.SatisNumarasi} {toplamIadeTutari:N2} TL")) return;

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    using (var islem = baglanti.BeginTransaction())
                    {
                        foreach (var urun in secilenler)
                        {
                            baglanti.Execute(@"INSERT INTO Iadeler (SatisId, UrunId, UrunAdi, IadeAdedi, IadeTutari, IadeSebebi, IadeTarihi, IadeTuru, KullaniciAdi)
                                              VALUES (@SatisId, @UrunId, @UrunAdi, @IadeAdedi, @IadeTutari, @IadeSebebi, @IadeTarihi, @IadeTuru, @KullaniciAdi)",
                                new
                                {
                                    SatisId = _satis.Id,
                                    urun.UrunId,
                                    urun.UrunAdi,
                                    urun.IadeAdedi,
                                    IadeTutari = IadeTutariHesapla(new[] { urun }),
                                    IadeSebebi = iadeSebebi,
                                    IadeTarihi = DateTime.Now,
                                    IadeTuru = iadeTuru,
                                    KullaniciAdi = AktifOturum.KullaniciAdi ?? "Sistem"
                                }, islem);

                            // Bozuk / SKT geçmiş ürün rafa geri dönmez, stoğa eklenmez
                            if (iadeSebebi != "Bozuk / Hasarlı Ürün" && iadeSebebi != "SKT Geçmiş")
                            {
                                baglanti.Execute("UPDATE Urunler SET StokMiktari = StokMiktari + @Adet, GuncellenmeTarihi = @Tarih WHERE Id = @Id",
                                    new { Adet = urun.IadeAdedi, Id = urun.UrunId, Tarih = DateTime.Now }, islem);
                            }
                        }

                        baglanti.Execute("UPDATE Satislar SET IadeTutari = COALESCE(IadeTutari, 0) + @Tutar WHERE Id = @Id",
                            new { Tutar = toplamIadeTutari, Id = _satis.Id }, islem);

                        if (veresiyeyeIade && _satis.MusteriId.HasValue)
                        {
                            baglanti.Execute("UPDATE Musteriler SET BorcMiktari = BorcMiktari - @Tutar WHERE Id = @Id",
                                new { Tutar = toplamIadeTutari, Id = _satis.MusteriId.Value }, islem);
                            baglanti.Execute(@"INSERT INTO Islemler (IslemTipi, Aciklama, Tutar, Tarih, MusteriId, SatisId)
                                               VALUES ('Tahsilat', @Aciklama, @Tutar, @Tarih, @MusteriId, @SatisId)",
                                new
                                {
                                    Aciklama = $"Ürün iadesi - borçtan düşüldü (Satış: {_satis.SatisNumarasi ?? _satis.FisNo})",
                                    Tutar = toplamIadeTutari,
                                    Tarih = DateTime.Now,
                                    MusteriId = _satis.MusteriId.Value,
                                    SatisId = _satis.Id
                                }, islem);
                        }

                        islem.Commit();
                    }
                }

                LogServisi.Bilgi($"İade yapıldı: {_satis.SatisNumarasi} {toplamIadeTutari:N2} TL ({iadeTuru}, {iadeSebebi})");
                IadeFisiYazdir(secilenler, toplamIadeTutari, iadeTuru);

                MessageBox.Show($"İade işlemi başarıyla tamamlandı!\nToplam İade: {toplamIadeTutari:N2} ₺" +
                    (veresiyeyeIade ? "\nTutar müşterinin borcundan düşüldü." : "\nMüşteriye nakit ödeme yapınız."),
                    "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İade işlemi sırasında hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IadeFisiYazdir(List<IadeUrunSatiri> satirlar, decimal tutar, string iadeTuru)
        {
            if (!YaziciServisi.YaziciTanimli) return;

            var sb = new StringBuilder();
            string cizgi = new string('-', YaziciServisi.FisGenisligiKarakter);
            sb.AppendLine(YaziciServisi.OrtalaSatir(AyarServisi.Oku(AyarServisi.DukkanAdi)));
            sb.AppendLine(YaziciServisi.OrtalaSatir("İADE FİŞİ"));
            sb.AppendLine($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"Satış No: {_satis.SatisNumarasi ?? _satis.FisNo}");
            sb.AppendLine($"Personel: {AktifOturum.AdSoyad ?? "-"}");
            sb.AppendLine(cizgi);
            foreach (var s in satirlar)
            {
                sb.AppendLine(s.UrunAdi);
                sb.AppendLine(YaziciServisi.IkiSutun($"  {s.IadeAdedi:0.###} x {s.BirimFiyat:N2}", $"{s.IadeAdedi * s.BirimFiyat:N2}"));
            }
            sb.AppendLine(cizgi);
            sb.AppendLine(YaziciServisi.IkiSutun("İADE TOPLAMI", $"{tutar:N2} TL"));
            sb.AppendLine($"İade Şekli: {iadeTuru}");
            sb.AppendLine();
            sb.AppendLine("Teslim Alan İmza: ..................");
            sb.AppendLine(YaziciServisi.OrtalaSatir("MALİ DEĞERİ YOKTUR"));

            new YaziciServisi().MetinYazdir(sb.ToString());
        }

        private void BtnIptal_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
