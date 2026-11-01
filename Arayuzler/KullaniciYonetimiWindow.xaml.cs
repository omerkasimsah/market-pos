using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using MarketPOS.Servisler;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public class KullaniciSatiri
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; }
        public string AdSoyad { get; set; }
        public string Rol { get; set; }
        public bool AktifMi { get; set; }
        public string RolGorunum => Rol == "Yonetici" ? "Yönetici" : "Kasiyer";
        public string DurumGorunum => AktifMi ? "Aktif" : "Pasif";
    }

    /// <summary>
    /// Kasiyer ve yönetici hesaplarının eklenmesi, düzenlenmesi, şifre sıfırlanması ve pasife alınması.
    /// Kullanıcılar silinmez; geçmiş satışlarda adları korunur.
    /// </summary>
    public partial class KullaniciYonetimiWindow : Window
    {
        private readonly VeritabaniYoneticisi _db = new VeritabaniYoneticisi();
        private KullaniciSatiri _secili;

        public KullaniciYonetimiWindow()
        {
            InitializeComponent();
            Listele();
        }

        private void Listele()
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();
                dgKullanicilar.ItemsSource = baglanti.Query<KullaniciSatiri>(
                    "SELECT Id, KullaniciAdi, AdSoyad, Rol, AktifMi FROM Kullanicilar ORDER BY AktifMi DESC, KullaniciAdi").ToList();
            }
        }

        private void DgKullanicilar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _secili = dgKullanicilar.SelectedItem as KullaniciSatiri;
            if (_secili == null) return;

            txtFormBaslik.Text = "✏️ Kullanıcı Düzenle";
            lblSifre.Text = "Yeni Şifre (boş bırakılırsa değişmez):";
            txtKullaniciAdi.Text = _secili.KullaniciAdi;
            txtAdSoyad.Text = _secili.AdSoyad;
            txtSifre.Clear();
            cmbRol.SelectedIndex = _secili.Rol == "Yonetici" ? 1 : 0;
            chkAktif.IsChecked = _secili.AktifMi;
        }

        private void BtnYeni_Click(object sender, RoutedEventArgs e)
        {
            _secili = null;
            dgKullanicilar.SelectedItem = null;
            txtFormBaslik.Text = "➕ Yeni Kullanıcı";
            lblSifre.Text = "Şifre:";
            txtKullaniciAdi.Clear();
            txtAdSoyad.Clear();
            txtSifre.Clear();
            cmbRol.SelectedIndex = 0;
            chkAktif.IsChecked = true;
            txtKullaniciAdi.Focus();
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            string kullaniciAdi = txtKullaniciAdi.Text.Trim();
            string adSoyad = txtAdSoyad.Text.Trim();
            string sifre = txtSifre.Password;
            string rol = (cmbRol.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Kasiyer";
            bool aktif = chkAktif.IsChecked == true;

            if (string.IsNullOrEmpty(kullaniciAdi) || string.IsNullOrEmpty(adSoyad))
            {
                MessageBox.Show("Kullanıcı adı ve ad soyad zorunludur.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if ((_secili == null || sifre.Length > 0) && sifre.Length < 4)
            {
                MessageBox.Show("Şifre en az 4 karakter olmalıdır.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();

                    // Sistemde en az bir aktif yönetici kalmalı (aksi halde kimse ayarlara giremez)
                    if (_secili != null && _secili.Rol == "Yonetici" && (rol != "Yonetici" || !aktif))
                    {
                        int digerYoneticiler = baglanti.ExecuteScalar<int>(
                            "SELECT COUNT(*) FROM Kullanicilar WHERE Rol = 'Yonetici' AND AktifMi = 1 AND Id <> @Id", new { _secili.Id });
                        if (digerYoneticiler == 0)
                        {
                            MessageBox.Show("Sistemde en az bir aktif yönetici bulunmalıdır.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }

                    int ayniAd = baglanti.ExecuteScalar<int>("SELECT COUNT(*) FROM Kullanicilar WHERE KullaniciAdi = @KullaniciAdi AND Id <> @Id",
                        new { KullaniciAdi = kullaniciAdi, Id = _secili?.Id ?? 0 });
                    if (ayniAd > 0)
                    {
                        MessageBox.Show("Bu kullanıcı adı zaten kullanılıyor.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (_secili == null)
                    {
                        baglanti.Execute(@"INSERT INTO Kullanicilar (KullaniciAdi, Sifre, AdSoyad, Rol, AktifMi, KayitTarihi)
                                           VALUES (@KullaniciAdi, @Sifre, @AdSoyad, @Rol, @AktifMi, @Tarih)",
                            new { KullaniciAdi = kullaniciAdi, Sifre = SifreServisi.Hashle(sifre), AdSoyad = adSoyad, Rol = rol, AktifMi = aktif ? 1 : 0, Tarih = DateTime.Now });
                        LogServisi.Bilgi($"Kullanıcı eklendi: {kullaniciAdi} ({rol})");
                    }
                    else
                    {
                        baglanti.Execute("UPDATE Kullanicilar SET KullaniciAdi = @KullaniciAdi, AdSoyad = @AdSoyad, Rol = @Rol, AktifMi = @AktifMi WHERE Id = @Id",
                            new { KullaniciAdi = kullaniciAdi, AdSoyad = adSoyad, Rol = rol, AktifMi = aktif ? 1 : 0, _secili.Id });
                        if (sifre.Length > 0)
                        {
                            baglanti.Execute("UPDATE Kullanicilar SET Sifre = @Sifre WHERE Id = @Id", new { Sifre = SifreServisi.Hashle(sifre), _secili.Id });
                        }
                        LogServisi.Bilgi($"Kullanıcı güncellendi: {kullaniciAdi} ({rol}, {(aktif ? "aktif" : "pasif")})");
                    }
                }

                MessageBox.Show("Kullanıcı kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                Listele();
                BtnYeni_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kullanıcı kaydedilemedi:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
