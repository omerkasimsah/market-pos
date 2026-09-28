namespace MarketPOS.Modeller
{
    /// <summary>
    /// Bir faturanın/fişin içindeki her bir satırın (ürünün) bilgisini tutar.
    /// </summary>
    public class SatisDetay
    {
        public int Id { get; set; }
        public int SatisId { get; set; }
        public int UrunId { get; set; }
        public string UrunAdi { get; set; } // Hızlı görünüm için
        public decimal Adet { get; set; }
        public decimal BirimFiyat { get; set; }
        public decimal ToplamFiyat { get; set; }
    }
}
