using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dapper;
using MarketPOS.Modeller;
using MarketPOS.Servisler;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public partial class YoneticiOnayWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        public bool Onaylandi { get; private set; } = false;
        public string OnaylayanYonetici { get; private set; }

        public YoneticiOnayWindow()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
        }

        /// <summary>
        /// Oturumdaki kullanıcı yönetici ise doğrudan, kasiyer ise yönetici şifresi sorarak yetki verir.
        /// Satır silme, sepet iptali, iade gibi kritik işlemlerden önce çağrılır.
        /// </summary>
        public static bool YetkiDogrula(Window sahip, string islemAdi)
        {
            if (AktifOturum.YoneticiMi) return true;

            var onay = new YoneticiOnayWindow();
            if (sahip != null && sahip.IsLoaded) onay.Owner = sahip;
            bool sonuc = onay.ShowDialog() == true && onay.Onaylandi;
            if (sonuc) LogServisi.Bilgi($"Yönetici onayı ({onay.OnaylayanYonetici}): {islemAdi}");
            return sonuc;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtSifre.Focus();
        }

        private void TxtSifre_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Onayla();
            }
            else if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        private void Onayla()
        {
            string girilenSifre = txtSifre.Password;
            if (string.IsNullOrEmpty(girilenSifre)) return;

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    var yoneticiler = baglanti.Query<KullaniciDto>(
                        "SELECT * FROM Kullanicilar WHERE Rol = 'Yonetici' AND AktifMi = 1").ToList();

                    var yetkili = yoneticiler.FirstOrDefault(y => SifreServisi.Dogrula(girilenSifre, y.Sifre));
                    if (yetkili != null)
                    {
                        Onaylandi = true;
                        OnaylayanYonetici = yetkili.KullaniciAdi;
                        this.DialogResult = true;
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Hatalı veya yetkisiz şifre!", "Reddedildi", MessageBoxButton.OK, MessageBoxImage.Error);
                        txtSifre.Clear();
                        txtSifre.Focus();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veritabanı hatası: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
