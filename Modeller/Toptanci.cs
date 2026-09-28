using System;

namespace MarketPOS.Modeller
{
    public class Toptanci
    {
        public int Id { get; set; }
        public string FirmaAdi { get; set; }
        public string Yetkili { get; set; }
        public string Telefon { get; set; }
        public decimal BorcMiktari { get; set; }
        public DateTime KayitTarihi { get; set; }
    }
}
