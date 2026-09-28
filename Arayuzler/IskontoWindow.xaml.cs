using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MarketPOS.Arayuzler
{
    public partial class IskontoWindow : Window
    {
        private readonly decimal _mevcutToplam;
        public decimal UygulananIndirim { get; private set; }

        public IskontoWindow(decimal mevcutToplam)
        {
            InitializeComponent();
            _mevcutToplam = mevcutToplam;
            txtMevcutToplam.Text = $"Mevcut Toplam: {_mevcutToplam:N2} ₺";
            Hesapla();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtDeger.TextChanged += TxtDeger_TextChanged;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                txtDeger.Focus();
            }));
        }

        private void Radio_Checked(object sender, RoutedEventArgs e)
        {
            if (lblDeger != null)
            {
                if (rbYuzde.IsChecked == true)
                {
                    lblDeger.Text = "İskonto Yüzdesi (%):";
                }
                else
                {
                    lblDeger.Text = "İskonto Tutarı (TL):";
                }
                Hesapla();
                txtDeger?.Focus();
            }
        }

        private void TxtDeger_TextChanged(object sender, TextChangedEventArgs e)
        {
            Hesapla();
        }

        private void Hesapla()
        {
            if (txtIndirimTutari == null) return;

            decimal deger = 0;
            decimal.TryParse(txtDeger.Text, out deger);

            decimal indirimTutari = 0;

            if (rbYuzde.IsChecked == true)
            {
                indirimTutari = _mevcutToplam * (deger / 100m);
            }
            else
            {
                indirimTutari = deger;
            }

            if (indirimTutari > _mevcutToplam)
            {
                indirimTutari = _mevcutToplam; // Toplam tutardan fazla indirim yapılamaz
            }

            UygulananIndirim = indirimTutari;
            txtIndirimTutari.Text = $"İndirim: {indirimTutari:N2} ₺";
            txtYeniToplam.Text = $"Yeni Toplam: {(_mevcutToplam - indirimTutari):N2} ₺";
        }

        private void Uygula_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Iptal_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
