using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Dapper;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public partial class UrunEkleDuzenleWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        private readonly Urun _mevcutUrun;
        private readonly bool _duzenlemeModu;
        private string _secilenFotografYolu = "";
        private readonly string _uygulamaFotoKlasoru;

        public UrunEkleDuzenleWindow(VeritabaniYoneticisi db, Urun urun = null)
        {
            InitializeComponent();
            _db = db;
            _mevcutUrun = urun;
            _duzenlemeModu = urun != null;

            // Fotoğraf klasörünü ayarla (Uygulamanın çalıştığı dizinde UrunFotograflari klasörü)
            _uygulamaFotoKlasoru = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UrunFotograflari");
            if (!Directory.Exists(_uygulamaFotoKlasoru))
            {
                Directory.CreateDirectory(_uygulamaFotoKlasoru);
            }

            if (_duzenlemeModu)
            {
                txtBaslik.Text = "ÜRÜN BİLGİLERİNİ GÜNCELLE";
                txtBarkod.Text = _mevcutUrun.Barkod;
                txtUrunAdi.Text = _mevcutUrun.UrunAdi;
                txtKategori.Text = _mevcutUrun.Kategori ?? "";
                txtFirma.Text = _mevcutUrun.Firma ?? "";
                txtAlisFiyati.Text = _mevcutUrun.AlisFiyati.ToString("N2");
                txtSatisFiyati.Text = _mevcutUrun.SatisFiyati.ToString("N2");
                txtStokMiktari.Text = _mevcutUrun.StokMiktari.ToString("G");
                chkKdvDahil.IsChecked = _mevcutUrun.KdvDahilMi;

                if (_mevcutUrun.KdvOrani == 1m) cmbKdvOrani.SelectedIndex = 0;
                else if (_mevcutUrun.KdvOrani == 20m) cmbKdvOrani.SelectedIndex = 2;
                else cmbKdvOrani.SelectedIndex = 1;

                // Fotoğraf yükleme
                if (!string.IsNullOrEmpty(_mevcutUrun.FotografYolu) && File.Exists(_mevcutUrun.FotografYolu))
                {
                    _secilenFotografYolu = _mevcutUrun.FotografYolu;
                    FotografGoster(_secilenFotografYolu);
                }
            }
            else
            {
                dpSktTarihi.SelectedDate = DateTime.Today.AddMonths(6);
                txtPartiNo.Text = "PRT" + DateTime.Now.ToString("yyMMdd");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (_duzenlemeModu) txtUrunAdi.Focus();
                else txtBarkod.Focus();
            }));

            CanliHesapla();
        }

        private void HesaplamaParametreleri_Changed(object sender, RoutedEventArgs e)
        {
            CanliHesapla();
        }

        private void CanliHesapla()
        {
            if (lblNetAlis == null || lblKdvTutari == null || lblKarMarji == null) return;

            decimal.TryParse(txtAlisFiyati?.Text, out decimal alisFiyati);
            decimal.TryParse(txtSatisFiyati?.Text, out decimal satisFiyati);

            decimal kdvOrani = 10m;
            if (cmbKdvOrani?.SelectedIndex == 0) kdvOrani = 1m;
            else if (cmbKdvOrani?.SelectedIndex == 2) kdvOrani = 20m;

            bool kdvDahil = chkKdvDahil?.IsChecked == true;

            decimal netAlis = kdvDahil ? (alisFiyati / (1m + (kdvOrani / 100m))) : alisFiyati;
            decimal kdvTutari = kdvDahil ? (alisFiyati - netAlis) : (alisFiyati * (kdvOrani / 100m));
            decimal toplamMaliyet = kdvDahil ? alisFiyati : (alisFiyati + kdvTutari);

            lblNetAlis.Text = $"Net Alış: {netAlis:N2} ₺";
            lblKdvTutari.Text = $"KDV Tutarı: {kdvTutari:N2} ₺";

            if (toplamMaliyet > 0 && satisFiyati > 0)
            {
                decimal karMarji = ((satisFiyati - toplamMaliyet) / toplamMaliyet) * 100m;
                lblKarMarji.Text = $"%{karMarji:F1} Kar";
            }
            else
            {
                lblKarMarji.Text = "%0.0 Kar";
            }
        }

        #region Fotoğraf İşlemleri

        private void BtnFotoSec_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Ürün Fotoğrafı Seç";
            dialog.Filter = "Görüntü Dosyaları (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png";

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Seçilen dosyayı uygulamanın kendi klasörüne kopyala
                    string dosyaUzantisi = Path.GetExtension(dialog.FileName);
                    string yeniDosyaAdi = $"URUN_{DateTime.Now:yyyyMMddHHmmssfff}{dosyaUzantisi}";
                    string hedefYol = Path.Combine(_uygulamaFotoKlasoru, yeniDosyaAdi);

                    File.Copy(dialog.FileName, hedefYol, true);

                    _secilenFotografYolu = hedefYol;
                    FotografGoster(_secilenFotografYolu);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fotoğraf yüklenirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnFotoSil_Click(object sender, RoutedEventArgs e)
        {
            _secilenFotografYolu = "";
            imgUrunFoto.Source = null;
            txtFotoYerTutucu.Visibility = Visibility.Visible;
        }

        private void FotografGoster(string yol)
        {
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // Dosya kilidini önlemek için
                bitmap.UriSource = new Uri(yol);
                bitmap.EndInit();
                
                imgUrunFoto.Source = bitmap;
                txtFotoYerTutucu.Visibility = Visibility.Collapsed;
            }
            catch
            {
                // Fotoğraf bulunamazsa veya bozuksa
                imgUrunFoto.Source = null;
                txtFotoYerTutucu.Visibility = Visibility.Visible;
                txtFotoYerTutucu.Text = "Fotoğraf Bulunamadı!";
            }
        }

        #endregion

        private void Kaydet_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBarkod.Text))
            {
                MessageBox.Show("Lütfen barkod giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtBarkod.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtUrunAdi.Text))
            {
                MessageBox.Show("Lütfen ürün adını giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtUrunAdi.Focus();
                return;
            }

            if (!decimal.TryParse(txtSatisFiyati.Text, out decimal satisFiyati) || satisFiyati < 0)
            {
                MessageBox.Show("Geçerli bir satış fiyatı giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtSatisFiyati.Focus();
                return;
            }

            decimal.TryParse(txtAlisFiyati.Text, out decimal alisFiyati);
            decimal.TryParse(txtStokMiktari.Text, out decimal eklenenStok);

            decimal kdvOrani = 10m;
            if (cmbKdvOrani.SelectedIndex == 0) kdvOrani = 1m;
            else if (cmbKdvOrani.SelectedIndex == 2) kdvOrani = 20m;

            bool kdvDahil = chkKdvDahil.IsChecked == true;

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    using (var islem = baglanti.BeginTransaction())
                    {
                        int urunId = 0;

                        if (_duzenlemeModu)
                        {
                            urunId = _mevcutUrun.Id;
                            string sorgu = @"
                                UPDATE Urunler 
                                SET Barkod = @Barkod, UrunAdi = @UrunAdi, SatisFiyati = @SatisFiyati, 
                                    AlisFiyati = @AlisFiyati, KdvOrani = @KdvOrani, KdvDahilMi = @KdvDahilMi,
                                    StokMiktari = StokMiktari + @EklenenStok, Kategori = @Kategori, Firma = @Firma, 
                                    FotografYolu = @FotografYolu, GuncellenmeTarihi = @GuncellenmeTarihi
                                WHERE Id = @Id";

                            baglanti.Execute(sorgu, new
                            {
                                Barkod = txtBarkod.Text.Trim(),
                                UrunAdi = txtUrunAdi.Text.Trim(),
                                SatisFiyati = satisFiyati,
                                AlisFiyati = alisFiyati,
                                KdvOrani = kdvOrani,
                                KdvDahilMi = kdvDahil ? 1 : 0,
                                EklenenStok = eklenenStok,
                                Kategori = txtKategori.Text.Trim(),
                                Firma = txtFirma.Text.Trim(),
                                FotografYolu = _secilenFotografYolu,
                                GuncellenmeTarihi = DateTime.Now,
                                Id = urunId
                            }, islem);
                        }
                        else
                        {
                            string sorgu = @"
                                INSERT INTO Urunler (Barkod, UrunAdi, SatisFiyati, AlisFiyati, KdvOrani, KdvDahilMi, StokMiktari, Kategori, Firma, FotografYolu, OlusturulmaTarihi, GuncellenmeTarihi)
                                VALUES (@Barkod, @UrunAdi, @SatisFiyati, @AlisFiyati, @KdvOrani, @KdvDahilMi, @StokMiktari, @Kategori, @Firma, @FotografYolu, @OlusturulmaTarihi, @GuncellenmeTarihi);
                                SELECT last_insert_rowid();";

                            urunId = baglanti.ExecuteScalar<int>(sorgu, new
                            {
                                Barkod = txtBarkod.Text.Trim(),
                                UrunAdi = txtUrunAdi.Text.Trim(),
                                SatisFiyati = satisFiyati,
                                AlisFiyati = alisFiyati,
                                KdvOrani = kdvOrani,
                                KdvDahilMi = kdvDahil ? 1 : 0,
                                StokMiktari = eklenenStok,
                                Kategori = txtKategori.Text.Trim(),
                                Firma = txtFirma.Text.Trim(),
                                FotografYolu = _secilenFotografYolu,
                                OlusturulmaTarihi = DateTime.Now,
                                GuncellenmeTarihi = DateTime.Now
                            }, islem);
                        }

                        // Eğer stok eklendiyse (veya yeni ürünse) Stok Partisi oluştur
                        if (eklenenStok > 0)
                        {
                            string partiSorgu = @"
                                INSERT INTO UrunPartileri (UrunId, PartiNo, StokMiktari, SktTarihi, EklenmeTarihi)
                                VALUES (@UrunId, @PartiNo, @StokMiktari, @SktTarihi, @EklenmeTarihi)";

                            baglanti.Execute(partiSorgu, new
                            {
                                UrunId = urunId,
                                PartiNo = string.IsNullOrWhiteSpace(txtPartiNo.Text) ? "BİLİNMİYOR" : txtPartiNo.Text.Trim(),
                                StokMiktari = eklenenStok,
                                SktTarihi = dpSktTarihi.SelectedDate ?? DateTime.Today.AddYears(1),
                                EklenmeTarihi = DateTime.Now
                            }, islem);
                        }

                        islem.Commit();
                    }
                }

                MessageBox.Show("Ürün başarıyla kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("UNIQUE constraint failed"))
                {
                    MessageBox.Show("Bu barkoda sahip başka bir ürün zaten var!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Kayıt sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Iptal_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
