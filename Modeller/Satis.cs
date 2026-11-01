using System;

namespace MarketPOS.Modeller
{
    /// <summary>
    /// Gerçekleşen satış işlemlerini temsil eden veri modeli.
    /// </summary>
    public class Satis
    {
        public int Id { get; set; }
        public string FisNo { get; set; }
        public decimal ToplamTutar { get; set; }
        /// <summary>
        /// Nakit, Kredi Kartı, Veresiye vb.
        /// </summary>
        public string OdemeTuru { get; set; }
        public DateTime Tarih { get; set; }
        public bool IptalEdildi { get; set; }
        public string SatisNumarasi { get; set; }
        public decimal NakitTutar { get; set; }
        public decimal KartTutar { get; set; }
        public decimal VeresiyeTutar { get; set; }
        public decimal IskontoTutari { get; set; }
        public decimal IadeTutari { get; set; }
        public int? MusteriId { get; set; }
        public string MusteriAdi { get; set; }
        public string KullaniciAdi { get; set; }
    }
}
