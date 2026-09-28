using System.Configuration;
using System.Data;
using System;
using System.Windows;

namespace MarketPOS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var yedekleme = new MarketPOS.Servisler.OtomatikYedeklemeServisi();
                yedekleme.ArkaPlandaYedekle();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Yedekleme başlatılamadı: " + ex.Message);
            }

            var dbYoneticisi = new MarketPOS.Veritabani.VeritabaniYoneticisi();
            dbYoneticisi.VeritabaniniHazirla();
            dbYoneticisi.TestVerileriEkle();
        }
    }
}
