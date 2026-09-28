using System;

namespace MarketPOS.Modeller
{
    /// <summary>
    /// Market ürünlerini temsil eden veri modeli.
    /// </summary>
    public class Urun
    {
        public int Id { get; set; }
        public string Barkod { get; set; }
        public string UrunAdi { get; set; }
        public decimal SatisFiyati { get; set; }
        public decimal AlisFiyati { get; set; }
        public decimal KdvOrani { get; set; } = 10m;
        public bool KdvDahilMi { get; set; } = true;
        public decimal StokMiktari { get; set; }
        public string Kategori { get; set; }
        public string Firma { get; set; }
        public string FotografYolu { get; set; }
        public DateTime OlusturulmaTarihi { get; set; }
        public DateTime GuncellenmeTarihi { get; set; }
        
        public bool StokTukendi => StokMiktari <= 0;

        public decimal NetAlisFiyati => KdvDahilMi ? (AlisFiyati / (1m + (KdvOrani / 100m))) : AlisFiyati;
        public decimal KdvTutari => KdvDahilMi ? (AlisFiyati - NetAlisFiyati) : (AlisFiyati * (KdvOrani / 100m));
        public decimal ToplamAlisMaliyet => KdvDahilMi ? AlisFiyati : (AlisFiyati + KdvTutari);

        public decimal KarMarji
        {
            get
            {
                decimal maliyet = ToplamAlisMaliyet;
                if (maliyet <= 0) return 0;
                return ((SatisFiyati - maliyet) / maliyet) * 100m;
            }
        }
    }
}
