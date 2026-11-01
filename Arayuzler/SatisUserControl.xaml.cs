using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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

        // Klavye / barkod okuyucu / numpad ile yazılan giriş tamponu (ENTER ile barkod olarak işlenir)
        private string _okunanBarkod = "";

        // "X (Adet)" veya "*" ile belirlenen bir sonraki ürünün miktarı
        private string _numpadGirisi = "";

        // Son tamamlanan satış (F11 ile fiş tekrar basımı için)
        private int? _sonSatisId;

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
                parentWindow.PreviewTextInput -= ParentWindow_PreviewTextInput;
                parentWindow.PreviewKeyDown -= ParentWindow_PreviewKeyDown;
                parentWindow.PreviewTextInput += ParentWindow_PreviewTextInput;
                parentWindow.PreviewKeyDown += ParentWindow_PreviewKeyDown;
                parentWindow.Closing += ParentWindow_Closing;
            }
            txtKasiyer.Text = AktifOturum.AdSoyad ?? "-";
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
                parentWindow.Closing -= ParentWindow_Closing;
            }
        }

        private void ParentWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Sepette veya beklemede ürün varken pencere yanlışlıkla kapatılmasın
            if (_sepet.Count > 0 || _parkEdilenler.Count > 0)
            {
                var cevap = MessageBox.Show("Sepette veya beklemede tamamlanmamış satış var. Satış ekranı kapatılırsa bu sepetler silinir.\n\nYine de kapatılsın mı?",
                    "Tamamlanmamış Satış", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (cevap != MessageBoxResult.Yes) e.Cancel = true;
            }
        }

        #region Barkod / Klavye Dinleyici (Global Barcode Listener)

        /// <summary>Kullanıcı gerçekten bir metin kutusuna yazıyorsa (barkod gösterge kutusu hariç) global dinleyici devreye girmez.</summary>
        private bool DuzenlemeAlaniMi(object kaynak) => kaynak is TextBox tb && tb != txtGizliBarkod && !tb.IsReadOnly;

        private void ParentWindow_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (DuzenlemeAlaniMi(e.OriginalSource) || string.IsNullOrEmpty(e.Text)) return;

            char c = e.Text[0];
            if (char.IsControl(c)) return;

            // "3*" + barkod: miktar çarpanı (örn. 3 adet). "0,5*" gibi ondalık miktar da girilebilir.
            if (c == '*' && MiktarMi(_okunanBarkod))
            {
                MiktarCarpaniniAyarla();
                e.Handled = true;
                return;
            }

            _okunanBarkod += e.Text;
            GirisiGoster();
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
            if (e.Key == Key.F10) { BtnIade_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.F11) { BtnSonFis_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.F12) { BtnUrunAra_Click(null, null); e.Handled = true; return; }

            if (DuzenlemeAlaniMi(e.OriginalSource)) return;

            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                if (_okunanBarkod.Length > 0)
                {
                    string temizBarkod = _okunanBarkod.Trim();
                    _okunanBarkod = "";
                    txtGizliBarkod.Text = temizBarkod;
                    UrunuSepeteEkle(temizBarkod);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Back && _okunanBarkod.Length > 0)
            {
                _okunanBarkod = _okunanBarkod.Substring(0, _okunanBarkod.Length - 1);
                GirisiGoster();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _okunanBarkod = "";
                _numpadGirisi = "";
                txtGizliBarkod.Text = "Barkod bekleniyor...";
                e.Handled = true;
            }
            // Numpad +/- : seçili (veya son) satırın adedini değiştirir. Giriş yazılırken metin olarak kalır.
            else if (e.Key == Key.Add && _okunanBarkod.Length == 0)
            {
                BtnAdetArtir_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Subtract && _okunanBarkod.Length == 0)
            {
                BtnAdetAzalt_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Delete && _okunanBarkod.Length == 0)
            {
                BtnSatirSil_Click(null, null);
                e.Handled = true;
            }
        }

        private void GirisiGoster()
        {
            string carpan = string.IsNullOrEmpty(_numpadGirisi) ? "" : $"{_numpadGirisi} X ";
            txtGizliBarkod.Text = carpan + (_okunanBarkod.Length > 0 ? _okunanBarkod : "");
        }

        private static bool MiktarMi(string metin) => MiktarCozumle(metin).HasValue;

        private static decimal? MiktarCozumle(string metin)
        {
            if (string.IsNullOrWhiteSpace(metin) || metin.Length > 8) return null;
            if (decimal.TryParse(metin.Replace('.', ','), NumberStyles.Number, new CultureInfo("tr-TR"), out decimal sonuc) && sonuc > 0) return sonuc;
            return null;
        }

        private void MiktarCarpaniniAyarla()
        {
            _numpadGirisi = _okunanBarkod;
            _okunanBarkod = "";
            txtGizliBarkod.Text = $"{_numpadGirisi} X  (ürünü okutun)";
        }

        #endregion

        #region Ürün Ekleme ve Sepet İşlemleri

        private void UrunuSepeteEkle(string barkod, string varsayilanAd = null)
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();

                // 1. Terazi barkodu kontrolü (13 hane, ayarlardaki öneklerden biriyle başlar: varsayılan 27, 28, 29)
                // Biçim: PP KKKKK DDDDD C -> PP: önek, KKKKK: ürün kodu, DDDDD: gramaj (0.550 kg) veya fiyat (5,50 TL)
                string orjinalBarkod = barkod;
                decimal? teraziDegeri = null;
                string urunKodu = null;

                var onekler = AyarServisi.Oku(AyarServisi.TeraziOnekleri).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (barkod.Length == 13 && barkod.All(char.IsDigit) && onekler.Any(o => barkod.StartsWith(o.Trim())))
                {
                    urunKodu = barkod.Substring(2, 5);
                    if (decimal.TryParse(barkod.Substring(7, 5), out decimal okunan)) teraziDegeri = okunan;
                }

                Urun urun = baglanti.QueryFirstOrDefault<Urun>("SELECT * FROM Urunler WHERE Barkod = @Barkod", new { Barkod = orjinalBarkod });

                if (urun == null && urunKodu != null)
                {
                    // Terazi ürün kodu ürün kartına "00123", "123" veya "2700123" olarak girilmiş olabilir
                    urun = baglanti.QueryFirstOrDefault<Urun>(
                        "SELECT * FROM Urunler WHERE Barkod IN (@Kod, @KisaKod, @OnekliKod)",
                        new { Kod = urunKodu, KisaKod = urunKodu.TrimStart('0'), OnekliKod = orjinalBarkod.Substring(0, 7) });
                }
                else
                {
                    teraziDegeri = null; // Tam barkod eşleştiyse normal ürün olarak işle
                }

                if (urun != null)
                {
                    if (teraziDegeri.HasValue && teraziDegeri.Value > 0)
                    {
                        bool agirlikModu = AyarServisi.Oku(AyarServisi.TeraziBarkodTipi) != "Fiyat";
                        decimal miktar = agirlikModu ? teraziDegeri.Value / 1000m : 1m;
                        if (!agirlikModu) urun.SatisFiyati = teraziDegeri.Value / 100m;
                        _numpadGirisi = "";
                        SepeteUrunEkleVeyaGuncelle(urun, miktar, teraziUrunu: true);
                    }
                    else
                    {
                        SepeteUrunEkleVeyaGuncelle(urun);
                    }
                    SonUrunBilgisiniGoster(urun);
                }
                else
                {
                    // Ürün bulunamadı, Hızlı Ürün Ekleme Penceresini aç
                    System.Media.SystemSounds.Exclamation.Play();
                    HizliUrunEkleWindow pencere = new HizliUrunEkleWindow(urunKodu ?? barkod, _db);
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
                decimal sepettekiMiktar = _sepet.Where(x => x.UrunId == urun.Id).Sum(x => x.Adet);
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    decimal kalanStok = baglanti.ExecuteScalar<decimal>("SELECT StokMiktari FROM Urunler WHERE Id = @Id", new { Id = urun.Id });
                    txtSonUrunBilgisi.Text = $"{urun.UrunAdi} {urun.SatisFiyati:N2} ₺ - Kalan: {kalanStok - sepettekiMiktar:0.###}";
                }
            }
            catch
            {
                txtSonUrunBilgisi.Text = $"{urun.UrunAdi}";
            }
        }

        private void SepeteUrunEkleVeyaGuncelle(Urun urun, decimal? sabitMiktar = null, bool teraziUrunu = false)
        {
            decimal eklenecekAdet = sabitMiktar ?? MiktarCozumle(_numpadGirisi) ?? 1m;
            _numpadGirisi = "";

            // Stok kontrolü (ayarlardan eksi stoğa satış kapatılabilir)
            decimal sepettekiMiktar = _sepet.Where(x => x.UrunId == urun.Id).Sum(x => x.Adet);
            if (sepettekiMiktar + eklenecekAdet > urun.StokMiktari)
            {
                if (!AyarServisi.OkuBool(AyarServisi.EksiStokSatis))
                {
                    System.Media.SystemSounds.Hand.Play();
                    MessageBox.Show($"'{urun.UrunAdi}' için yeterli stok yok.\nMevcut stok: {urun.StokMiktari:0.###}", "Stok Yetersiz", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtGizliBarkod.Text = "Barkod bekleniyor...";
                    return;
                }
                ShowToastNotification($"⚠ Stok uyarısı: {urun.UrunAdi} (stok {urun.StokMiktari:0.###})", "#E67E22");
            }

            var sepettekiUrun = teraziUrunu ? null : _sepet.FirstOrDefault(x => x.UrunId == urun.Id && !x.TeraziUrunu && x.BirimFiyat == urun.SatisFiyati);
            if (sepettekiUrun != null)
            {
                sepettekiUrun.Adet += eklenecekAdet;
                dgSepet.SelectedItem = sepettekiUrun;
            }
            else
            {
                var yeni = new SepetUrunu
                {
                    UrunId = urun.Id,
                    Barkod = urun.Barkod,
                    UrunAdi = urun.UrunAdi,
                    BirimFiyat = urun.SatisFiyati,
                    KdvOrani = urun.KdvOrani,
                    AlisFiyati = urun.ToplamAlisMaliyet,
                    TeraziUrunu = teraziUrunu,
                    Adet = eklenecekAdet
                };
                _sepet.Add(yeni);
                dgSepet.SelectedItem = yeni;
            }

            ToplamiGuncelle();

            if (dgSepet.SelectedItem != null)
            {
                dgSepet.ScrollIntoView(dgSepet.SelectedItem);
            }
        }

        private decimal GenelToplam()
        {
            decimal toplam = Math.Round(_sepet.Sum(x => x.ToplamFiyat), 2) - _uygulananIskonto;
            return toplam < 0 ? 0 : toplam;
        }

        private void ToplamiGuncelle()
        {
            if (_uygulananIskonto > 0)
            {
                txtIskonto.Text = $"Uygulanan İskonto: {_uygulananIskonto:N2} ₺";
                txtIskonto.Visibility = Visibility.Visible;
            }
            else
            {
                txtIskonto.Visibility = Visibility.Collapsed;
            }

            txtGenelToplam.Text = $"{GenelToplam():N2} ₺";
            txtSepetOzeti.Text = $"{_sepet.Count} kalem / {_sepet.Sum(x => x.TeraziUrunu ? 1 : x.Adet):0.###} ürün";
        }

        private SepetUrunu SeciliVeyaSonSatir()
        {
            return dgSepet.SelectedItem as SepetUrunu ?? _sepet.LastOrDefault();
        }

        private void BtnAdetArtir_Click(object sender, RoutedEventArgs e)
        {
            var satir = SeciliVeyaSonSatir();
            if (satir == null || satir.TeraziUrunu) return;
            satir.Adet += 1;
            ToplamiGuncelle();
            txtGizliBarkod.Focus();
        }

        private void BtnAdetAzalt_Click(object sender, RoutedEventArgs e)
        {
            var satir = SeciliVeyaSonSatir();
            if (satir == null) return;

            if (satir.TeraziUrunu || satir.Adet <= 1)
            {
                BtnSatirSil_Click(null, null);
                return;
            }

            if (!YoneticiOnayWindow.YetkiDogrula(Window.GetWindow(this), $"Adet azaltma: {satir.UrunAdi}")) return;
            satir.Adet -= 1;
            ToplamiGuncelle();
            txtGizliBarkod.Focus();
        }

        private void BtnSatirSil_Click(object sender, RoutedEventArgs e)
        {
            var satir = SeciliVeyaSonSatir();
            if (satir == null) return;

            if (!YoneticiOnayWindow.YetkiDogrula(Window.GetWindow(this), $"Satır iptali: {satir.UrunAdi} x{satir.Adet:0.###}")) return;
            _sepet.Remove(satir);
            if (_sepet.Count == 0) _uygulananIskonto = 0;
            ToplamiGuncelle();
            ShowToastNotification($"Satır silindi: {satir.UrunAdi}", "#E74C3C");
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
            if (sender is Button btn) UrunAraAc(btn.Tag?.ToString());
        }

        private void BtnUrunAra_Click(object sender, RoutedEventArgs e) => UrunAraAc(null);

        private void UrunAraAc(string arama)
        {
            var pencere = new UrunAraWindow(arama);
            var owner = Window.GetWindow(this);
            if (owner != null && owner.IsLoaded) pencere.Owner = owner;
            if (pencere.ShowDialog() == true && pencere.SecilenUrun != null)
            {
                SepeteUrunEkleVeyaGuncelle(pencere.SecilenUrun);
                SonUrunBilgisiniGoster(pencere.SecilenUrun);
            }
            txtGizliBarkod.Focus();
        }

        #endregion

        #region Numpad İşlemleri

        private void Numpad_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                _okunanBarkod += btn.Content.ToString();
                GirisiGoster();
            }
        }

        private void NumpadTemizle_Click(object sender, RoutedEventArgs e)
        {
            _numpadGirisi = "";
            _okunanBarkod = "";
            txtGizliBarkod.Text = "Giriş temizlendi.";
        }

        private void NumpadSil_Click(object sender, RoutedEventArgs e)
        {
            if (_okunanBarkod.Length > 0) _okunanBarkod = _okunanBarkod.Substring(0, _okunanBarkod.Length - 1);
            GirisiGoster();
        }

        private void NumpadEnter_Click(object sender, RoutedEventArgs e)
        {
            if (_okunanBarkod.Length == 0) return;
            string barkod = _okunanBarkod.Trim();
            _okunanBarkod = "";
            UrunuSepeteEkle(barkod);
        }

        private void AdetCarp_Click(object sender, RoutedEventArgs e)
        {
            decimal? miktar = MiktarCozumle(_okunanBarkod);
            if (!miktar.HasValue)
            {
                txtGizliBarkod.Text = "Önce miktarı yazın, sonra X'e basın.";
                return;
            }
            MiktarCarpaniniAyarla();
        }

        #endregion

        #region Kısayol İşlemleri

        private void BtnNakit_Click(object sender, RoutedEventArgs e)
        {
            if (_sepet.Count == 0) return;

            decimal genelToplam = GenelToplam();

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

            decimal genelToplam = GenelToplam();

            string geciciFisNo = "FIS" + DateTime.Now.ToString("yyMMddHHmmss");
            string posPort = AyarServisi.Oku(AyarServisi.PosComPort);

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

            decimal genelToplam = GenelToplam();

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
                    if (!YoneticiOnayWindow.YetkiDogrula(Window.GetWindow(this), $"Sepet iptali ({GenelToplam():N2} TL, {_sepet.Count} kalem)")) return;

                    SepetiTemizle();
                    ShowToastNotification("Sepet Temizlendi", "#E74C3C");
                }
            }
        }

        private void BtnIskonto_Click(object sender, RoutedEventArgs e)
        {
            if (_sepet.Count == 0) return;

            decimal araToplam = _sepet.Sum(x => x.ToplamFiyat);
            IskontoWindow pencere = new IskontoWindow(araToplam);
            var owner = Window.GetWindow(this);
            if (owner != null && owner.IsLoaded) pencere.Owner = owner;

            if (pencere.ShowDialog() == true)
            {
                decimal indirim = Math.Min(Math.Round(pencere.UygulananIndirim, 2), araToplam);
                if (indirim > 0 && !YoneticiOnayWindow.YetkiDogrula(owner, $"İskonto {indirim:N2} TL")) return;
                _uygulananIskonto = indirim;
                ToplamiGuncelle();
            }

            txtGizliBarkod.Focus();
        }

        private void BtnParaUstu_Click(object sender, RoutedEventArgs e)
        {
            decimal genelToplam = GenelToplam();
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

            decimal genelToplam = GenelToplam();

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

        private void BtnIade_Click(object sender, RoutedEventArgs e)
        {
            var pencere = new IadeIslemWindow();
            var owner = Window.GetWindow(this);
            if (owner != null && owner.IsLoaded) pencere.Owner = owner;
            pencere.ShowDialog();
            BugunkuSatisSayisiniGuncelle();
            txtGizliBarkod.Focus();
        }

        private void BtnSonFis_Click(object sender, RoutedEventArgs e)
        {
            if (!_sonSatisId.HasValue)
            {
                ShowToastNotification("Bu oturumda tamamlanmış satış yok.", "#7F8C8D");
                return;
            }
            var fis = new SatisServisi().FisBilgisiGetir(_sonSatisId.Value);
            if (fis != null && _yaziciServisi.FisYazdir(fis))
                ShowToastNotification("Son fiş tekrar yazdırıldı", "#3498DB");
            else
                ShowToastNotification("Fiş yazdırılamadı - yazıcı ayarlarını kontrol edin", "#E74C3C");
        }

        private void ShowToastNotification(string mesaj, string hexRenk)
        {
            txtToastMesaj.Text = mesaj;

            var converter = new System.Windows.Media.BrushConverter();
            var brush = (System.Windows.Media.Brush)converter.ConvertFromString(hexRenk);
            brdToast.Background = brush;

            brdToast.Visibility = Visibility.Visible;
            brdToast.Opacity = 1.0;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, ev) =>
            {
                brdToast.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();

            txtGizliBarkod.Focus();
        }

        private bool SatisKaydet(string odemeTuru, int? musteriId = null, string musteriAd = null,
            decimal nakitTutar = 0, decimal kartTutar = 0, decimal veresiyeTutar = 0)
        {
            decimal genelToplam = GenelToplam();
            DateTime tarih = DateTime.Now;
            string fisNo = "FIS" + tarih.ToString("yyMMddHHmmssfff");
            string satisNumarasi;
            int satisId;

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    using (var islem = baglanti.BeginTransaction())
                    {
                        // Satış numarası işlem içinde üretilir ki aynı numara iki kez verilmesin
                        string gunOneki = $"SAT-{tarih:yyyyMMdd}-";
                        string sonNumara = baglanti.ExecuteScalar<string>(
                            "SELECT SatisNumarasi FROM Satislar WHERE SatisNumarasi LIKE @Prefix ORDER BY SatisNumarasi DESC LIMIT 1",
                            new { Prefix = gunOneki + "%" }, islem);
                        int sira = sonNumara != null && int.TryParse(sonNumara.Substring(gunOneki.Length), out int s) ? s + 1 : 1;
                        satisNumarasi = $"{gunOneki}{sira:D4}";

                        string satisSorgu = @"
                            INSERT INTO Satislar (FisNo, ToplamTutar, OdemeTuru, Tarih, IptalEdildi, NakitTutar, KartTutar, VeresiyeTutar, IskontoTutari, MusteriId, MusteriAdi, SatisNumarasi, KullaniciId, KullaniciAdi)
                            VALUES (@FisNo, @ToplamTutar, @OdemeTuru, @Tarih, 0, @NakitTutar, @KartTutar, @VeresiyeTutar, @IskontoTutari, @MusteriId, @MusteriAdi, @SatisNumarasi, @KullaniciId, @KullaniciAdi);
                            SELECT last_insert_rowid();";
                        satisId = baglanti.ExecuteScalar<int>(satisSorgu, new
                        {
                            FisNo = fisNo, ToplamTutar = genelToplam, OdemeTuru = odemeTuru, Tarih = tarih,
                            NakitTutar = nakitTutar, KartTutar = kartTutar, VeresiyeTutar = veresiyeTutar, IskontoTutari = _uygulananIskonto,
                            MusteriId = musteriId, MusteriAdi = musteriAd, SatisNumarasi = satisNumarasi,
                            KullaniciId = AktifOturum.KullaniciId == 0 ? (int?)null : AktifOturum.KullaniciId,
                            KullaniciAdi = AktifOturum.KullaniciAdi ?? "Sistem"
                        }, islem);

                        foreach (var sepetUrunu in _sepet)
                        {
                            string detaySorgu = @"
                                INSERT INTO SatisDetaylari (SatisId, UrunId, UrunAdi, Adet, BirimFiyat, ToplamFiyat, KdvOrani, AlisFiyati)
                                VALUES (@SatisId, @UrunId, @UrunAdi, @Adet, @BirimFiyat, @ToplamFiyat, @KdvOrani, @AlisFiyati)";
                            baglanti.Execute(detaySorgu, new
                            {
                                SatisId = satisId,
                                sepetUrunu.UrunId,
                                sepetUrunu.UrunAdi,
                                sepetUrunu.Adet,
                                sepetUrunu.BirimFiyat,
                                ToplamFiyat = Math.Round(sepetUrunu.ToplamFiyat, 2),
                                sepetUrunu.KdvOrani,
                                sepetUrunu.AlisFiyati
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
                                Aciklama = $"Veresiye Satış (No: {satisNumarasi})",
                                Tutar = veresiyeTutar,
                                Tarih = tarih,
                                MusteriId = musteriId.Value,
                                SatisId = satisId
                            }, islem);

                            baglanti.Execute("UPDATE Musteriler SET BorcMiktari = BorcMiktari + @Tutar WHERE Id = @Id", new { Tutar = veresiyeTutar, Id = musteriId.Value }, islem);
                        }

                        islem.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                LogServisi.Hata("Satış kaydedilemedi", ex);
                MessageBox.Show($"Satış kaydedilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            _sonSatisId = satisId;
            BugunkuSatisSayisiniGuncelle();

            // Satış kaydı başarılı -> Fiş yazdır / çekmece aç (yazıcı hatası satışı geri almaz)
            try
            {
                bool cekmece = nakitTutar > 0 && AyarServisi.OkuBool(AyarServisi.CekmeceAc);
                string mod = AyarServisi.Oku(AyarServisi.FisYazdirmaModu);
                bool yazdir = mod == "Otomatik" ||
                    (mod == "Sor" && MessageBox.Show("Fiş yazdırılsın mı?", "Fiş", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);

                if (yazdir && YaziciServisi.YaziciTanimli)
                {
                    var fis = new FisBilgisi
                    {
                        FisNo = fisNo,
                        SatisNumarasi = satisNumarasi,
                        Tarih = tarih,
                        Urunler = _sepet.ToList(),
                        Iskonto = _uygulananIskonto,
                        ToplamTutar = genelToplam,
                        OdemeTuru = odemeTuru,
                        NakitTutar = nakitTutar,
                        KartTutar = kartTutar,
                        VeresiyeTutar = veresiyeTutar,
                        MusteriAdi = musteriAd,
                        Kasiyer = AktifOturum.AdSoyad
                    };
                    if (!_yaziciServisi.FisYazdir(fis, cekmece))
                        ShowToastNotification("⚠ Satış kaydedildi ancak fiş yazdırılamadı", "#E67E22");
                }
                else if (cekmece)
                {
                    _yaziciServisi.CekmeceyiAc();
                }
            }
            catch (Exception ex)
            {
                LogServisi.Hata("Fiş yazdırma hatası", ex);
            }

            return true;
        }

        private void BugunkuSatisSayisiniGuncelle()
        {
            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    int sayi = baglanti.ExecuteScalar<int>("SELECT COUNT(*) FROM Satislar WHERE Tarih >= @Bugun AND Tarih < @Yarin AND IptalEdildi = 0",
                        new { Bugun = DateTime.Today, Yarin = DateTime.Today.AddDays(1) });
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

        private void BtnFiyatGor_Click(object sender, RoutedEventArgs e) => FiyatGorAc();

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
                Urunler = _sepet.ToList(),
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
                var cevap = MessageBox.Show("Şu anki sepette ürünler var! Mevcut sepet beklemeye alınıp bekleyen sepet çağrılsın mı?", "Uyarı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (cevap != MessageBoxResult.Yes) return;
            }

            // En son bekletilen sepeti (LIFO - Last In First Out) geri getir
            var sonSepet = _parkEdilenler.Last();
            _parkEdilenler.Remove(sonSepet);

            // Ekrandaki sepet kaybolmasın: beklemeye alınır (sepetler yer değiştirir)
            if (_sepet.Count > 0)
            {
                _parkEdilenler.Insert(0, new ParkEdilenSatis { Tarih = DateTime.Now, Urunler = _sepet.ToList(), Iskonto = _uygulananIskonto });
            }

            _sepet.Clear();
            foreach (var item in sonSepet.Urunler)
            {
                _sepet.Add(item);
            }
            _uygulananIskonto = sonSepet.Iskonto;

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
