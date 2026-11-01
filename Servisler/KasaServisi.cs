using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dapper;
using MarketPOS.Veritabani;

namespace MarketPOS.Servisler
{
    public class KdvSatiri
    {
        public decimal Oran { get; set; }
        public decimal Toplam { get; set; }   // KDV dahil tutar
        public decimal Matrah => Toplam / (1m + Oran / 100m);
        public decimal Kdv => Toplam - Matrah;
    }

    /// <summary>
    /// Bir günün kasa hareketlerinin özeti (Kasa Özeti ekranı ve Z Raporu ortak kullanır).
    /// </summary>
    public class GunOzeti
    {
        public DateTime Gun { get; set; }
        public int SatisAdedi { get; set; }
        public int IptalAdedi { get; set; }
        public decimal BrutCiro { get; set; }
        public decimal Nakit { get; set; }
        public decimal Kart { get; set; }
        public decimal Veresiye { get; set; }
        public decimal Iskonto { get; set; }
        public decimal NakitIade { get; set; }
        public decimal DigerIade { get; set; }
        public decimal IadeToplam => NakitIade + DigerIade;
        public decimal NakitTahsilat { get; set; }
        public decimal Gider { get; set; }
        public decimal Maliyet { get; set; }
        public List<KdvSatiri> KdvDagilimi { get; set; } = new List<KdvSatiri>();

        public decimal NetCiro => BrutCiro - IadeToplam;
        public decimal BrutKar => NetCiro - Maliyet;
        public decimal NetKar => BrutKar - Gider;
        public decimal OrtalamaSepet => SatisAdedi > 0 ? BrutCiro / SatisAdedi : 0;

        /// <summary>Kasada olması gereken nakit (açılış + nakit satış + tahsilat - nakit iade - gider).</summary>
        public decimal BeklenenNakit(decimal kasaAcilis) => kasaAcilis + Nakit + NakitTahsilat - NakitIade - Gider;
    }

    public class KasaServisi
    {
        private readonly VeritabaniYoneticisi _db = new VeritabaniYoneticisi();

        private class SatisOzetSatiri
        {
            public int Id { get; set; }
            public decimal ToplamTutar { get; set; }
            public string OdemeTuru { get; set; }
            public decimal NakitTutar { get; set; }
            public decimal KartTutar { get; set; }
            public decimal VeresiyeTutar { get; set; }
            public decimal IskontoTutari { get; set; }
            public bool IptalEdildi { get; set; }
        }

        private class DetayOzetSatiri
        {
            public int SatisId { get; set; }
            public decimal ToplamFiyat { get; set; }
            public decimal Adet { get; set; }
            public decimal BirimMaliyet { get; set; }
            public decimal KdvOrani { get; set; }
        }

        public GunOzeti Hesapla(DateTime gun)
        {
            var bas = gun.Date;
            var bit = bas.AddDays(1);
            var ozet = new GunOzeti { Gun = bas };

            using (var baglanti = _db.BaglantiGetir())
            {
                baglanti.Open();
                var p = new { Bas = bas, Bit = bit };

                var satislar = baglanti.Query<SatisOzetSatiri>(@"
                    SELECT Id, ToplamTutar, OdemeTuru, COALESCE(NakitTutar,0) NakitTutar, COALESCE(KartTutar,0) KartTutar,
                           COALESCE(VeresiyeTutar,0) VeresiyeTutar, COALESCE(IskontoTutari,0) IskontoTutari, IptalEdildi
                    FROM Satislar WHERE Tarih >= @Bas AND Tarih < @Bit", p).ToList();

                var gecerli = satislar.Where(s => !s.IptalEdildi).ToList();
                ozet.SatisAdedi = gecerli.Count;
                ozet.IptalAdedi = satislar.Count - gecerli.Count;
                ozet.BrutCiro = gecerli.Sum(s => s.ToplamTutar);
                ozet.Iskonto = gecerli.Sum(s => s.IskontoTutari);

                foreach (var s in gecerli)
                {
                    if (s.NakitTutar + s.KartTutar + s.VeresiyeTutar > 0)
                    {
                        ozet.Nakit += s.NakitTutar;
                        ozet.Kart += s.KartTutar;
                        ozet.Veresiye += s.VeresiyeTutar;
                    }
                    else if (s.OdemeTuru == "Kredi Kartı") ozet.Kart += s.ToplamTutar;
                    else if (s.OdemeTuru == "Veresiye") ozet.Veresiye += s.ToplamTutar;
                    else ozet.Nakit += s.ToplamTutar; // Eski kayıtlar: ödeme dağılımı yoksa nakit kabul edilir
                }

                ozet.NakitIade = baglanti.ExecuteScalar<decimal?>(
                    "SELECT SUM(IadeTutari) FROM Iadeler WHERE IadeTarihi >= @Bas AND IadeTarihi < @Bit AND COALESCE(IadeTuru,'Nakit İade') = 'Nakit İade'", p) ?? 0;
                ozet.DigerIade = baglanti.ExecuteScalar<decimal?>(
                    "SELECT SUM(IadeTutari) FROM Iadeler WHERE IadeTarihi >= @Bas AND IadeTarihi < @Bit AND COALESCE(IadeTuru,'Nakit İade') <> 'Nakit İade'", p) ?? 0;

                // Satıştan bağımsız, müşteriden elden alınan veresiye tahsilatları (iptal kaynaklı kayıtlar hariç)
                ozet.NakitTahsilat = baglanti.ExecuteScalar<decimal?>(
                    "SELECT SUM(Tutar) FROM Islemler WHERE IslemTipi = 'Tahsilat' AND SatisId IS NULL AND Tarih >= @Bas AND Tarih < @Bit", p) ?? 0;

                ozet.Gider = baglanti.ExecuteScalar<decimal?>("SELECT SUM(Tutar) FROM Giderler WHERE Tarih >= @Bas AND Tarih < @Bit", p) ?? 0;

                // Satır bazında maliyet ve KDV (eski kayıtlarda satır bilgisi yoksa ürün kartındaki değer kullanılır)
                var detaylar = baglanti.Query<DetayOzetSatiri>(@"
                    SELECT d.SatisId, d.ToplamFiyat, d.Adet,
                           CASE WHEN COALESCE(d.AlisFiyati,0) > 0 THEN d.AlisFiyati ELSE COALESCE(u.AlisFiyati,0) END AS BirimMaliyet,
                           CASE WHEN COALESCE(d.KdvOrani,0) > 0 THEN d.KdvOrani ELSE COALESCE(u.KdvOrani,0) END AS KdvOrani
                    FROM SatisDetaylari d
                    INNER JOIN Satislar s ON s.Id = d.SatisId
                    LEFT JOIN Urunler u ON u.Id = d.UrunId
                    WHERE s.Tarih >= @Bas AND s.Tarih < @Bit AND s.IptalEdildi = 0", p).ToList();

                var satisToplamlari = gecerli.ToDictionary(s => s.Id, s => s.ToplamTutar);
                var araToplamlar = detaylar.GroupBy(d => d.SatisId).ToDictionary(g => g.Key, g => g.Sum(x => x.ToplamFiyat));

                var kdvToplam = new Dictionary<decimal, decimal>();
                foreach (var d in detaylar)
                {
                    ozet.Maliyet += d.Adet * d.BirimMaliyet;

                    // Fiş altı iskonto satırlara oransal dağıtılır
                    decimal ara = araToplamlar[d.SatisId];
                    decimal oran = ara > 0 && satisToplamlari.TryGetValue(d.SatisId, out decimal net) ? net / ara : 1m;
                    kdvToplam.TryGetValue(d.KdvOrani, out decimal mevcut);
                    kdvToplam[d.KdvOrani] = mevcut + d.ToplamFiyat * oran;
                }
                ozet.KdvDagilimi = kdvToplam.OrderBy(k => k.Key).Select(k => new KdvSatiri { Oran = k.Key, Toplam = k.Value }).ToList();

                // İade edilen ürünlerin maliyeti satış maliyetinden düşülür
                ozet.Maliyet -= baglanti.ExecuteScalar<decimal?>(@"
                    SELECT SUM(i.IadeAdedi * COALESCE(u.AlisFiyati,0)) FROM Iadeler i LEFT JOIN Urunler u ON u.Id = i.UrunId
                    WHERE i.IadeTarihi >= @Bas AND i.IadeTarihi < @Bit", p) ?? 0;
            }

            return ozet;
        }

        /// <summary>Z raporu fişi için düz metin döküm oluşturur.</summary>
        public string ZRaporuMetni(GunOzeti o, string raporNo, decimal kasaAcilis, decimal? sayilanNakit)
        {
            string cizgi = new string('-', YaziciServisi.FisGenisligiKarakter);
            var sb = new StringBuilder();
            sb.AppendLine(YaziciServisi.OrtalaSatir(AyarServisi.Oku(AyarServisi.DukkanAdi)));
            sb.AppendLine(YaziciServisi.OrtalaSatir("Z RAPORU / GÜN SONU"));
            sb.AppendLine(YaziciServisi.IkiSutun("Rapor No: " + raporNo, o.Gun.ToString("dd.MM.yyyy")));
            sb.AppendLine(YaziciServisi.IkiSutun("Basım:", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));
            sb.AppendLine("Personel: " + (Modeller.AktifOturum.AdSoyad ?? "-"));
            sb.AppendLine(cizgi);
            sb.AppendLine(YaziciServisi.IkiSutun("Satış Adedi", o.SatisAdedi.ToString()));
            sb.AppendLine(YaziciServisi.IkiSutun("İptal Adedi", o.IptalAdedi.ToString()));
            sb.AppendLine(YaziciServisi.IkiSutun("Brüt Ciro", $"{o.BrutCiro:N2}"));
            sb.AppendLine(YaziciServisi.IkiSutun("İskonto", $"{o.Iskonto:N2}"));
            sb.AppendLine(YaziciServisi.IkiSutun("İade", $"{o.IadeToplam:N2}"));
            sb.AppendLine(YaziciServisi.IkiSutun("NET CİRO", $"{o.NetCiro:N2}"));
            sb.AppendLine(cizgi);
            sb.AppendLine(YaziciServisi.IkiSutun("Nakit", $"{o.Nakit:N2}"));
            sb.AppendLine(YaziciServisi.IkiSutun("Kredi Kartı", $"{o.Kart:N2}"));
            sb.AppendLine(YaziciServisi.IkiSutun("Veresiye", $"{o.Veresiye:N2}"));
            sb.AppendLine(cizgi);
            foreach (var k in o.KdvDagilimi)
            {
                sb.AppendLine(YaziciServisi.IkiSutun($"KDV %{k.Oran:0} Matrah", $"{k.Matrah:N2}"));
                sb.AppendLine(YaziciServisi.IkiSutun($"KDV %{k.Oran:0} Tutar", $"{k.Kdv:N2}"));
            }
            sb.AppendLine(cizgi);
            sb.AppendLine(YaziciServisi.IkiSutun("Kasa Açılış", $"{kasaAcilis:N2}"));
            sb.AppendLine(YaziciServisi.IkiSutun("+ Nakit Satış", $"{o.Nakit:N2}"));
            sb.AppendLine(YaziciServisi.IkiSutun("+ Tahsilat", $"{o.NakitTahsilat:N2}"));
            sb.AppendLine(YaziciServisi.IkiSutun("- Nakit İade", $"{o.NakitIade:N2}"));
            sb.AppendLine(YaziciServisi.IkiSutun("- Gider", $"{o.Gider:N2}"));
            sb.AppendLine(YaziciServisi.IkiSutun("BEKLENEN NAKİT", $"{o.BeklenenNakit(kasaAcilis):N2}"));
            if (sayilanNakit.HasValue)
            {
                sb.AppendLine(YaziciServisi.IkiSutun("SAYILAN NAKİT", $"{sayilanNakit.Value:N2}"));
                sb.AppendLine(YaziciServisi.IkiSutun("KASA FARKI", $"{sayilanNakit.Value - o.BeklenenNakit(kasaAcilis):N2}"));
            }
            sb.AppendLine(cizgi);
            sb.AppendLine(YaziciServisi.OrtalaSatir("MALİ DEĞERİ YOKTUR"));
            return sb.ToString();
        }
    }
}
