using System;
using System.Windows;
using System.Windows.Input;
using Dapper;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public partial class YoneticiOnayWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        public bool Onaylandi { get; private set; } = false;

        public YoneticiOnayWindow()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
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

            string hash = BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(girilenSifre))).Replace("-", "").ToLower();

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    int yetkiliSayisi = baglanti.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM Kullanicilar WHERE Sifre = @Sifre AND Rol = 'Yonetici'",
                        new { Sifre = hash });

                    if (yetkiliSayisi > 0)
                    {
                        Onaylandi = true;
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
