using System;

namespace MarketPOS.Modeller
{
    /// <summary>
    /// Kasa giriş/çıkış veya genel hesap hareketlerini temsil eden model.
    /// </summary>
    public class Islem
    {
        public int Id { get; set; }
        /// <summary>
        /// Gelir veya Gider
        /// </summary>
        public string IslemTipi { get; set; }
        public string Aciklama { get; set; }
        public decimal Tutar { get; set; }
        public DateTime Tarih { get; set; }
        public int? MusteriId { get; set; }
        public int? SatisId { get; set; }
    }
}
