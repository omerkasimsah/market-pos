using System;
using System.Windows;
using System.Windows.Input;
using System.Security.Cryptography;
using System.Text;
using MarketPOS.Veritabani;
using MarketPOS.Modeller;
using MarketPOS.Servisler;
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

        // Art arda hatalı denemelerde girişi kısa süreliğine kilitle
        private int _hataliDeneme;
        private DateTime _kilitBitis = DateTime.MinValue;

        private void GirisYap()
        {
            txtHataMesaji.Visibility = Visibility.Collapsed;
            string kullaniciAdi = txtKullaniciAdi.Text.Trim();
            string sifre = txtSifre.Password;

            if (DateTime.Now < _kilitBitis)
            {
                HataGoster($"Çok fazla hatalı deneme. {(int)(_kilitBitis - DateTime.Now).TotalSeconds + 1} sn sonra tekrar deneyin.");
                return;
            }

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

                    if (SifreServisi.Dogrula(sifre, kullanici.Sifre))
                    {
                        _hataliDeneme = 0;

                        // Eski (tuzsuz SHA256) şifreyi güvenli biçime yükselt
                        if (SifreServisi.YukseltmeGerekli(kullanici.Sifre))
                        {
                            baglanti.Execute("UPDATE Kullanicilar SET Sifre = @Sifre WHERE Id = @Id",
                                new { Sifre = SifreServisi.Hashle(sifre), kullanici.Id });
                        }

                        AktifOturum.OturumAc(kullanici.Id, kullanici.KullaniciAdi, kullanici.AdSoyad, kullanici.Rol);
                        LogServisi.Bilgi($"Giriş yapıldı: {kullanici.KullaniciAdi} ({kullanici.Rol})");

                        AnaMenuWindow anaMenu = new AnaMenuWindow();
                        anaMenu.Show();
                        this.Close();

                        // Fabrika şifresiyle çalışılmasın
                        if (sifre == "1234")
                        {
                            MessageBox.Show("Varsayılan şifre (1234) kullanıyorsunuz.\nGüvenliğiniz için lütfen şimdi yeni bir şifre belirleyin.",
                                "Şifre Değiştirme Gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
                            var pencere = new SifreDegistirWindow { Owner = anaMenu };
                            pencere.ShowDialog();
                        }
                    }
                    else
                    {
                        _hataliDeneme++;
                        LogServisi.Bilgi($"Hatalı giriş denemesi: {kullaniciAdi}");
                        if (_hataliDeneme >= 5)
                        {
                            _kilitBitis = DateTime.Now.AddSeconds(30);
                            _hataliDeneme = 0;
                            HataGoster("5 hatalı deneme yapıldı. Giriş 30 saniye kilitlendi.");
                        }
                        else
                        {
                            HataGoster("Hatalı şifre girdiniz.");
                        }
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
