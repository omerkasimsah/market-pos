using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MarketPOS.Arayuzler
{
    public partial class HesapMakinesiWindow : Window
    {
        private double _ilkSayi = 0;
        private string _islem = "";
        private bool _yeniSayi = true;

        public HesapMakinesiWindow()
        {
            InitializeComponent();
        }

        private void BtnRakam_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null)
            {
                if (_yeniSayi || txtEkran.Text == "0")
                {
                    txtEkran.Text = btn.Content.ToString();
                    _yeniSayi = false;
                }
                else
                {
                    txtEkran.Text += btn.Content.ToString();
                }
            }
        }

        private void BtnVirgul_Click(object sender, RoutedEventArgs e)
        {
            if (_yeniSayi)
            {
                txtEkran.Text = "0,";
                _yeniSayi = false;
            }
            else if (!txtEkran.Text.Contains(","))
            {
                txtEkran.Text += ",";
            }
        }

        private void BtnIslem_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null)
            {
                double.TryParse(txtEkran.Text.Replace(".", ","), out _ilkSayi);
                _islem = btn.Content.ToString();
                _yeniSayi = true;
            }
        }

        private void BtnEsittir_Click(object sender, RoutedEventArgs e)
        {
            Hesapla();
        }

        private void Hesapla()
        {
            if (string.IsNullOrEmpty(_islem)) return;

            double ikinciSayi = 0;
            double.TryParse(txtEkran.Text.Replace(".", ","), out ikinciSayi);
            double sonuc = 0;

            switch (_islem)
            {
                case "+": sonuc = _ilkSayi + ikinciSayi; break;
                case "-": sonuc = _ilkSayi - ikinciSayi; break;
                case "*": sonuc = _ilkSayi * ikinciSayi; break;
                case "/": sonuc = ikinciSayi != 0 ? _ilkSayi / ikinciSayi : 0; break;
                case "%": sonuc = (_ilkSayi * ikinciSayi) / 100.0; break;
            }

            txtEkran.Text = sonuc.ToString("N2");
            _islem = "";
            _yeniSayi = true;
        }

        private void BtnTemizle_Click(object sender, RoutedEventArgs e)
        {
            txtEkran.Text = "0";
            _ilkSayi = 0;
            _islem = "";
            _yeniSayi = true;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }
    }
}
