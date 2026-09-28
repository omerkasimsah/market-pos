using System;

namespace MarketPOS.Modeller
{
    /// <summary>
    /// Veresiye yazdırılan veya kayıtlı olan müşterileri temsil eden model.
    /// </summary>
    public class Musteri
    {
        public int Id { get; set; }
        public string AdSoyad { get; set; }
        public string Telefon { get; set; }
        public decimal BorcMiktari { get; set; }
        public DateTime KayitTarihi { get; set; }
    }
}
