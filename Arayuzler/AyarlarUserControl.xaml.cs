using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
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
        private readonly VeritabaniYoneticisi _db;
        private readonly PosCihazServisi _posServisi;
        private readonly YaziciServisi _yaziciServisi;

        public AyarlarUserControl()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
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
            AyarlariYukle();
        }

        private void YazicilariYukle()
        {
            cmbYazici.Items.Clear();
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                cmbYazici.Items.Add(printer);
            }

            PrinterSettings defaultSettings = new PrinterSettings();
            if (!string.IsNullOrEmpty(defaultSettings.PrinterName) && cmbYazici.Items.Contains(defaultSettings.PrinterName))
            {
                cmbYazici.SelectedItem = defaultSettings.PrinterName;
            }
            else if (cmbYazici.Items.Count > 0)
            {
                cmbYazici.SelectedIndex = 0;
            }
        }

        private void AyarlariYukle()
        {
            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    var ayarListesi = baglanti.Query<AyarModeli>("SELECT Anahtar, Deger FROM Ayarlar").ToList();
                    var ayarlar = ayarListesi.ToDictionary(x => x.Anahtar, x => x.Deger);

                    if (ayarlar.ContainsKey("DukkanAdi")) txtDukkanAdi.Text = ayarlar["DukkanAdi"];
                    else txtDukkanAdi.Text = "SÜPERMARKET POS";

                    if (ayarlar.ContainsKey("FisAltNotu")) txtFisAltNotu.Text = ayarlar["FisAltNotu"];
                    else txtFisAltNotu.Text = "BİZİ TERCİH ETTİĞİNİZ İÇİN TEŞEKKÜRLER!";

                    if (ayarlar.ContainsKey("PosComPort"))
                    {
                        string port = ayarlar["PosComPort"];
                        foreach (ComboBoxItem item in cmbComPort.Items)
                        {
                            if (item.Content.ToString() == port)
                            {
                                cmbComPort.SelectedItem = item;
                                break;
                            }
                        }
                    }

                    if (ayarlar.ContainsKey("YaziciAdi") && cmbYazici.Items.Contains(ayarlar["YaziciAdi"]))
                    {
                        cmbYazici.SelectedItem = ayarlar["YaziciAdi"];
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayarlar yüklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dukkanAdi = txtDukkanAdi.Text.Trim();
                string fisAltNotu = txtFisAltNotu.Text.Trim();
                string posPort = (cmbComPort.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "COM3";
                string yazici = cmbYazici.SelectedItem?.ToString() ?? "";

                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    string sql = "INSERT INTO Ayarlar (Anahtar, Deger) VALUES (@Anahtar, @Deger) ON CONFLICT(Anahtar) DO UPDATE SET Deger = @Deger";

                    baglanti.Execute(sql, new { Anahtar = "DukkanAdi", Deger = dukkanAdi });
                    baglanti.Execute(sql, new { Anahtar = "FisAltNotu", Deger = fisAltNotu });
                    baglanti.Execute(sql, new { Anahtar = "PosComPort", Deger = posPort });
                    baglanti.Execute(sql, new { Anahtar = "YaziciAdi", Deger = yazici });
                }

                MessageBox.Show("Sistem ayarları başarıyla kaydedildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayarlar kaydedilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnTestPos_Click(object sender, RoutedEventArgs e)
        {
            string posPort = (cmbComPort.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "COM3";
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

            var testUrunleri = new List<Modeller.SepetUrunu>
            {
                new Modeller.SepetUrunu { UrunAdi = "TEST ÜRÜNÜ (DENEME)", Adet = 1, BirimFiyat = 10.00m }
            };

            bool ok = _yaziciServisi.FisYazdir("TEST-001", DateTime.Now, testUrunleri, 10.00m, "Nakit", "DENEME FİŞİ YAZDIRMA TESTİ");
            if (ok)
            {
                MessageBox.Show($"'{yazici}' yazıcısına deneme fişi gönderildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"'{yazici}' yazıcısına fiş yazdırılırken hata oluştu.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
