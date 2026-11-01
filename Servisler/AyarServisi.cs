using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dapper;
using MarketPOS.Veritabani;

namespace MarketPOS.Servisler
{
    /// <summary>
    /// Ayarlar tablosundaki anahtar/değer çiftlerini okuyan ve yazan merkezi servis.
    /// Okumalar bellekte önbelleklenir; kayıt sonrası önbellek tazelenir.
    /// </summary>
    public static class AyarServisi
    {
        // Ayar anahtarları (tek bir yerde tanımlı olsun ki yazım hatası olmasın)
        public const string DukkanAdi = "DukkanAdi";
        public const string FisAltNotu = "FisAltNotu";
        public const string DukkanAdres = "DukkanAdres";
        public const string DukkanTelefon = "DukkanTelefon";
        public const string VergiBilgisi = "VergiBilgisi";
        public const string PosComPort = "PosComPort";
        public const string YaziciAdi = "YaziciAdi";
        public const string FisGenisligi = "FisGenisligi";           // 32 (58mm) veya 48 (80mm)
        public const string FisYazdirmaModu = "FisYazdirmaModu";     // Otomatik / Sor / Kapali
        public const string CekmeceAc = "CekmeceAc";                 // 1 / 0
        public const string EksiStokSatis = "EksiStokSatis";         // 1 / 0
        public const string TeraziOnekleri = "TeraziOnekleri";       // Örn: 27,28,29
        public const string TeraziBarkodTipi = "TeraziBarkodTipi";   // Agirlik / Fiyat
        public const string KritikStokSeviyesi = "KritikStokSeviyesi";

        private static readonly Dictionary<string, string> Varsayilanlar = new Dictionary<string, string>
        {
            { DukkanAdi, "SÜPERMARKET POS" },
            { FisAltNotu, "BİZİ TERCİH ETTİĞİNİZ İÇİN TEŞEKKÜRLER!" },
            { DukkanAdres, "" },
            { DukkanTelefon, "" },
            { VergiBilgisi, "" },
            { PosComPort, "COM3" },
            { YaziciAdi, "" },
            { FisGenisligi, "32" },
            { FisYazdirmaModu, "Otomatik" },
            { CekmeceAc, "1" },
            { EksiStokSatis, "1" },
            { TeraziOnekleri, "27,28,29" },
            { TeraziBarkodTipi, "Agirlik" },
            { KritikStokSeviyesi, "5" }
        };

        private static Dictionary<string, string> _onbellek;
        private static readonly object _kilit = new object();

        public static string Oku(string anahtar)
        {
            lock (_kilit)
            {
                if (_onbellek == null) Yenile();
                if (_onbellek.TryGetValue(anahtar, out string deger) && deger != null) return deger;
                return Varsayilanlar.TryGetValue(anahtar, out string varsayilan) ? varsayilan : "";
            }
        }

        public static bool OkuBool(string anahtar) => Oku(anahtar) == "1";

        public static int OkuInt(string anahtar, int varsayilan)
        {
            return int.TryParse(Oku(anahtar), out int sonuc) ? sonuc : varsayilan;
        }

        public static decimal OkuDecimal(string anahtar, decimal varsayilan)
        {
            string deger = (Oku(anahtar) ?? "").Replace(',', '.');
            return decimal.TryParse(deger, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal sonuc) ? sonuc : varsayilan;
        }

        public static void Kaydet(IDictionary<string, string> ayarlar)
        {
            var db = new VeritabaniYoneticisi();
            using (var baglanti = db.BaglantiGetir())
            {
                baglanti.Open();
                using (var islem = baglanti.BeginTransaction())
                {
                    const string sql = "INSERT INTO Ayarlar (Anahtar, Deger) VALUES (@Anahtar, @Deger) ON CONFLICT(Anahtar) DO UPDATE SET Deger = @Deger";
                    foreach (var ayar in ayarlar)
                    {
                        baglanti.Execute(sql, new { Anahtar = ayar.Key, Deger = ayar.Value ?? "" }, islem);
                    }
                    islem.Commit();
                }
            }

            lock (_kilit) { _onbellek = null; }
        }

        public static void Kaydet(string anahtar, string deger)
        {
            Kaydet(new Dictionary<string, string> { { anahtar, deger } });
        }

        /// <summary>Veritabanı dışarıdan değiştiğinde (örn. yedekten geri yükleme) ayarların yeniden okunmasını sağlar.</summary>
        public static void OnbellegiTemizle()
        {
            lock (_kilit) { _onbellek = null; }
        }

        private static void Yenile()
        {
            try
            {
                var db = new VeritabaniYoneticisi();
                using (var baglanti = db.BaglantiGetir())
                {
                    baglanti.Open();
                    _onbellek = baglanti.Query<Arayuzler.AyarModeli>("SELECT Anahtar, Deger FROM Ayarlar")
                        .GroupBy(x => x.Anahtar)
                        .ToDictionary(g => g.Key, g => g.First().Deger);
                }
            }
            catch (Exception ex)
            {
                LogServisi.Hata("Ayarlar okunamadı", ex);
                _onbellek = new Dictionary<string, string>();
            }
        }
    }
}
