using System;
using System.Data.SQLite;
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
    /// Yedek, SQLite'ın çevrimiçi yedekleme API'si ile alınır; program açıkken bile tutarlı bir kopya oluşur.
    /// </summary>
    public class OtomatikYedeklemeServisi
    {
        private static readonly object _yedekKilidi = new object();
        private readonly VeritabaniYoneticisi _db;
        private Timer _zamanlayici;

        public static string YedekKlasoru => Path.Combine(VeritabaniYoneticisi.VeriKlasoru, "Yedekler");

        public OtomatikYedeklemeServisi()
        {
            _db = new VeritabaniYoneticisi();
            Directory.CreateDirectory(YedekKlasoru);
        }

        /// <summary>
        /// Günlük saat 23:59'da arka planda otomatik çalışması için zamanlayıcıyı başlatır.
        /// </summary>
        public void ZamanlayiciyiBaslat()
        {
            if (_zamanlayici != null) return;

            DateTime simdi = DateTime.Now;
            DateTime bugunHedef = DateTime.Today.AddHours(23).AddMinutes(59);

            if (simdi > bugunHedef)
            {
                bugunHedef = bugunHedef.AddDays(1);
            }

            _zamanlayici = new Timer(e => YedekAl(), null, bugunHedef - simdi, TimeSpan.FromDays(1));
        }

        /// <summary>
        /// Tam yedekleme işlemini (SQLite Kopyalama + CSV İhracı + 30 Günlük Temizlik) gerçekleştirir.
        /// Başarılıysa oluşturulan yedek dosyasının yolunu döndürür.
        /// </summary>
        public string YedekAl()
        {
            lock (_yedekKilidi)
            {
                string yedekYolu = null;
                try
                {
                    yedekYolu = VeritabaniYedekle();
                    CsvYedekle();
                    EskiYedekleriTemizle();
                }
                catch (Exception ex)
                {
                    LogServisi.Hata("Yedekleme genel hatası", ex);
                }
                return yedekYolu;
            }
        }

        private string VeritabaniYedekle()
        {
            if (!File.Exists(VeritabaniYoneticisi.VeritabaniYolu)) return null;

            string hedefYol = Path.Combine(YedekKlasoru, $"Yedek_{DateTime.Now:yyyy_MM_dd_HHmmss}.db");

            using (var kaynak = _db.BaglantiGetir())
            using (var hedef = new SQLiteConnection($"Data Source={hedefYol};Version=3;"))
            {
                kaynak.Open();
                hedef.Open();
                kaynak.BackupDatabase(hedef, "main", "main", -1, null, 0);
            }

            LogServisi.Bilgi($"Veritabanı yedeği alındı: {hedefYol}");
            return hedefYol;
        }

        /// <summary>
        /// Seçilen yedek dosyasını canlı veritabanının üzerine geri yükler.
        /// Geri yüklemeden önce mevcut durumun güvenlik yedeği alınır.
        /// </summary>
        public void GeriYukle(string yedekDosyasi)
        {
            lock (_yedekKilidi)
            {
                if (!File.Exists(yedekDosyasi)) throw new FileNotFoundException("Yedek dosyası bulunamadı.", yedekDosyasi);

                // Dosyanın gerçekten bu programın veritabanı olduğunu doğrula
                using (var kontrol = new SQLiteConnection($"Data Source={yedekDosyasi};Version=3;Read Only=True;"))
                {
                    kontrol.Open();
                    int tablo = kontrol.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Urunler','Satislar')");
                    if (tablo < 2) throw new InvalidDataException("Seçilen dosya geçerli bir MarketPOS yedeği değil.");
                }

                string guvenlikYedegi = Path.Combine(YedekKlasoru, $"GeriYuklemeOncesi_{DateTime.Now:yyyy_MM_dd_HHmmss}.db");
                using (var canli = _db.BaglantiGetir())
                using (var guvenlik = new SQLiteConnection($"Data Source={guvenlikYedegi};Version=3;"))
                {
                    canli.Open();
                    guvenlik.Open();
                    canli.BackupDatabase(guvenlik, "main", "main", -1, null, 0);
                }

                using (var kaynak = new SQLiteConnection($"Data Source={yedekDosyasi};Version=3;Read Only=True;"))
                using (var canli = _db.BaglantiGetir())
                {
                    kaynak.Open();
                    canli.Open();
                    kaynak.BackupDatabase(canli, "main", "main", -1, null, 0);
                }

                VeritabaniYoneticisi.HazirlikDurumunuSifirla();
                _db.VeritabaniniHazirla();
                AyarServisi.OnbellegiTemizle();
                LogServisi.Bilgi($"Yedekten geri yüklendi: {yedekDosyasi} (önceki durum: {guvenlikYedegi})");
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
                    urunCsv.AppendLine("Id;Barkod;UrunAdi;SatisFiyati;AlisFiyati;KdvOrani;StokMiktari;Kategori;OlusturulmaTarihi;GuncellenmeTarihi");

                    foreach (var u in urunler)
                    {
                        urunCsv.AppendLine($"{u.Id};{Csv(u.Barkod)};{Csv(u.UrunAdi)};{u.SatisFiyati:F2};{u.AlisFiyati:F2};{u.KdvOrani:F0};{u.StokMiktari:F3};{Csv(u.Kategori)};{u.OlusturulmaTarihi:yyyy-MM-dd HH:mm:ss};{u.GuncellenmeTarihi:yyyy-MM-dd HH:mm:ss}");
                    }

                    File.WriteAllText(Path.Combine(YedekKlasoru, "Urunler_Yedek.csv"), urunCsv.ToString(), Encoding.UTF8);

                    // 2. Müşterileri CSV'ye aktar
                    var musteriler = baglanti.Query<Musteri>("SELECT * FROM Musteriler").ToList();
                    StringBuilder musteriCsv = new StringBuilder();
                    musteriCsv.AppendLine("Id;AdSoyad;Telefon;BorcMiktari;KayitTarihi");

                    foreach (var m in musteriler)
                    {
                        musteriCsv.AppendLine($"{m.Id};{Csv(m.AdSoyad)};{Csv(m.Telefon)};{m.BorcMiktari:F2};{m.KayitTarihi:yyyy-MM-dd HH:mm:ss}");
                    }

                    File.WriteAllText(Path.Combine(YedekKlasoru, "Musteriler_Yedek.csv"), musteriCsv.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                LogServisi.Hata("CSV dışa aktarım hatası", ex);
            }
        }

        private static string Csv(string deger) => "\"" + (deger ?? "").Replace("\"", "\"\"") + "\"";

        private void EskiYedekleriTemizle()
        {
            try
            {
                DateTime sinirTarih = DateTime.Now.AddDays(-30);
                var dosyalar = new DirectoryInfo(YedekKlasoru).GetFiles("Yedek_*.db")
                    .OrderByDescending(d => d.CreationTime)
                    .ToList();

                // En yeni 10 yedek ne kadar eski olursa olsun her zaman korunur
                foreach (var dosya in dosyalar.Skip(10).Where(d => d.CreationTime < sinirTarih))
                {
                    dosya.Delete();
                }
            }
            catch (Exception ex)
            {
                LogServisi.Hata("Eski yedek temizleme hatası", ex);
            }
        }

        public void ArkaPlandaYedekle()
        {
            Task.Run(() => YedekAl());
        }
    }
}
