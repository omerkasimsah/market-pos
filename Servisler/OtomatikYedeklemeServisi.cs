using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;

namespace MarketPOS.Servisler
{
    /// <summary>
    /// Veritabanının zaman damgalı kopyasını alan, Urunler ve Musteriler tablolarını CSV olarak dışa aktaran
    /// ve 30 günden eski yedekleri temizleyen güvenlik servisi.
    /// </summary>
    public class OtomatikYedeklemeServisi
    {
        private readonly string _veritabaniYolu = "MarketVeritabani.sqlite";
        private readonly string _yedekKlasoru = "Yedekler";
        private readonly VeritabaniYoneticisi _db;
        private Timer _zamanlayici;

        public OtomatikYedeklemeServisi()
        {
            _db = new VeritabaniYoneticisi();

            if (!Directory.Exists(_yedekKlasoru))
            {
                Directory.CreateDirectory(_yedekKlasoru);
            }
        }

        /// <summary>
        /// Günlük saat 23:59'da arka planda otomatik çalışması için zamanlayıcıyı başlatır.
        /// </summary>
        public void ZamanlayiciyiBaslat()
        {
            DateTime simdi = DateTime.Now;
            DateTime bugunHedef = DateTime.Today.AddHours(23).AddMinutes(59);

            if (simdi > bugunHedef)
            {
                bugunHedef = bugunHedef.AddDays(1);
            }

            TimeSpan ilkBekleme = bugunHedef - simdi;
            TimeSpan gunlukPeriyot = TimeSpan.FromDays(1);

            _zamanlayici = new Timer((e) =>
            {
                YedekAl();
            }, null, ilkBekleme, gunlukPeriyot);

            Debug.WriteLine($"Otomatik Yedek Zamanlayıcısı Kuruldu. İlk Tetiklenme: {bugunHedef}");
        }

        /// <summary>
        /// Tam yedekleme işlemini (SQLite Kopyalama + CSV İhracı + 30 Günlük Temizlik) gerçekleştirir.
        /// </summary>
        public void YedekAl()
        {
            try
            {
                VeritabaniYedekle();
                CsvYedekle();
                EskiYedekleriTemizle();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Yedekleme Genel Hatası: " + ex.Message);
            }
        }

        private void VeritabaniYedekle()
        {
            try
            {
                if (File.Exists(_veritabaniYolu))
                {
                    string tarihDamgasi = DateTime.Now.ToString("yyyy_MM_dd_HHmm");
                    string yedekDosyaAdi = $"Yedek_{tarihDamgasi}.db";
                    string hedefYol = Path.Combine(_yedekKlasoru, yedekDosyaAdi);

                    File.Copy(_veritabaniYolu, hedefYol, true);
                    Debug.WriteLine($"Veritabanı Yedeği Alındı: {hedefYol}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SQLite Yedekleme Hatası: " + ex.Message);
            }
        }

        private void CsvYedekle()
        {
            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();

                    // 1. Ürünleri CSV'ye aktar
                    var urunler = baglanti.Query<Urun>("SELECT * FROM Urunler").ToList();
                    StringBuilder urunCsv = new StringBuilder();
                    urunCsv.AppendLine("Id;Barkod;UrunAdi;SatisFiyati;StokMiktari;OlusturulmaTarihi;GuncellenmeTarihi");

                    foreach (var u in urunler)
                    {
                        urunCsv.AppendLine($"{u.Id};\"{u.Barkod}\";\"{u.UrunAdi}\";{u.SatisFiyati:F2};{u.StokMiktari:F2};{u.OlusturulmaTarihi:yyyy-MM-dd HH:mm:ss};{u.GuncellenmeTarihi:yyyy-MM-dd HH:mm:ss}");
                    }

                    string urunYol = Path.Combine(_yedekKlasoru, "Urunler_Yedek.csv");
                    File.WriteAllText(urunYol, urunCsv.ToString(), Encoding.UTF8);

                    // 2. Müşterileri CSV'ye aktar
                    var musteriler = baglanti.Query<Musteri>("SELECT * FROM Musteriler").ToList();
                    StringBuilder musteriCsv = new StringBuilder();
                    musteriCsv.AppendLine("Id;AdSoyad;Telefon;BorcMiktari;KayitTarihi");

                    foreach (var m in musteriler)
                    {
                        musteriCsv.AppendLine($"{m.Id};\"{m.AdSoyad}\";\"{m.Telefon}\";{m.BorcMiktari:F2};{m.KayitTarihi:yyyy-MM-dd HH:mm:ss}");
                    }

                    string musteriYol = Path.Combine(_yedekKlasoru, "Musteriler_Yedek.csv");
                    File.WriteAllText(musteriYol, musteriCsv.ToString(), Encoding.UTF8);

                    Debug.WriteLine("CSV Yedekleri Başarıyla Oluşturuldu.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CSV Dışa Aktarım Hatası: " + ex.Message);
            }
        }

        private void EskiYedekleriTemizle()
        {
            try
            {
                var dizinBilgisi = new DirectoryInfo(_yedekKlasoru);
                var dosyalar = dizinBilgisi.GetFiles("Yedek_*.db");

                DateTime sinirTarih = DateTime.Now.AddDays(-30);

                foreach (var dosya in dosyalar)
                {
                    if (dosya.CreationTime < sinirTarih)
                    {
                        dosya.Delete();
                        Debug.WriteLine($"30 Günden Eski Yedek Silindi: {dosya.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Eski Yedek Temizleme Hatası: " + ex.Message);
            }
        }

        public void ArkaPlandaYedekle()
        {
            Task.Run(() => YedekAl());
        }
    }
}
