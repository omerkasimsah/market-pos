using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dapper;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;

namespace MarketPOS.Arayuzler
{
    /// <summary>
    /// Barkodu okunamayan veya barkodsuz ürünlerin (manav, şarküteri vb.) ad/kategori ile bulunup sepete eklenmesi.
    /// </summary>
    public partial class UrunAraWindow : Window
    {
        private readonly VeritabaniYoneticisi _db = new VeritabaniYoneticisi();
        public Urun SecilenUrun { get; private set; }

        public UrunAraWindow(string baslangicAramasi = null)
        {
            InitializeComponent();
            txtArama.Text = baslangicAramasi ?? "";
            Ara();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtArama.Focus();
            txtArama.CaretIndex = txtArama.Text.Length;
        }

        private void TxtArama_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => Ara();

        private static readonly System.Globalization.CultureInfo Tr = new System.Globalization.CultureInfo("tr-TR");
        private System.Collections.Generic.List<(Urun Urun, string Anahtar)> _urunler;

        private void Ara()
        {
            if (dgUrunler == null) return;

            // Ürün listesi pencere açılışında bir kez okunur; SQLite LIKE Türkçe İ/ı harflerini doğru eşleştiremediği için
            // arama bellekte Türkçe kurallarla yapılır
            if (_urunler == null)
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    _urunler = baglanti.Query<Urun>("SELECT * FROM Urunler ORDER BY UrunAdi")
                        .Select(u => (u, $"{u.UrunAdi} {u.Barkod} {u.Kategori}".ToLower(Tr)))
                        .ToList();
                }
            }

            // "süt 1l" gibi çok kelimeli aramada tüm kelimeler geçmeli
            string[] kelimeler = txtArama.Text.Trim().ToLower(Tr).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            var sonuc = _urunler.Where(x => kelimeler.All(k => x.Anahtar.Contains(k))).Select(x => x.Urun).Take(200).ToList();

            dgUrunler.ItemsSource = sonuc;
            if (sonuc.Count > 0) dgUrunler.SelectedIndex = 0;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && dgUrunler.Items.Count > 0)
            {
                dgUrunler.SelectedIndex = System.Math.Min(dgUrunler.SelectedIndex + 1, dgUrunler.Items.Count - 1);
                dgUrunler.ScrollIntoView(dgUrunler.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Up && dgUrunler.Items.Count > 0)
            {
                dgUrunler.SelectedIndex = System.Math.Max(dgUrunler.SelectedIndex - 1, 0);
                dgUrunler.ScrollIntoView(dgUrunler.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                BtnSec_Click(null, null);
                e.Handled = true;
            }
        }

        private void DgUrunler_MouseDoubleClick(object sender, MouseButtonEventArgs e) => BtnSec_Click(null, null);

        private void BtnSec_Click(object sender, RoutedEventArgs e)
        {
            if (dgUrunler.SelectedItem is Urun urun)
            {
                SecilenUrun = urun;
                DialogResult = true;
            }
        }
    }
}
