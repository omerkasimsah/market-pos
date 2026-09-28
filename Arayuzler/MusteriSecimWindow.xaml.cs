using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;
using Dapper;

namespace MarketPOS.Arayuzler
{
    public partial class MusteriSecimWindow : Window
    {
        public Musteri SecilenMusteri { get; private set; }
        private readonly VeritabaniYoneticisi _db;
        private List<Musteri> _tumMusteriler;

        public MusteriSecimWindow(VeritabaniYoneticisi db)
        {
            InitializeComponent();
            _db = db;
            _tumMusteriler = new List<Musteri>();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                txtArama.Focus();
            }));

            MusterileriYukle();
        }

        private void MusterileriYukle()
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();
                _tumMusteriler = baglanti.Query<Musteri>("SELECT * FROM Musteriler ORDER BY AdSoyad").ToList();
            }
            AramayiUygula("");
        }

        private void txtArama_TextChanged(object sender, TextChangedEventArgs e)
        {
            AramayiUygula(txtArama.Text);
        }

        private void AramayiUygula(string arama)
        {
            if (string.IsNullOrWhiteSpace(arama))
            {
                dgMusteriler.ItemsSource = _tumMusteriler;
            }
            else
            {
                arama = arama.ToLower();
                dgMusteriler.ItemsSource = _tumMusteriler.Where(m => 
                    (m.AdSoyad != null && m.AdSoyad.ToLower().Contains(arama)) || 
                    (m.Telefon != null && m.Telefon.Contains(arama))
                ).ToList();
            }
        }

        private void Sec_Click(object sender, RoutedEventArgs e)
        {
            SecimiTamamla();
        }

        private void dgMusteriler_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SecimiTamamla();
        }

        private void SecimiTamamla()
        {
            if (dgMusteriler.SelectedItem is Musteri musteri)
            {
                SecilenMusteri = musteri;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Lütfen bir müşteri seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Iptal_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
