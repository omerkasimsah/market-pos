using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    public class MusteriDto
    {
        public int Id { get; set; }
        public string AdSoyad { get; set; }
        public string Telefon { get; set; }
        public decimal BorcMiktari { get; set; }
        public DateTime KayitTarihi { get; set; }
    }

    public partial class TopluVeresiyeWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        private List<MusteriDto> _tumMusteriler = new List<MusteriDto>();

        public TopluVeresiyeWindow()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ListeyiYukle();
        }

        private void ListeyiYukle()
        {
            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    _tumMusteriler = baglanti.Query<MusteriDto>(
                        "SELECT Id, AdSoyad, Telefon, BorcMiktari, KayitTarihi FROM Musteriler ORDER BY AdSoyad").ToList();
                }

                FiltreleVeGoster();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Müşteri listesi yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FiltreleVeGoster()
        {
            var filtreliListe = _tumMusteriler.AsEnumerable();

            string aramaDegeri = txtArama.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(aramaDegeri))
            {
                filtreliListe = filtreliListe.Where(m => 
                    (m.AdSoyad != null && m.AdSoyad.ToLower().Contains(aramaDegeri)) ||
                    (m.Telefon != null && m.Telefon.Contains(aramaDegeri)));
            }

            if (chkBakanlar.IsChecked == true)
            {
                filtreliListe = filtreliListe.Where(m => m.BorcMiktari > 0);
            }

            var sonucListe = filtreliListe.ToList();
            dgMusteriler.ItemsSource = sonucListe;

            decimal toplamAlacak = sonucListe.Sum(m => m.BorcMiktari);
            txtToplamAlacak.Text = $"{toplamAlacak:N2} ₺";
        }

        private void TxtArama_TextChanged(object sender, TextChangedEventArgs e)
        {
            FiltreleVeGoster();
        }

        private void ChkBakanlar_Click(object sender, RoutedEventArgs e)
        {
            FiltreleVeGoster();
        }

        private void BtnYeniMusteri_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Yeni Müşteri Ekleme ekranı henüz geliştirilme aşamasındadır.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            // Burada yeni bir form açılabilir
        }

        private void BtnTahsilat_Click(object sender, RoutedEventArgs e)
        {
            var seciliMusteri = dgMusteriler.SelectedItem as MusteriDto;
            if (seciliMusteri == null)
            {
                MessageBox.Show("Lütfen tahsilat yapılacak müşteriyi seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (seciliMusteri.BorcMiktari <= 0)
            {
                MessageBox.Show("Bu müşterinin borcu bulunmamaktadır.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Tahsilat işlemi için basit bir simülasyon (gerçek projede ayrı bir popup yapılabilir)
            var sonuc = MessageBox.Show($"{seciliMusteri.AdSoyad} adlı müşteriden tahsilat yapmak istiyor musunuz?", "Tahsilat Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (sonuc == MessageBoxResult.Yes)
            {
                try
                {
                    using (var baglanti = _db.BaglantiGetir())
                    {
                        baglanti.Open();
                        // Örnek: Borcu sıfırla (Gerçekte kısmi ödeme vb. olur)
                        baglanti.Execute("UPDATE Musteriler SET BorcMiktari = 0 WHERE Id = @Id", new { Id = seciliMusteri.Id });
                        MessageBox.Show("Tahsilat başarıyla kaydedildi ve borç sıfırlandı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                        ListeyiYukle();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Tahsilat sırasında hata oluştu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEkstre_Click(object sender, RoutedEventArgs e)
        {
            var seciliMusteri = dgMusteriler.SelectedItem as MusteriDto;
            if (seciliMusteri == null)
            {
                MessageBox.Show("Lütfen ekstresini görmek istediğiniz müşteriyi seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"{seciliMusteri.AdSoyad} isimli müşterinin detaylı ekstresi (Yapım Aşamasında)", "Hesap Ekstresi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnWhatsApp_Click(object sender, RoutedEventArgs e)
        {
            var seciliMusteri = dgMusteriler.SelectedItem as MusteriDto;
            if (seciliMusteri == null)
            {
                MessageBox.Show("Lütfen hatırlatma göndermek istediğiniz müşteriyi seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(seciliMusteri.Telefon) || seciliMusteri.Telefon.Length < 10)
            {
                MessageBox.Show("Seçili müşterinin geçerli bir telefon numarası bulunmamaktadır.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (seciliMusteri.BorcMiktari <= 0)
            {
                MessageBox.Show("Bu müşterinin borcu bulunmamaktadır.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Telefon numarasını sadece rakam kalacak şekilde temizle
                string temizTel = new string(seciliMusteri.Telefon.Where(char.IsDigit).ToArray());
                
                // Başında 90 yoksa ve 10 haneliyse ekle
                if (temizTel.Length == 10) temizTel = "90" + temizTel;
                else if (temizTel.Length == 11 && temizTel.StartsWith("0")) temizTel = "90" + temizTel.Substring(1);

                string mesaj = $"Sayın {seciliMusteri.AdSoyad}, işletmemizde bulunan {seciliMusteri.BorcMiktari:N2} TL tutarındaki açık hesabınız/borcunuz için hatırlatma mesajıdır. En kısa sürede ödeme yapmanızı rica ederiz. İyi günler.";
                string urlEncodedMesaj = Uri.EscapeDataString(mesaj);
                string waUrl = $"https://wa.me/{temizTel}?text={urlEncodedMesaj}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = waUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("WhatsApp açılamadı: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
