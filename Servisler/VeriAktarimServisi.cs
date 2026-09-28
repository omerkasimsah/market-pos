using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb; // Access (MDB) bağlantıları için
using System.IO;
using Dapper;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;

namespace MarketPOS.Servisler
{
    /// <summary>
    /// Eski bakkal yazılımının veritabanından (Access/MDB, SQL veya CSV) verileri okuyup
    /// yeni SQLite veritabanına aktaracak esnek ve hatasız Veri Aktarım Servisi.
    /// Düşük RAM kullanımı için veriler toplu (batch) veya satır satır işlenebilir.
    /// </summary>
    public class VeriAktarimServisi
    {
        private readonly VeritabaniYoneticisi _veritabaniYoneticisi;

        public VeriAktarimServisi(VeritabaniYoneticisi veritabaniYoneticisi)
        {
            _veritabaniYoneticisi = veritabaniYoneticisi;
        }

        /// <summary>
        /// Virgülle veya noktalı virgülle ayrılmış CSV dosyasından ürünleri içe aktarır.
        /// </summary>
        /// <param name="dosyaYolu">CSV dosyasının yolu</param>
        /// <param name="ayrac">Sütun ayracı (örn: ';' veya ',')</param>
        public void CsvdenUrunleriAktar(string dosyaYolu, char ayrac = ';')
        {
            if (!File.Exists(dosyaYolu))
                throw new FileNotFoundException("Belirtilen CSV dosyası bulunamadı.", dosyaYolu);

            try
            {
                using (var yeniVeritabani = _veritabaniYoneticisi.BaglantiGetir())
                {
                    yeniVeritabani.Open();
                    using (var islem = yeniVeritabani.BeginTransaction())
                    {
                        var satirlar = File.ReadAllLines(dosyaYolu);
                        // İlk satır başlık kabul edilirse döngü 1'den başlatılabilir.
                        for (int i = 1; i < satirlar.Length; i++)
                        {
                            var sutunlar = satirlar[i].Split(ayrac);
                            if (sutunlar.Length >= 4)
                            {
                                var yeniUrun = new Urun
                                {
                                    Barkod = sutunlar[0].Trim(),
                                    UrunAdi = sutunlar[1].Trim(),
                                    SatisFiyati = decimal.TryParse(sutunlar[2], out decimal fiyat) ? fiyat : 0,
                                    StokMiktari = decimal.TryParse(sutunlar[3], out decimal stok) ? stok : 0,
                                    OlusturulmaTarihi = DateTime.Now,
                                    GuncellenmeTarihi = DateTime.Now
                                };

                                string sorgu = @"
                                    INSERT INTO Urunler (Barkod, UrunAdi, SatisFiyati, StokMiktari, OlusturulmaTarihi, GuncellenmeTarihi) 
                                    VALUES (@Barkod, @UrunAdi, @SatisFiyati, @StokMiktari, @OlusturulmaTarihi, @GuncellenmeTarihi)";
                                
                                // Hata olmaması için aynı barkod varsa atlayabilir veya güncelleyebiliriz (esneklik).
                                // Burada basit bir INSERT kullanıyoruz. Hata yakalama ile yoksayıyoruz.
                                try
                                {
                                    yeniVeritabani.Execute(sorgu, yeniUrun, islem);
                                }
                                catch (Exception)
                                {
                                    // Aynı barkodlu ürün eklendiğinde UNIQUE kısıtlamasından dönen hatayı yoksay.
                                }
                            }
                        }
                        islem.Commit(); // Tüm verileri tek seferde onayla (Performans için)
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"CSV aktarımı sırasında bir hata oluştu: {ex.Message}");
            }
        }

        /// <summary>
        /// Eski Access (.mdb veya .accdb) veritabanından ürünleri okur ve SQLite'a aktarır.
        /// OLEDB sağlayıcısı gerektirir (Örn: Microsoft.ACE.OLEDB.12.0 veya Microsoft.Jet.OLEDB.4.0)
        /// </summary>
        /// <param name="accessDosyaYolu">Access MDB/ACCDB dosyasının yolu</param>
        /// <param name="eskiTabloAdi">Verilerin çekileceği eski tablo adı</param>
        public void AccessVeritabanindanUrunleriAktar(string accessDosyaYolu, string eskiTabloAdi = "EskiUrunler")
        {
            if (!File.Exists(accessDosyaYolu))
                throw new FileNotFoundException("Belirtilen Access dosyası bulunamadı.", accessDosyaYolu);

            // Bağlantı dizesi, Office sürümüne göre değişebilir.
            string accessBaglantiDizesi = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={accessDosyaYolu};Persist Security Info=False;";

            try
            {
                using (OleDbConnection eskiBaglanti = new OleDbConnection(accessBaglantiDizesi))
                {
                    eskiBaglanti.Open();
                    string sorgu = $"SELECT * FROM {eskiTabloAdi}";
                    
                    using (OleDbCommand komut = new OleDbCommand(sorgu, eskiBaglanti))
                    using (OleDbDataReader okuyucu = komut.ExecuteReader())
                    using (var yeniVeritabani = _veritabaniYoneticisi.BaglantiGetir())
                    {
                        yeniVeritabani.Open();
                        using (var islem = yeniVeritabani.BeginTransaction())
                        {
                            while (okuyucu.Read())
                            {
                                // Sütun adları eski yazılımın veritabanına göre eşleştirilmelidir.
                                // Örnek olarak varsayılan isimler kullanılmıştır.
                                var yeniUrun = new Urun
                                {
                                    Barkod = okuyucu["Barkod"]?.ToString() ?? Guid.NewGuid().ToString(),
                                    UrunAdi = okuyucu["UrunAdi"]?.ToString() ?? "Bilinmeyen Ürün",
                                    SatisFiyati = Convert.ToDecimal(okuyucu["SatisFiyati"] ?? 0),
                                    StokMiktari = Convert.ToDecimal(okuyucu["StokMiktari"] ?? 0),
                                    OlusturulmaTarihi = DateTime.Now,
                                    GuncellenmeTarihi = DateTime.Now
                                };

                                string eklemeSorgusu = @"
                                    INSERT INTO Urunler (Barkod, UrunAdi, SatisFiyati, StokMiktari, OlusturulmaTarihi, GuncellenmeTarihi) 
                                    VALUES (@Barkod, @UrunAdi, @SatisFiyati, @StokMiktari, @OlusturulmaTarihi, @GuncellenmeTarihi)";
                                
                                try
                                {
                                    yeniVeritabani.Execute(eklemeSorgusu, yeniUrun, islem);
                                }
                                catch (Exception)
                                {
                                    // Zaten var olan ürünleri veya hatalı kayıtları atla
                                }
                            }
                            islem.Commit();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Access aktarımı sırasında bir hata oluştu: {ex.Message}");
            }
        }
    }
}
