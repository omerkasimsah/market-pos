using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MarketPOS.Servisler
{
    /// <summary>
    /// Hataları ve önemli olayları uygulama klasöründeki "Loglar" dizinine günlük dosyalar halinde yazar.
    /// Sahada oluşan hataların sonradan incelenebilmesi için gereklidir.
    /// </summary>
    public static class LogServisi
    {
        private static readonly object _kilit = new object();

        public static string LogKlasoru => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Loglar");

        public static void Bilgi(string mesaj) => Yaz("BİLGİ", mesaj);

        public static void Hata(string mesaj, Exception ex = null)
        {
            Yaz("HATA", ex == null ? mesaj : $"{mesaj}\n{ex}");
        }

        private static void Yaz(string seviye, string mesaj)
        {
            try
            {
                Debug.WriteLine($"[{seviye}] {mesaj}");
                lock (_kilit)
                {
                    Directory.CreateDirectory(LogKlasoru);
                    string dosya = Path.Combine(LogKlasoru, $"log_{DateTime.Now:yyyyMMdd}.txt");
                    string kullanici = Modeller.AktifOturum.KullaniciAdi ?? "-";
                    File.AppendAllText(dosya, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{seviye}] ({kullanici}) {mesaj}{Environment.NewLine}", Encoding.UTF8);
                }
            }
            catch
            {
                // Log yazılamıyorsa uygulamayı durdurmamak için sessizce geç
            }
        }
    }
}
