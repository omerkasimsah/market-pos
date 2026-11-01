using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MarketPOS.Servisler;

namespace MarketPOS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex _tekOrnekKilidi;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Aynı bilgisayarda programın iki kez açılması veritabanı çakışmalarına yol açar
            _tekOrnekKilidi = new Mutex(true, "MarketPOS_TekOrnek", out bool yeniOrnek);
            if (!yeniOrnek)
            {
                MessageBox.Show("MarketPOS zaten açık. Görev çubuğundaki pencereyi kullanın.", "MarketPOS", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Beklenmeyen hatalarda program kapanmasın, hata loglansın
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (s, ev) => LogServisi.Hata("Yakalanmamış hata", ev.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, ev) => { LogServisi.Hata("Arka plan görev hatası", ev.Exception); ev.SetObserved(); };

            base.OnStartup(e);

            try
            {
                new MarketPOS.Veritabani.VeritabaniYoneticisi().VeritabaniniHazirla();
            }
            catch (Exception ex)
            {
                LogServisi.Hata("Veritabanı hazırlanamadı", ex);
                MessageBox.Show($"Veritabanı açılamadı:\n{ex.Message}\n\nDosya: {MarketPOS.Veritabani.VeritabaniYoneticisi.VeritabaniYolu}", "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            try
            {
                new OtomatikYedeklemeServisi().ArkaPlandaYedekle();
            }
            catch (Exception ex)
            {
                LogServisi.Hata("Açılış yedeği başlatılamadı", ex);
            }

            LogServisi.Bilgi("Program başlatıldı");
            new Arayuzler.GirisEkraniWindow().Show();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogServisi.Hata("Arayüz hatası", e.Exception);
            MessageBox.Show($"Beklenmeyen bir hata oluştu, işlem tamamlanamadı:\n{e.Exception.Message}\n\nAyrıntılar 'Loglar' klasörüne kaydedildi.",
                "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LogServisi.Bilgi("Program kapatıldı");
            try { _tekOrnekKilidi?.ReleaseMutex(); } catch { }
            base.OnExit(e);
        }
    }
}
