using System;
using System.Threading.Tasks;
using System.Windows;
using MarketPOS.Modeller;
using MarketPOS.Servisler;

namespace MarketPOS.Arayuzler
{
    public partial class PosBeklemeWindow : Window
    {
        private readonly decimal _tutar;
        private readonly string _fisNo;
        private readonly string _portAdi;
        private readonly PosCihazServisi _posServisi;

        public PosIslemSonucu Sonuc { get; private set; }

        public PosBeklemeWindow(decimal tutar, string fisNo, string portAdi = "COM3")
        {
            InitializeComponent();
            _tutar = tutar;
            _fisNo = fisNo;
            _portAdi = portAdi;
            _posServisi = new PosCihazServisi();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtTutar.Text = $"{_tutar:N2} ₺";
            txtDurumMetni.Text = $"⏳ {_portAdi} portuna tutar gönderildi. Lütfen müşterinin kart okutmasını veya şifre girmesini bekleyin...";

            // Arka planda POS cihazı ile seri port haberleşmesi başlat
            var sonuc = await Task.Run(() => _posServisi.SatisGonder(_tutar, _fisNo, _portAdi, 15));

            // Eğer kullanıcı simülasyon butonlarına basmadıysa veya pencere açık ise
            if (Sonuc == null)
            {
                Sonuc = sonuc;
                if (Sonuc.Basarili)
                {
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    pbİlerleme.IsIndeterminate = false;
                    pbİlerleme.Value = 0;
                    txtHataAciklamasi.Text = Sonuc.Mesaj;
                    txtHataAciklamasi.Visibility = Visibility.Visible;
                }
            }
        }

        private void BtnTestOnay_Click(object sender, RoutedEventArgs e)
        {
            Sonuc = new PosIslemSonucu
            {
                Basarili = true,
                Mesaj = "✅ POS İşlemi Başarıyla Onaylandı (Simülasyon)",
                OnayKodu = "ONAY-TEST" + DateTime.Now.ToString("HHmmss")
            };
            this.DialogResult = true;
            this.Close();
        }

        private void BtnTestYetersizBakiye_Click(object sender, RoutedEventArgs e)
        {
            Sonuc = new PosIslemSonucu
            {
                Basarili = false,
                Mesaj = "❌ POS İşlemi Reddedildi: Yetersiz Bakiye!",
                HataKodu = "YETERSIZ_BAKIYE"
            };
            pbİlerleme.IsIndeterminate = false;
            pbİlerleme.Value = 0;
            txtHataAciklamasi.Text = Sonuc.Mesaj;
            txtHataAciklamasi.Visibility = Visibility.Visible;
        }

        private void BtnIptal_Click(object sender, RoutedEventArgs e)
        {
            Sonuc = new PosIslemSonucu
            {
                Basarili = false,
                Mesaj = "❌ POS İşlemi Kullanıcı Tarafından İptal Edildi",
                HataKodu = "KULLANICI_IPTAL"
            };
            this.DialogResult = false;
            this.Close();
        }
    }
}
