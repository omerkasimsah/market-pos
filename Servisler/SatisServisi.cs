using System;
using System.Linq;
using Dapper;
using MarketPOS.Modeller;
using MarketPOS.Veritabani;

namespace MarketPOS.Servisler
{
    /// <summary>
    /// Satış iptali ve fiş tekrar basımı gibi birden fazla ekrandan çağrılan satış işlemleri.
    /// Tek merkezden yönetilir ki stok ve cari bakiyeler her ekranda aynı kurallarla güncellensin.
    /// </summary>
    public class SatisServisi
    {
        private readonly VeritabaniYoneticisi _db = new VeritabaniYoneticisi();

        /// <summary>
        /// Satışı tamamen iptal eder: stokları geri yükler, veresiye kısmını müşteri borcundan düşer
        /// ve satışı iptal edildi olarak işaretler. Hata durumunda açıklayıcı mesaj fırlatır.
        /// </summary>
        public void SatisIptalEt(int satisId)
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();
                using (var islem = baglanti.BeginTransaction())
                {
                    var satis = baglanti.QueryFirstOrDefault<Satis>("SELECT * FROM Satislar WHERE Id = @Id", new { Id = satisId }, islem);
                    if (satis == null) throw new InvalidOperationException("Satış bulunamadı.");
                    if (satis.IptalEdildi) throw new InvalidOperationException("Bu satış zaten iptal edilmiş.");

                    int iadeSayisi = baglanti.ExecuteScalar<int>("SELECT COUNT(*) FROM Iadeler WHERE SatisId = @Id", new { Id = satisId }, islem);
                    if (iadeSayisi > 0)
                        throw new InvalidOperationException("Bu satışta daha önce kısmi iade yapılmış. Tam iptal yerine kalan ürünler için İade ekranını kullanın.");

                    // 1. Stokları geri yükle
                    var detaylar = baglanti.Query<SatisDetay>("SELECT * FROM SatisDetaylari WHERE SatisId = @Id", new { Id = satisId }, islem).ToList();
                    foreach (var detay in detaylar)
                    {
                        baglanti.Execute("UPDATE Urunler SET StokMiktari = StokMiktari + @Adet, GuncellenmeTarihi = @Tarih WHERE Id = @UrunId",
                            new { detay.Adet, detay.UrunId, Tarih = DateTime.Now }, islem);
                    }

                    // 2. Veresiye kısmını (tam veresiye veya bölünmüş ödemedeki veresiye) müşteri borcundan düş
                    var borcKaydi = baglanti.QueryFirstOrDefault<Islem>(
                        "SELECT * FROM Islemler WHERE SatisId = @Id AND IslemTipi = 'Borçlandırma'", new { Id = satisId }, islem);
                    if (borcKaydi != null && borcKaydi.MusteriId.HasValue)
                    {
                        baglanti.Execute("UPDATE Musteriler SET BorcMiktari = BorcMiktari - @Tutar WHERE Id = @MusteriId",
                            new { borcKaydi.Tutar, MusteriId = borcKaydi.MusteriId.Value }, islem);
                        baglanti.Execute(@"INSERT INTO Islemler (IslemTipi, Aciklama, Tutar, Tarih, MusteriId, SatisId)
                                           VALUES ('Tahsilat', @Aciklama, @Tutar, @Tarih, @MusteriId, @SatisId)",
                            new
                            {
                                Aciklama = $"Satış iptali - veresiye borcu silindi (Satış: {satis.SatisNumarasi ?? satis.FisNo})",
                                borcKaydi.Tutar,
                                Tarih = DateTime.Now,
                                MusteriId = borcKaydi.MusteriId.Value,
                                SatisId = satisId
                            }, islem);
                    }

                    // 3. Satışı iptal olarak işaretle
                    baglanti.Execute("UPDATE Satislar SET IptalEdildi = 1 WHERE Id = @Id", new { Id = satisId }, islem);

                    islem.Commit();
                    LogServisi.Bilgi($"Satış iptal edildi: {satis.SatisNumarasi ?? satis.FisNo} ({satis.ToplamTutar:N2} TL)");
                }
            }
        }

        /// <summary>
        /// Kayıtlı bir satıştan fiş bilgisini oluşturur (tekrar/kopya fiş basımı için).
        /// </summary>
        public FisBilgisi FisBilgisiGetir(int satisId)
        {
            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();
                var satis = baglanti.QueryFirstOrDefault<Satis>("SELECT * FROM Satislar WHERE Id = @Id", new { Id = satisId });
                if (satis == null) return null;

                var detaylar = baglanti.Query<SatisDetay>("SELECT * FROM SatisDetaylari WHERE SatisId = @Id", new { Id = satisId }).ToList();

                return new FisBilgisi
                {
                    FisNo = satis.FisNo,
                    SatisNumarasi = satis.SatisNumarasi,
                    Tarih = satis.Tarih,
                    Urunler = detaylar.Select(d => new SepetUrunu
                    {
                        UrunId = d.UrunId,
                        UrunAdi = d.UrunAdi,
                        Adet = d.Adet,
                        BirimFiyat = d.BirimFiyat,
                        KdvOrani = d.KdvOrani
                    }).ToList(),
                    Iskonto = satis.IskontoTutari,
                    ToplamTutar = satis.ToplamTutar,
                    OdemeTuru = satis.OdemeTuru,
                    NakitTutar = satis.NakitTutar,
                    KartTutar = satis.KartTutar,
                    VeresiyeTutar = satis.VeresiyeTutar,
                    MusteriAdi = satis.MusteriAdi,
                    Kasiyer = satis.KullaniciAdi,
                    Kopya = true
                };
            }
        }
    }
}
