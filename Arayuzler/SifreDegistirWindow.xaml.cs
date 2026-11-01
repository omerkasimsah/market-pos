using System;
using System.Windows;
using Dapper;
using MarketPOS.Modeller;
using MarketPOS.Servisler;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    /// <summary>
    /// Oturum açmış kullanıcının kendi şifresini değiştirmesini sağlar.
    /// </summary>
    public partial class SifreDegistirWindow : Window
    {
        private readonly VeritabaniYoneticisi _db = new VeritabaniYoneticisi();

        public SifreDegistirWindow()
        {
            InitializeComponent();
            txtKullanici.Text = $"{AktifOturum.AdSoyad} ({AktifOturum.KullaniciAdi})";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => txtMevcut.Focus();

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (AktifOturum.KullaniciId == 0) { Close(); return; }

            if (txtYeni.Password.Length < 4)
            {
                MessageBox.Show("Yeni şifre en az 4 karakter olmalıdır.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (txtYeni.Password != txtYeniTekrar.Password)
            {
                MessageBox.Show("Yeni şifreler birbiriyle uyuşmuyor.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (txtYeni.Password == "1234")
            {
                MessageBox.Show("Varsayılan şifre (1234) tekrar kullanılamaz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    string kayitli = baglanti.QueryFirstOrDefault<string>("SELECT Sifre FROM Kullanicilar WHERE Id = @Id", new { Id = AktifOturum.KullaniciId });
                    if (!SifreServisi.Dogrula(txtMevcut.Password, kayitli))
                    {
                        MessageBox.Show("Mevcut şifrenizi hatalı girdiniz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        txtMevcut.Clear();
                        txtMevcut.Focus();
                        return;
                    }

                    baglanti.Execute("UPDATE Kullanicilar SET Sifre = @Sifre WHERE Id = @Id",
                        new { Sifre = SifreServisi.Hashle(txtYeni.Password), Id = AktifOturum.KullaniciId });
                }

                LogServisi.Bilgi("Kullanıcı şifresini değiştirdi");
                MessageBox.Show("Şifreniz başarıyla değiştirildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şifre değiştirilemedi:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
