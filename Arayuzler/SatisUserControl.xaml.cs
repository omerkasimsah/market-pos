using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;
using Dapper;

using MarketPOS.Servisler;

namespace MarketPOS.Arayuzler
{
    public class ParkEdilenSatis
    {
        public DateTime Tarih { get; set; } = DateTime.Now;
        public List<SepetUrunu> Urunler { get; set; } = new List<SepetUrunu>();
        public decimal Iskonto { get; set; }
    }

    public partial class SatisUserControl : UserControl
    {
        private readonly VeritabaniYoneticisi _db;
        private readonly ObservableCollection<SepetUrunu> _sepet;
        private decimal _uygulananIskonto = 0;
        private readonly PosCihazServisi _posServisi;
        private readonly YaziciServisi _yaziciServisi;
        
        // Tekli / Havuz Bekleyen Sepet Sistemi
        private readonly List<ParkEdilenSatis> _parkEdilenler = new List<ParkEdilenSatis>();
        
        // Barkod okuma dinleyicisi değişkenleri
        private string _okunanBarkod = "";
        private DateTime _sonTusZamani = DateTime.Now;
        private readonly TimeSpan _barkodZamanAsimi = TimeSpan.FromMilliseconds(50);

        private string _numpadGirisi = "";

        public SatisUserControl()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
            _posServisi = new PosCihazServisi();
            _yaziciServisi = new YaziciServisi();
            _sepet = new ObservableCollection<SepetUrunu>();
            dgSepet.ItemsSource = _sepet;
            ToplamiGuncelle();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Global dinleyicileri ana pencereye bağla
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.PreviewTextInput += ParentWindow_PreviewTextInput;
                parentWindow.PreviewKeyDown += ParentWindow_PreviewKeyDown;
            }
            BugunkuSatisSayisiniGuncelle();
            SlotButonlariniGuncelle();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Dinleyicileri temizle ki bellek sızıntısı veya çakışma olmasın
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.PreviewTextInput -= ParentWindow_PreviewTextInput;
                parentWindow.PreviewKeyDown -= ParentWindow_PreviewKeyDown;
            }
        }

        #region Barkod Dinleyici (Global Barcode Listener)

        private void ParentWindow_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.OriginalSource is TextBox) return;

            TimeSpan fark = DateTime.Now - _sonTusZamani;
            
            if (fark > _barkodZamanAsimi)
            {
                _okunanBarkod = "";
            }

            _okunanBarkod += e.Text;
            _sonTusZamani = DateTime.Now;
            e.Handled = true;
        }

        private void ParentWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1) { BtnNakit_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.F2) { BtnKrediKarti_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.F3) { BtnVeresiye_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.F4) { BtnIptal_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.F5) { BtnIskonto_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.F6) { BtnParaUstu_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.F7) { BtnParkEt_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.F8) { BtnBolunmusOdeme_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.F9) { FiyatGorAc(); e.Handled = true; return; }

            if (e.Key == Key.Enter && _okunanBarkod.Length > 0)
            {
                string temizBarkod = _okunanBarkod.Trim();
                _okunanBarkod = "";
                txtGizliBarkod.Text = temizBarkod;
                UrunuSepeteEkle(temizBarkod);
                e.Handled = true;
            }
        }

        #endregion

        #region Ürün Ekleme ve Sepet İşlemleri

        private void UrunuSepeteEkle(string barkod, string varsayilanAd = null)
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();

                // 1. Terazili Barkod Kontrolü (Genelde 13 Haneli ve 27, 28, 29 ile başlar)
                // Örnek: 27 00123 00550 4 -> Bayrak: 27, Ürün Kodu: 00123, Fiyat: 5,50 TL
                decimal teraziFiyati = 0;
                string orjinalBarkod = barkod;

                if (barkod.Length == 13 && (barkod.StartsWith("27") || barkod.StartsWith("28") || barkod.StartsWith("29")))
                {
                    string urunKodu = barkod.Substring(2, 5); // 00123
                    string fiyatKismi = barkod.Substring(7, 5); // 00550
                    
                    if (decimal.TryParse(fiyatKismi, out decimal okunanFiyat))
                    {
                        teraziFiyati = okunanFiyat / 100m; // 5.50
                    }
                    barkod = urunKodu; // Arama için sadece ürün kodunu kullan
                }

                // Hem tam barkod ile hem de terazi ürün koduyla (eğer terazili ise) ara
                var urun = baglanti.QueryFirstOrDefault<Urun>(
                    "SELECT * FROM Urunler WHERE Barkod = @Barkod OR Barkod = @OrjinalBarkod", 
                    new { Barkod = barkod, OrjinalBarkod = orjinalBarkod });

                if (urun != null)
                {
                    if (teraziFiyati > 0)
                    {
                        // Terazili ürünse miktarı 1 kabul et, birim fiyatı terazi fiyatına eşitle
                        urun.SatisFiyati = teraziFiyati;
                    }

                    SepeteUrunEkleVeyaGuncelle(urun);
                    SonUrunBilgisiniGoster(urun);
                }
                else
                {
                    // Ürün bulunamadı, Hızlı Ürün Ekleme Penceresini aç
                    HizliUrunEkleWindow pencere = new HizliUrunEkleWindow(barkod, _db);
                    if (!string.IsNullOrEmpty(varsayilanAd))
                    {
                        pencere.txtUrunAdi.Text = varsayilanAd;
                    }
                    pencere.Owner = Window.GetWindow(this);
                    if (pencere.ShowDialog() == true && pencere.EklenenUrun != null)
                    {
                        SepeteUrunEkleVeyaGuncelle(pencere.EklenenUrun);
                        SonUrunBilgisiniGoster(pencere.EklenenUrun);
                    }
                    else
                    {
                        txtGizliBarkod.Text = "Barkod iptal edildi.";
                    }
                }
            }
        }

        private void SonUrunBilgisiniGoster(Urun urun)
        {
            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    decimal kalanStok = baglanti.ExecuteScalar<decimal>("SELECT StokMiktari FROM Urunler WHERE Id = @Id", new { Id = urun.Id });
                    txtSonUrunBilgisi.Text = $"{urun.UrunAdi} - Kalan: {kalanStok:G}";
                }
            }
            catch
            {
                txtSonUrunBilgisi.Text = $"{urun.UrunAdi}";
            }
        }

        private void SepeteUrunEkleVeyaGuncelle(Urun urun)
        {
            decimal eklenecekAdet = 1;
            if (!string.IsNullOrEmpty(_numpadGirisi) && decimal.TryParse(_numpadGirisi, out decimal girilenAdet))
            {
                eklenecekAdet = girilenAdet;
            }
            _numpadGirisi = ""; 

            var sepettekiUrun = _sepet.FirstOrDefault(x => x.Barkod == urun.Barkod);
            if (sepettekiUrun != null)
            {
                sepettekiUrun.Adet += eklenecekAdet;
            }
            else
            {
                _sepet.Add(new SepetUrunu
                {
                    UrunId = urun.Id,
                    Barkod = urun.Barkod,
                    UrunAdi = urun.UrunAdi,
                    BirimFiyat = urun.SatisFiyati,
                    Adet = eklenecekAdet
                });
            }
            
            ToplamiGuncelle();
            
            if (_sepet.Count > 0)
            {
                dgSepet.ScrollIntoView(_sepet[_sepet.Count - 1]);
            }
        }

        private void ToplamiGuncelle()
        {
            decimal genelToplam = _sepet.Sum(x => x.ToplamFiyat);
            if (_uygulananIskonto > 0)
            {
                txtIskonto.Text = $"Uygulanan İskonto: {_uygulananIskonto:N2} ₺";
                txtIskonto.Visibility = Visibility.Visible;
                genelToplam -= _uygulananIskonto;
                if (genelToplam < 0) genelToplam = 0;
            }
            else
            {
                txtIskonto.Visibility = Visibility.Collapsed;
            }
            
            txtGenelToplam.Text = $"{genelToplam:N2} ₺";
        }

        #endregion

        #region Hızlı Ürün İşlemleri

        private void HizliUrun_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                string urunTag = btn.Tag.ToString();
                string sanalBarkod = "HIZLI_" + urunTag.Replace(" ", "").ToUpper();
                txtGizliBarkod.Text = sanalBarkod;
                UrunuSepeteEkle(sanalBarkod, urunTag);
            }
        }

        private void HizliKategori_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null)
            {
                MessageBox.Show($"{btn.Tag} kategorisi ürünleri listelenecek.", "Kategori", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Numpad İşlemleri

        private void Numpad_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null)
            {
                _numpadGirisi += btn.Content.ToString();
                txtGizliBarkod.Text = "Çarpan: " + _numpadGirisi;
            }
        }

        private void NumpadTemizle_Click(object sender, RoutedEventArgs e)
        {
            _numpadGirisi = "";
            txtGizliBarkod.Text = "Çarpan temizlendi.";
        }

        private void AdetCarp_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_numpadGirisi))
            {
                txtGizliBarkod.Text = $"Beklenen Adet: {_numpadGirisi} X";
            }
        }

        #endregion

        #region Kısayol İşlemleri

        private void BtnNakit_Click(object sender, RoutedEventArgs e)
        {
            if (_sepet.Count == 0) return;
            
            decimal genelToplam = _sepet.Sum(x => x.ToplamFiyat) - _uygulananIskonto;
            if (genelToplam < 0) genelToplam = 0;

            if (SatisKaydet("Nakit", nakitTutar: genelToplam))
            {
                ShowToastNotification($"Satış Başarılı - Nakit: {genelToplam:N2} ₺", "#27AE60");

                // Para Üstü penceresi otomatik aç
                try
                {
                    ParaUstuWindow paraUstu = new ParaUstuWindow(genelToplam);
                    var owner = Window.GetWindow(this);
                    if (owner != null && owner.IsLoaded) paraUstu.Owner = owner;
                    paraUstu.ShowDialog();
                }
                catch { }

                SepetiTemizle();
            }
        }

        private void BtnKrediKarti_Click(object sender, RoutedEventArgs e)
        {
            if (_sepet.Count == 0) return;

            decimal genelToplam = _sepet.Sum(x => x.ToplamFiyat) - _uygulananIskonto;
            if (genelToplam < 0) genelToplam = 0;

            string geciciFisNo = "FIS" + DateTime.Now.ToString("yyMMddHHmmss");
            string posPort = "COM3";
            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    posPort = baglanti.QueryFirstOrDefault<string>("SELECT Deger FROM Ayarlar WHERE Anahtar = 'PosComPort'") ?? "COM3";
                }
            }
            catch { }

            PosBeklemeWindow pencere = new PosBeklemeWindow(genelToplam, geciciFisNo, posPort);
            var owner = Window.GetWindow(this);
            if (owner != null && owner.IsLoaded) pencere.Owner = owner;

            if (pencere.ShowDialog() == true && pencere.Sonuc?.Basarili == true)
            {
                if (SatisKaydet("Kredi Kartı", kartTutar: genelToplam))
                {
                    ShowToastNotification($"✅ POS ONAYLANDI - Satış Tamamlandı ({genelToplam:N2} ₺)", "#27AE60");
                    SepetiTemizle();
                }
            }
            else
            {
                string hataMesaji = pencere.Sonuc?.Mesaj ?? "❌ POS İşlemi Reddedildi";
                ShowToastNotification(hataMesaji, "#E74C3C");
                MessageBox.Show($"POS ÖDEME REDDEDİLDİ!\n\nDurum: {hataMesaji}\n\nSatış tamamlanmadı, sepetteki ürünler korundu. Farklı bir ödeme yöntemi seçebilirsiniz.", "POS Reddi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnVeresiye_Click(object sender, RoutedEventArgs e)
        {
            if (_sepet.Count == 0) return;
            
            decimal genelToplam = _sepet.Sum(x => x.ToplamFiyat) - _uygulananIskonto;

            MusteriSecimWindow pencere = new MusteriSecimWindow(_db);
            var owner = Window.GetWindow(this);
            if (owner != null && owner.IsLoaded) pencere.Owner = owner;
            if (pencere.ShowDialog() == true && pencere.SecilenMusteri != null)
            {
                var musteri = pencere.SecilenMusteri;
                if (SatisKaydet("Veresiye", musteri.Id, musteri.AdSoyad, veresiyeTutar: genelToplam))
                {
                    ShowToastNotification($"Satış Başarılı - Veresiye: {genelToplam:N2} ₺ ({musteri.AdSoyad})", "#8E44AD");
                    SepetiTemizle();
                }
            }
        }

        private void BtnIptal_Click(object sender, RoutedEventArgs e)
        {
            if (_sepet.Count > 0)
            {
                var cevap = MessageBox.Show("Sepetteki tüm ürünler silinecek. Onaylıyor musunuz?", "Sepeti Temizle", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (cevap == MessageBoxResult.Yes)
                {
                    if (!IptalYetkisiVarMi()) return;

                    SepetiTemizle();
                    ShowToastNotification("Sepet Temizlendi", "#E74C3C");
                }
            }
        }

        private void DgSepet_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (dgSepet.SelectedItem != null)
                {
                    if (!IptalYetkisiVarMi())
                    {
                        e.Handled = true; // Silmeyi iptal et
                        return;
                    }
                    
                    // Silme başarılıysa toplamı güncelle
                    Dispatcher.BeginInvoke(new Action(() => ToplamiGuncelle()), DispatcherPriority.Background);
                }
            }
        }

        private bool IptalYetkisiVarMi()
        {
            // Eğer giriş yapan kullanıcı zaten yöneticiyse doğrudan izin ver
            if (AktifOturum.KullaniciAdi != null && AktifOturum.Rol == "Yonetici")
            {
                return true;
            }

            // Kasiyer ise yönetici onayı penceresini aç
            YoneticiOnayWindow onay = new YoneticiOnayWindow();
            onay.Owner = Window.GetWindow(this);
            if (onay.ShowDialog() == true && onay.Onaylandi)
            {
                return true;
            }

            return false;
        }

        private void BtnIskonto_Click(object sender, RoutedEventArgs e)
        {
            if (_sepet.Count == 0) return;
            
            decimal genelToplam = _sepet.Sum(x => x.ToplamFiyat);
            IskontoWindow pencere = new IskontoWindow(genelToplam);
            var owner = Window.GetWindow(this);
            if (owner != null && owner.IsLoaded) pencere.Owner = owner;
            
            if (pencere.ShowDialog() == true)
            {
                _uygulananIskonto = pencere.UygulananIndirim;
                ToplamiGuncelle();
            }
            
            txtGizliBarkod.Focus();
        }

        private void BtnParaUstu_Click(object sender, RoutedEventArgs e)
        {
            decimal genelToplam = _sepet.Sum(x => x.ToplamFiyat) - _uygulananIskonto;
            if (genelToplam <= 0 && _sepet.Count == 0) return;

            try
            {
                ParaUstuWindow pencere = new ParaUstuWindow(genelToplam);
                var owner = Window.GetWindow(this);
                if (owner != null && owner.IsLoaded) pencere.Owner = owner;
                pencere.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Para üstü penceresi açılırken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBolunmusOdeme_Click(object sender, RoutedEventArgs e)
        {
            if (_sepet.Count == 0) return;

            decimal genelToplam = _sepet.Sum(x => x.ToplamFiyat) - _uygulananIskonto;
            if (genelToplam < 0) genelToplam = 0;

            try
            {
                BolunmusOdemeWindow pencere = new BolunmusOdemeWindow(genelToplam, _db);
                var owner = Window.GetWindow(this);
                if (owner != null && owner.IsLoaded) pencere.Owner = owner;

                if (pencere.ShowDialog() == true)
                {
                    int? musteriId = pencere.SecilenMusteriId;
                    string musteriAd = pencere.SecilenMusteriAdi;

                    string odemeTuru = "Bölünmüş";
                    if (pencere.KartTutar == 0 && pencere.VeresiyeTutar == 0) odemeTuru = "Nakit";
                    else if (pencere.NakitTutar == 0 && pencere.VeresiyeTutar == 0) odemeTuru = "Kredi Kartı";
                    else if (pencere.NakitTutar == 0 && pencere.KartTutar == 0) odemeTuru = "Veresiye";

                    if (SatisKaydet(odemeTuru, musteriId, musteriAd,
                        nakitTutar: pencere.NakitTutar, kartTutar: pencere.KartTutar, veresiyeTutar: pencere.VeresiyeTutar))
                    {
                        string detay = "";
                        if (pencere.NakitTutar > 0) detay += $"Nakit: {pencere.NakitTutar:N2}₺ ";
                        if (pencere.KartTutar > 0) detay += $"Kart: {pencere.KartTutar:N2}₺ ";
                        if (pencere.VeresiyeTutar > 0) detay += $"Veresiye: {pencere.VeresiyeTutar:N2}₺";

                        ShowToastNotification($"💰 Bölünmüş Ödeme Tamamlandı - {detay}", "#27AE60");

                        if (pencere.NakitTutar > 0)
                        {
                            try
                            {
                                ParaUstuWindow paraUstu = new ParaUstuWindow(pencere.NakitTutar);
                                var owner2 = Window.GetWindow(this);
                                if (owner2 != null && owner2.IsLoaded) paraUstu.Owner = owner2;
                                paraUstu.ShowDialog();
                            }
                            catch { }
                        }

                        SepetiTemizle();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bölünmüş ödeme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowToastNotification(string mesaj, string hexRenk)
        {
            txtToastMesaj.Text = mesaj;
            
            var converter = new System.Windows.Media.BrushConverter();
            var brush = (System.Windows.Media.Brush)converter.ConvertFromString(hexRenk);
            brdToast.Background = brush;
            
            brdToast.Visibility = Visibility.Visible;
            brdToast.Opacity = 1.0;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (s, ev) =>
            {
                brdToast.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();

            txtGizliBarkod.Focus();
        }

        #region Satış Numarası Üretici

        private string SatisNumarasiUret()
        {
            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    string bugun = DateTime.Now.ToString("yyyyMMdd");
                    int bugunkuSayi = baglanti.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM Satislar WHERE SatisNumarasi LIKE @Prefix",
                        new { Prefix = $"SAT-{bugun}-%" });
                    return $"SAT-{bugun}-{(bugunkuSayi + 1):D4}";
                }
            }
            catch
            {
                return $"SAT-{DateTime.Now:yyyyMMddHHmmss}";
            }
        }

        #endregion

        private bool SatisKaydet(string odemeTuru, int? musteriId = null, string musteriAd = null,
            decimal nakitTutar = 0, decimal kartTutar = 0, decimal veresiyeTutar = 0)
        {
            decimal genelToplam = _sepet.Sum(x => x.ToplamFiyat) - _uygulananIskonto;
            if (genelToplam < 0) genelToplam = 0;

            string fisNo = "FIS" + DateTime.Now.ToString("yyMMddHHmmss");
            string satisNumarasi = SatisNumarasiUret();

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    using (var islem = baglanti.BeginTransaction())
                    {
                        string satisSorgu = @"
                            INSERT INTO Satislar (FisNo, ToplamTutar, OdemeTuru, Tarih, IptalEdildi, NakitTutar, KartTutar, VeresiyeTutar, MusteriId, MusteriAdi, SatisNumarasi, KullaniciId, KullaniciAdi) 
                            VALUES (@FisNo, @ToplamTutar, @OdemeTuru, @Tarih, 0, @NakitTutar, @KartTutar, @VeresiyeTutar, @MusteriId, @MusteriAdi, @SatisNumarasi, @KullaniciId, @KullaniciAdi);
                            SELECT last_insert_rowid();";
                        int satisId = baglanti.ExecuteScalar<int>(satisSorgu, new 
                        { 
                            FisNo = fisNo, ToplamTutar = genelToplam, OdemeTuru = odemeTuru, Tarih = DateTime.Now,
                            NakitTutar = nakitTutar, KartTutar = kartTutar, VeresiyeTutar = veresiyeTutar,
                            MusteriId = musteriId, MusteriAdi = musteriAd, SatisNumarasi = satisNumarasi,
                            KullaniciId = AktifOturum.KullaniciId == 0 ? (int?)null : AktifOturum.KullaniciId,
                            KullaniciAdi = AktifOturum.KullaniciAdi ?? "Sistem"
                        }, islem);

                        foreach (var sepetUrunu in _sepet)
                        {
                            string detaySorgu = @"
                                INSERT INTO SatisDetaylari (SatisId, UrunId, UrunAdi, Adet, BirimFiyat, ToplamFiyat)
                                VALUES (@SatisId, @UrunId, @UrunAdi, @Adet, @BirimFiyat, @ToplamFiyat)";
                            baglanti.Execute(detaySorgu, new
                            {
                                SatisId = satisId,
                                UrunId = sepetUrunu.UrunId,
                                UrunAdi = sepetUrunu.UrunAdi,
                                Adet = sepetUrunu.Adet,
                                BirimFiyat = sepetUrunu.BirimFiyat,
                                ToplamFiyat = sepetUrunu.ToplamFiyat
                            }, islem);

                            // Genel stok düşümü
                            baglanti.Execute("UPDATE Urunler SET StokMiktari = StokMiktari - @Adet WHERE Id = @Id", new { Adet = sepetUrunu.Adet, Id = sepetUrunu.UrunId }, islem);

                            // FIFO SKT Parti stok düşümü (En yakın SKT'li stok önce düşer)
                            var partiler = baglanti.Query<UrunParti>("SELECT * FROM UrunPartileri WHERE UrunId = @UrunId AND StokMiktari > 0 ORDER BY SktTarihi ASC, EklenmeTarihi ASC", new { UrunId = sepetUrunu.UrunId }, islem).ToList();
                            decimal kalanDusecek = sepetUrunu.Adet;
                            foreach (var parti in partiler)
                            {
                                if (kalanDusecek <= 0) break;
                                decimal dusulecekMiktar = Math.Min(parti.StokMiktari, kalanDusecek);
                                baglanti.Execute("UPDATE UrunPartileri SET StokMiktari = StokMiktari - @Miktar WHERE Id = @Id", new { Miktar = dusulecekMiktar, Id = parti.Id }, islem);
                                kalanDusecek -= dusulecekMiktar;
                            }
                        }

                        // Veresiye işlemi
                        if (veresiyeTutar > 0 && musteriId.HasValue)
                        {
                            string hesapSorgu = @"
                                INSERT INTO Islemler (IslemTipi, Aciklama, Tutar, Tarih, MusteriId, SatisId) 
                                VALUES ('Borçlandırma', @Aciklama, @Tutar, @Tarih, @MusteriId, @SatisId)";
                            baglanti.Execute(hesapSorgu, new 
                            { 
                                Aciklama = $"Veresiye Satış (Fiş: {fisNo}, No: {satisNumarasi})",
                                Tutar = veresiyeTutar, 
                                Tarih = DateTime.Now, 
                                MusteriId = musteriId.Value,
                                SatisId = satisId
                            }, islem);

                            baglanti.Execute("UPDATE Musteriler SET BorcMiktari = BorcMiktari + @Tutar WHERE Id = @Id", new { Tutar = veresiyeTutar, Id = musteriId.Value }, islem);
                        }
                        
                        islem.Commit();
                    }
                }

                // Satış kaydı başarılı -> Fiş Yazdır & Göstergeleri Yenile
                _yaziciServisi.FisYazdir(fisNo, DateTime.Now, _sepet.ToList(), genelToplam, odemeTuru);
                BugunkuSatisSayisiniGuncelle();

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Satış kaydedilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void BugunkuSatisSayisiniGuncelle()
        {
            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    int sayi = baglanti.ExecuteScalar<int>("SELECT COUNT(*) FROM Satislar WHERE Date(Tarih) = Date('now') AND IptalEdildi = 0");
                    if (txtBugunkuSatisSayisi != null)
                    {
                        txtBugunkuSatisSayisi.Text = $"{sayi} Adet";
                    }
                }
            }
            catch { }
        }

        private void BtnAnaMenu_Click(object sender, RoutedEventArgs e)
        {
            AnaMenuWindow.MenuyuGoster();
        }

        #region Bekleyen Sepet Havuzu (F7) & Fiyat Gör (F9)

        private void FiyatGorAc()
        {
            FiyatGorWindow fg = new FiyatGorWindow(_db);
            fg.Owner = Window.GetWindow(this);
            fg.ShowDialog();
            
            // Fiyat gör kapatıldıktan sonra odaklanmayı tekrar barkoda al
            txtGizliBarkod.Focus();
        }

        private void BtnParkEt_Click(object sender, RoutedEventArgs e)
        {
            if (_sepet.Count == 0)
            {
                MessageBox.Show("Sepette ürün yok, beklemeye alınamaz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var park = new ParkEdilenSatis
            {
                Tarih = DateTime.Now,
                Urunler = _sepet.Select(x => new SepetUrunu { UrunId = x.UrunId, Barkod = x.Barkod, UrunAdi = x.UrunAdi, BirimFiyat = x.BirimFiyat, Adet = x.Adet }).ToList(),
                Iskonto = _uygulananIskonto
            };

            _parkEdilenler.Add(park);
            SepetiTemizle();
            SlotButonlariniGuncelle();
            ShowToastNotification($"Sepet Beklemeye Alındı! (Bekleyen: {_parkEdilenler.Count})", "#E67E22");
        }

        private void BtnParktanCagir_Click(object sender, RoutedEventArgs e)
        {
            if (_parkEdilenler.Count == 0)
            {
                MessageBox.Show("Bekleyen sepet bulunmuyor.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_sepet.Count > 0)
            {
                var cevap = MessageBox.Show("Şu anki sepette ürünler var! Çağırma işlemi mevcut sepeti silecektir. Devam edilsin mi?", "Uyarı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (cevap != MessageBoxResult.Yes) return;
            }

            // En son bekletilen sepeti (LIFO - Last In First Out) geri getir
            var sonSepet = _parkEdilenler.Last();
            
            _sepet.Clear();
            foreach (var item in sonSepet.Urunler)
            {
                _sepet.Add(item);
            }
            _uygulananIskonto = sonSepet.Iskonto;

            _parkEdilenler.Remove(sonSepet);

            ToplamiGuncelle();
            SlotButonlariniGuncelle();
            ShowToastNotification("Bekleyen Sepet Ekrana Alındı", "#27AE60");
        }

        private void SlotButonlariniGuncelle()
        {
            if (_parkEdilenler.Count > 0)
            {
                btnParktanCagir.Content = $"PARKTAN ÇAĞIR ({_parkEdilenler.Count})";
                btnParktanCagir.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#C0392B"));
            }
            else
            {
                btnParktanCagir.Content = "PARKTAN ÇAĞIR";
                btnParktanCagir.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D35400"));
            }
        }

        #endregion

        private void SepetiTemizle()
        {
            _sepet.Clear();
            _numpadGirisi = "";
            _okunanBarkod = "";
            _uygulananIskonto = 0;
            txtGizliBarkod.Text = "Barkod bekleniyor...";
            ToplamiGuncelle();
        }

        #endregion
    }
}
