using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MarketPOS.Servisler;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public class AyarModeli
    {
        public string Anahtar { get; set; }
        public string Deger { get; set; }
    }

    public partial class AyarlarUserControl : UserControl
    {
        private readonly PosCihazServisi _posServisi;
        private readonly YaziciServisi _yaziciServisi;

        public AyarlarUserControl()
        {
            InitializeComponent();
            _posServisi = new PosCihazServisi();
            _yaziciServisi = new YaziciServisi();
        }

        private void BtnAnaMenu_Click(object sender, RoutedEventArgs e)
        {
            AnaMenuWindow.MenuyuGoster();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            YazicilariYukle();
            PortlariYukle();
            AyarlariYukle();
            YedekBilgisiniGuncelle();
        }

        private void YazicilariYukle()
        {
            cmbYazici.Items.Clear();
            cmbYazici.Items.Add("");   // Boş seçim = fiş basma
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                cmbYazici.Items.Add(printer);
            }
        }

        private void PortlariYukle()
        {
            // Bilgisayarda gerçekten bulunan portlar + standart COM1-COM9
            var portlar = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            try { foreach (var p in System.IO.Ports.SerialPort.GetPortNames()) portlar.Add(p); } catch { }
            for (int i = 1; i <= 9; i++) portlar.Add("COM" + i);

            cmbComPort.Items.Clear();
            foreach (var p in portlar.OrderBy(p => p.Length).ThenBy(p => p)) cmbComPort.Items.Add(p);
        }

        private static void TagIleSec(ComboBox combo, string deger)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag?.ToString() == deger) { combo.SelectedItem = item; return; }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private static string SeciliTag(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

        private void AyarlariYukle()
        {
            try
            {
                txtDukkanAdi.Text = AyarServisi.Oku(AyarServisi.DukkanAdi);
                txtDukkanAdres.Text = AyarServisi.Oku(AyarServisi.DukkanAdres);
                txtDukkanTelefon.Text = AyarServisi.Oku(AyarServisi.DukkanTelefon);
                txtVergiBilgisi.Text = AyarServisi.Oku(AyarServisi.VergiBilgisi);
                txtFisAltNotu.Text = AyarServisi.Oku(AyarServisi.FisAltNotu);

                string port = AyarServisi.Oku(AyarServisi.PosComPort);
                if (!cmbComPort.Items.Contains(port)) cmbComPort.Items.Add(port);
                cmbComPort.SelectedItem = port;

                string yazici = AyarServisi.Oku(AyarServisi.YaziciAdi);
                if (!string.IsNullOrEmpty(yazici) && !cmbYazici.Items.Contains(yazici)) cmbYazici.Items.Add(yazici);
                cmbYazici.SelectedItem = yazici;

                TagIleSec(cmbFisGenisligi, AyarServisi.OkuInt(AyarServisi.FisGenisligi, 32) >= 48 ? "48" : "32");
                TagIleSec(cmbFisModu, AyarServisi.Oku(AyarServisi.FisYazdirmaModu));
                chkCekmece.IsChecked = AyarServisi.OkuBool(AyarServisi.CekmeceAc);
                chkEksiStok.IsChecked = AyarServisi.OkuBool(AyarServisi.EksiStokSatis);
                txtTeraziOnekleri.Text = AyarServisi.Oku(AyarServisi.TeraziOnekleri);
                TagIleSec(cmbTeraziTipi, AyarServisi.Oku(AyarServisi.TeraziBarkodTipi));
                txtKritikStok.Text = AyarServisi.Oku(AyarServisi.KritikStokSeviyesi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayarlar yüklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtKritikStok.Text.Trim(), out decimal kritik) || kritik < 0)
            {
                MessageBox.Show("Kritik stok seviyesi geçerli bir sayı olmalıdır.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string onekler = string.Join(",", txtTeraziOnekleri.Text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(o => o.Trim()).Where(o => o.Length == 2 && o.All(char.IsDigit)));

            try
            {
                AyarServisi.Kaydet(new Dictionary<string, string>
                {
                    { AyarServisi.DukkanAdi, txtDukkanAdi.Text.Trim() },
                    { AyarServisi.DukkanAdres, txtDukkanAdres.Text.Trim() },
                    { AyarServisi.DukkanTelefon, txtDukkanTelefon.Text.Trim() },
                    { AyarServisi.VergiBilgisi, txtVergiBilgisi.Text.Trim() },
                    { AyarServisi.FisAltNotu, txtFisAltNotu.Text.Trim() },
                    { AyarServisi.PosComPort, cmbComPort.SelectedItem?.ToString() ?? "COM3" },
                    { AyarServisi.YaziciAdi, cmbYazici.SelectedItem?.ToString() ?? "" },
                    { AyarServisi.FisGenisligi, SeciliTag(cmbFisGenisligi) },
                    { AyarServisi.FisYazdirmaModu, SeciliTag(cmbFisModu) },
                    { AyarServisi.CekmeceAc, chkCekmece.IsChecked == true ? "1" : "0" },
                    { AyarServisi.EksiStokSatis, chkEksiStok.IsChecked == true ? "1" : "0" },
                    { AyarServisi.TeraziOnekleri, onekler },
                    { AyarServisi.TeraziBarkodTipi, SeciliTag(cmbTeraziTipi) },
                    { AyarServisi.KritikStokSeviyesi, kritik.ToString() }
                });

                LogServisi.Bilgi("Sistem ayarları güncellendi");
                txtTeraziOnekleri.Text = onekler;
                MessageBox.Show("Sistem ayarları başarıyla kaydedildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayarlar kaydedilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnTestPos_Click(object sender, RoutedEventArgs e)
        {
            string posPort = cmbComPort.SelectedItem?.ToString() ?? "COM3";
            var sonuc = _posServisi.SatisGonder(1.00m, "TEST" + DateTime.Now.ToString("HHmmss"), posPort, 3);

            if (sonuc.Basarili)
            {
                MessageBox.Show($"POS Cihazı Testi BAŞARILI!\n\nPort: {posPort}\nYanıt: {sonuc.Mesaj}\nOnay Kodu: {sonuc.OnayKodu}", "POS Testi Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"POS Cihazı Testi Sonucu:\n\nPort: {posPort}\nMesaj: {sonuc.Mesaj}\n\nNot: Fiziksel POS bağlı değilse bu normaldir. Simülasyon modunu Satış ekranında F2 ile deneyebilirsiniz.", "POS Test Bilgisi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnTestYazici_Click(object sender, RoutedEventArgs e)
        {
            string yazici = cmbYazici.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(yazici))
            {
                MessageBox.Show("Lütfen önce listeden bir termal yazıcı seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_yaziciServisi.DenemeFisiYazdir(yazici))
            {
                MessageBox.Show($"'{yazici}' yazıcısına deneme fişi gönderildi.\n\nNot: Fiş başlığı kayıtlı ayarlardan alınır; değişiklik yaptıysanız önce kaydedin.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"'{yazici}' yazıcısına fiş yazdırılırken hata oluştu.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Yedekleme

        private void YedekBilgisiniGuncelle()
        {
            try
            {
                var son = new DirectoryInfo(OtomatikYedeklemeServisi.YedekKlasoru).GetFiles("Yedek_*.db")
                    .OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                txtYedekBilgi.Text = $"Veritabanı: {VeritabaniYoneticisi.VeritabaniYolu}\n" +
                    (son != null ? $"Son yedek: {son.LastWriteTime:dd.MM.yyyy HH:mm} ({son.Length / 1024} KB)" : "Henüz yedek yok.");
            }
            catch { }
        }

        private void BtnYedekAl_Click(object sender, RoutedEventArgs e)
        {
            string yol = new OtomatikYedeklemeServisi().YedekAl();
            YedekBilgisiniGuncelle();
            if (yol != null)
                MessageBox.Show($"Yedek alındı:\n{yol}", "Yedekleme", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("Yedek alınamadı. Ayrıntılar için Loglar klasörüne bakın.", "Yedekleme", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void BtnYedekKlasoru_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(OtomatikYedeklemeServisi.YedekKlasoru);
                Process.Start("explorer.exe", $"\"{OtomatikYedeklemeServisi.YedekKlasoru}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnGeriYukle_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Geri yüklenecek yedeği seçin",
                Filter = "MarketPOS Yedeği (*.db;*.sqlite)|*.db;*.sqlite",
                InitialDirectory = OtomatikYedeklemeServisi.YedekKlasoru
            };
            if (dialog.ShowDialog() != true) return;

            var cevap = MessageBox.Show(
                $"DİKKAT! Mevcut tüm veriler seçilen yedekteki verilerle DEĞİŞTİRİLECEK:\n\n{Path.GetFileName(dialog.FileName)}\n\n" +
                "Geri yüklemeden önce mevcut durumun güvenlik yedeği otomatik alınır.\nDevam edilsin mi?",
                "Yedekten Geri Yükle", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (cevap != MessageBoxResult.Yes) return;

            try
            {
                new OtomatikYedeklemeServisi().GeriYukle(dialog.FileName);
                MessageBox.Show("Yedek başarıyla geri yüklendi.\nAçık olan satış/stok ekranlarını kapatıp yeniden açın.", "Geri Yükleme", MessageBoxButton.OK, MessageBoxImage.Information);
                AyarlariYukle();
                YedekBilgisiniGuncelle();
            }
            catch (Exception ex)
            {
                LogServisi.Hata("Geri yükleme hatası", ex);
                MessageBox.Show($"Geri yükleme başarısız:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
