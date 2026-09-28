using System;
using System.Windows;
using System.Windows.Input;
using System.Security.Cryptography;
using System.Text;
using MarketPOS.Veritabani;
using MarketPOS.Modeller;
using Dapper;

namespace MarketPOS.Arayuzler
{
    public class KullaniciDto
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; }
        public string Sifre { get; set; }
        public string AdSoyad { get; set; }
        public string Rol { get; set; }
        public bool AktifMi { get; set; }
    }

    public partial class GirisEkraniWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;

        public GirisEkraniWindow()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
            
            // Veritabanının hazır olduğundan emin ol
            _db.VeritabaniniHazirla();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtKullaniciAdi.Focus();
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void TxtSifre_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                GirisYap();
            }
        }

        private void BtnGiris_Click(object sender, RoutedEventArgs e)
        {
            GirisYap();
        }

        private string SifreHashle(string sifre)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sifre));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        private void GirisYap()
        {
            txtHataMesaji.Visibility = Visibility.Collapsed;
            string kullaniciAdi = txtKullaniciAdi.Text.Trim();
            string sifre = txtSifre.Password;

            if (string.IsNullOrEmpty(kullaniciAdi) || string.IsNullOrEmpty(sifre))
            {
                HataGoster("Lütfen kullanıcı adı ve şifrenizi girin.");
                return;
            }

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    var kullanici = baglanti.QueryFirstOrDefault<KullaniciDto>(
                        "SELECT * FROM Kullanicilar WHERE KullaniciAdi = @KullaniciAdi AND AktifMi = 1",
                        new { KullaniciAdi = kullaniciAdi });

                    if (kullanici == null)
                    {
                        HataGoster("Kullanıcı bulunamadı veya hesabınız pasif durumda.");
                        return;
                    }

                    string girilenSifreHash = SifreHashle(sifre);

                    if (kullanici.Sifre == girilenSifreHash)
                    {
                        // Başarılı giriş
                        AktifOturum.OturumAc(kullanici.Id, kullanici.KullaniciAdi, kullanici.AdSoyad, kullanici.Rol);
                        
                        AnaMenuWindow anaMenu = new AnaMenuWindow();
                        anaMenu.Show();
                        
                        this.Close();
                    }
                    else
                    {
                        HataGoster("Hatalı şifre girdiniz.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Giriş sırasında hata oluştu:\n{ex.Message}", "Veritabanı Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HataGoster(string mesaj)
        {
            txtHataMesaji.Text = mesaj;
            txtHataMesaji.Visibility = Visibility.Visible;
            txtSifre.Clear();
            txtSifre.Focus();
        }
    }
}
