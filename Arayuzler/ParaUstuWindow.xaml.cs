using System;
using System.Windows;
using System.Windows.Controls;

namespace MarketPOS.Arayuzler
{
    public partial class ParaUstuWindow : Window
    {
        private readonly decimal _toplamTutar;

        public ParaUstuWindow(decimal toplamTutar)
        {
            InitializeComponent();
            _toplamTutar = toplamTutar;
            txtToplamTutar.Text = $"{_toplamTutar:N2} ₺";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtAlinanPara.Focus();
            txtAlinanPara.SelectAll();
        }

        private void TxtAlinanPara_TextChanged(object sender, TextChangedEventArgs e)
        {
            HesaplaParaUstu();
        }

        private void HesaplaParaUstu()
        {
            decimal alinanPara = 0;
            decimal.TryParse(txtAlinanPara.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out alinanPara);

            decimal paraUstu = alinanPara - _toplamTutar;

            if (paraUstu >= 0)
            {
                txtParaUstu.Text = $"{paraUstu:N2} ₺";
                brdParaUstu.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96)); // Yeşil
            }
            else
            {
                txtParaUstu.Text = $"{paraUstu:N2} ₺ (EKSİK!)";
                brdParaUstu.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)); // Kırmızı
            }
        }

        private void HizliPara_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                txtAlinanPara.Text = btn.Tag.ToString();
            }
        }

        private void BtnTamam_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
