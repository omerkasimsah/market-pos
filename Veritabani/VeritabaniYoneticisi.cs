using System;
using System.Data.SQLite;
using System.IO;
using Dapper;

namespace MarketPOS.Veritabani
{
    /// <summary>
    /// SQLite veritabanı bağlantısını ve tabloların oluşturulmasını yöneten sınıf.
    /// Düşük RAM kullanımı ve yüksek performans için Dapper kullanılmaktadır.
    /// </summary>
    public class VeritabaniYoneticisi
    {
        private const string VeritabaniDosyasi = "MarketVeritabani.sqlite";
        private readonly string baglantiDizesi = $"Data Source={VeritabaniDosyasi};Version=3;";

        /// <summary>
        /// Veritabanı bağlantısı döndürür (Dapper ile kullanım içindir).
        /// Kullanım sonrası bağlantının kapatıldığından emin olunmalıdır (using bloğu).
        /// </summary>
        public SQLiteConnection BaglantiGetir()
        {
            return new SQLiteConnection(baglantiDizesi);
        }

        /// <summary>
        /// Uygulama ilk açıldığında veritabanının ve tabloların var olup olmadığını kontrol eder, yoksa oluşturur.
        /// </summary>
        public void VeritabaniniHazirla()
        {
            if (!File.Exists(VeritabaniDosyasi))
            {
                SQLiteConnection.CreateFile(VeritabaniDosyasi);
            }

            using (var baglanti = BaglantiGetir())
            {
                baglanti.Open();

                // Ürünler tablosu
                string urunlerTablosu = @"
                    CREATE TABLE IF NOT EXISTS Urunler (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Barkod TEXT UNIQUE NOT NULL,
                        UrunAdi TEXT NOT NULL,
                        SatisFiyati DECIMAL(18,2) NOT NULL,
                        StokMiktari DECIMAL(18,2) NOT NULL,
                        OlusturulmaTarihi DATETIME NOT NULL,
                        GuncellenmeTarihi DATETIME NOT NULL
                    );";
                baglanti.Execute(urunlerTablosu);

                // Satışlar tablosu
                string satislarTablosu = @"
                    CREATE TABLE IF NOT EXISTS Satislar (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FisNo TEXT NOT NULL,
                        ToplamTutar DECIMAL(18,2) NOT NULL,
                        OdemeTuru TEXT NOT NULL,
                        Tarih DATETIME NOT NULL,
                        IptalEdildi INTEGER NOT NULL DEFAULT 0
                    );";
                baglanti.Execute(satislarTablosu);

                // Mevcut veritabanı varsa IptalEdildi kolonunu ekle (Basit Migrasyon)
                try { baglanti.Execute("ALTER TABLE Satislar ADD COLUMN IptalEdildi INTEGER NOT NULL DEFAULT 0;"); } catch { }

                // Satış Detayları tablosu
                string satisDetaylariTablosu = @"
                    CREATE TABLE IF NOT EXISTS SatisDetaylari (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SatisId INTEGER NOT NULL,
                        UrunId INTEGER NOT NULL,
                        UrunAdi TEXT NOT NULL,
                        Adet DECIMAL(18,2) NOT NULL,
                        BirimFiyat DECIMAL(18,2) NOT NULL,
                        ToplamFiyat DECIMAL(18,2) NOT NULL
                    );";
                baglanti.Execute(satisDetaylariTablosu);

                // Müşteriler tablosu
                string musterilerTablosu = @"
                    CREATE TABLE IF NOT EXISTS Musteriler (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        AdSoyad TEXT NOT NULL,
                        Telefon TEXT,
                        BorcMiktari DECIMAL(18,2) NOT NULL DEFAULT 0,
                        KayitTarihi DATETIME NOT NULL
                    );";
                baglanti.Execute(musterilerTablosu);

                string islemlerTablosu = @"
                    CREATE TABLE IF NOT EXISTS Islemler (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        IslemTipi TEXT NOT NULL,
                        Aciklama TEXT,
                        Tutar DECIMAL(18,2) NOT NULL,
                        Tarih DATETIME NOT NULL,
                        MusteriId INTEGER NULL,
                        SatisId INTEGER NULL
                    );";
                baglanti.Execute(islemlerTablosu);

                // Toptancılar tablosu
                string toptancilarTablosu = @"
                    CREATE TABLE IF NOT EXISTS Toptancilar (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FirmaAdi TEXT NOT NULL,
                        Yetkili TEXT,
                        Telefon TEXT,
                        BorcMiktari DECIMAL(18,2) NOT NULL DEFAULT 0,
                        KayitTarihi DATETIME NOT NULL
                    );";
                baglanti.Execute(toptancilarTablosu);

                // Giderler tablosu
                string giderlerTablosu = @"
                    CREATE TABLE IF NOT EXISTS Giderler (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        GiderTuru TEXT NOT NULL,
                        Aciklama TEXT,
                        Tutar DECIMAL(18,2) NOT NULL,
                        Tarih DATETIME NOT NULL
                    );";
                baglanti.Execute(giderlerTablosu);

                // Ayarlar tablosu
                string ayarlarTablosu = @"
                    CREATE TABLE IF NOT EXISTS Ayarlar (
                        Anahtar TEXT PRIMARY KEY,
                        Deger TEXT
                    );";
                baglanti.Execute(ayarlarTablosu);

                // Ürün Partileri (SKT & Lot Takibi) tablosu
                string urunPartileriTablosu = @"
                    CREATE TABLE IF NOT EXISTS UrunPartileri (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UrunId INTEGER NOT NULL,
                        PartiNo TEXT,
                        StokMiktari DECIMAL(18,2) NOT NULL,
                        SktTarihi DATETIME NOT NULL,
                        EklenmeTarihi DATETIME NOT NULL,
                        FOREIGN KEY(UrunId) REFERENCES Urunler(Id)
                    );";
                baglanti.Execute(urunPartileriTablosu);

                // Kolon migrasyonları (v1.x)
                try { baglanti.Execute("ALTER TABLE Islemler ADD COLUMN MusteriId INTEGER NULL;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Islemler ADD COLUMN SatisId INTEGER NULL;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Urunler ADD COLUMN Kategori TEXT;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Urunler ADD COLUMN Firma TEXT;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Urunler ADD COLUMN AlisFiyati DECIMAL(18,2) NOT NULL DEFAULT 0;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Urunler ADD COLUMN KdvOrani REAL DEFAULT 10;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Urunler ADD COLUMN KdvDahilMi INTEGER DEFAULT 1;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Urunler ADD COLUMN FotografYolu TEXT;"); } catch { }

                // ===== v2.0 Migrasyonları =====

                // Satislar tablosu genişletme (Bölünmüş ödeme, Satış numarası, Personel takibi)
                try { baglanti.Execute("ALTER TABLE Satislar ADD COLUMN NakitTutar DECIMAL(18,2) DEFAULT 0;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Satislar ADD COLUMN KartTutar DECIMAL(18,2) DEFAULT 0;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Satislar ADD COLUMN VeresiyeTutar DECIMAL(18,2) DEFAULT 0;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Satislar ADD COLUMN MusteriId INTEGER NULL;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Satislar ADD COLUMN MusteriAdi TEXT;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Satislar ADD COLUMN SatisNumarasi TEXT;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Satislar ADD COLUMN KullaniciId INTEGER;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Satislar ADD COLUMN KullaniciAdi TEXT;"); } catch { }
                try { baglanti.Execute("ALTER TABLE Satislar ADD COLUMN IadeTutari DECIMAL(18,2) DEFAULT 0;"); } catch { }

                // Ürünler tablosu - Fotoğraf yolu
                try { baglanti.Execute("ALTER TABLE Urunler ADD COLUMN FotografYolu TEXT;"); } catch { }

                // Kullanıcılar tablosu (Kasiyer / Yönetici yetkilendirme)
                string kullanicilarTablosu = @"
                    CREATE TABLE IF NOT EXISTS Kullanicilar (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        KullaniciAdi TEXT UNIQUE NOT NULL,
                        Sifre TEXT NOT NULL,
                        AdSoyad TEXT NOT NULL,
                        Rol TEXT NOT NULL DEFAULT 'Kasiyer',
                        AktifMi INTEGER DEFAULT 1,
                        KayitTarihi DATETIME NOT NULL
                    );";
                baglanti.Execute(kullanicilarTablosu);

                // Varsayılan yönetici hesabı oluştur (admin / 1234)
                int adminSayisi = baglanti.ExecuteScalar<int>("SELECT COUNT(*) FROM Kullanicilar WHERE KullaniciAdi = 'admin'");
                if (adminSayisi == 0)
                {
                    string hash = BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes("1234"))).Replace("-", "").ToLower();
                    baglanti.Execute("INSERT INTO Kullanicilar (KullaniciAdi, Sifre, AdSoyad, Rol, AktifMi, KayitTarihi) VALUES ('admin', @Sifre, 'Yönetici', 'Yonetici', 1, @Tarih)",
                        new { Sifre = hash, Tarih = DateTime.Now });
                }

                // Z Raporları tablosu (Gün sonu raporu)
                string zRaporlariTablosu = @"
                    CREATE TABLE IF NOT EXISTS ZRaporlari (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        RaporNo TEXT UNIQUE NOT NULL,
                        Tarih DATETIME NOT NULL,
                        ToplamSatisSayisi INTEGER,
                        ToplamCiro DECIMAL(18,2),
                        NakitToplam DECIMAL(18,2),
                        KartToplam DECIMAL(18,2),
                        VeresiyeToplam DECIMAL(18,2),
                        IadeToplam DECIMAL(18,2),
                        IskontoToplam DECIMAL(18,2),
                        KasaAcilis DECIMAL(18,2),
                        KasaKapanis DECIMAL(18,2),
                        PersonelAdi TEXT
                    );";
                baglanti.Execute(zRaporlariTablosu);

                // İadeler tablosu
                string iadelerTablosu = @"
                    CREATE TABLE IF NOT EXISTS Iadeler (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SatisId INTEGER NOT NULL,
                        UrunId INTEGER NOT NULL,
                        UrunAdi TEXT,
                        IadeAdedi DECIMAL(18,2),
                        IadeTutari DECIMAL(18,2),
                        IadeSebebi TEXT,
                        IadeTarihi DATETIME NOT NULL,
                        IadeTuru TEXT
                    );";
                baglanti.Execute(iadelerTablosu);

                // Toptancı Faturaları tablosu
                string toptanciFaturalariTablosu = @"
                    CREATE TABLE IF NOT EXISTS ToptanciFaturalari (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ToptanciId INTEGER NOT NULL,
                        FaturaNo TEXT,
                        ToplamTutar DECIMAL(18,2),
                        Tarih DATETIME NOT NULL,
                        Aciklama TEXT
                    );";
                baglanti.Execute(toptanciFaturalariTablosu);

                string toptanciFaturaDetaylariTablosu = @"
                    CREATE TABLE IF NOT EXISTS ToptanciFaturaDetaylari (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FaturaId INTEGER NOT NULL,
                        UrunId INTEGER NOT NULL,
                        UrunAdi TEXT,
                        Adet DECIMAL(18,2),
                        BirimFiyat DECIMAL(18,2),
                        ToplamFiyat DECIMAL(18,2)
                    );";
                baglanti.Execute(toptanciFaturaDetaylariTablosu);
            }
        }

        /// <summary>
        /// İlk testler için veritabanında ürün yoksa varsayılan test verilerini ekler.
        /// </summary>
        public void TestVerileriEkle()
        {
            using (var baglanti = BaglantiGetir())
            {
                baglanti.Open();
                int urunSayisi = baglanti.ExecuteScalar<int>("SELECT COUNT(*) FROM Urunler");
                
                if (urunSayisi == 0)
                {
                    string eklemeSorgusu = @"
                        INSERT INTO Urunler (Barkod, UrunAdi, SatisFiyati, StokMiktari, OlusturulmaTarihi, GuncellenmeTarihi) 
                        VALUES (@Barkod, @UrunAdi, @SatisFiyati, @StokMiktari, @OlusturulmaTarihi, @GuncellenmeTarihi)";

                    var ornekUrunler = new[]
                    {
                        new { Barkod = "8690000000001", UrunAdi = "Ekmek 200g", SatisFiyati = 10.00m, StokMiktari = 100m, OlusturulmaTarihi = DateTime.Now, GuncellenmeTarihi = DateTime.Now },
                        new { Barkod = "8690000000002", UrunAdi = "Su 0.5L", SatisFiyati = 5.00m, StokMiktari = 250m, OlusturulmaTarihi = DateTime.Now, GuncellenmeTarihi = DateTime.Now },
                        new { Barkod = "8690000000003", UrunAdi = "Çay 1000g", SatisFiyati = 120.00m, StokMiktari = 50m, OlusturulmaTarihi = DateTime.Now, GuncellenmeTarihi = DateTime.Now },
                        new { Barkod = "8690000000004", UrunAdi = "Şeker 1Kg", SatisFiyati = 35.00m, StokMiktari = 120m, OlusturulmaTarihi = DateTime.Now, GuncellenmeTarihi = DateTime.Now },
                        new { Barkod = "8690000000005", UrunAdi = "Süt 1L", SatisFiyati = 25.00m, StokMiktari = 80m, OlusturulmaTarihi = DateTime.Now, GuncellenmeTarihi = DateTime.Now }
                    };

                    baglanti.Execute(eklemeSorgusu, ornekUrunler);
                }
            }
        }
    }
}
