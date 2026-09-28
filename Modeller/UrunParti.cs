using System;

namespace MarketPOS.Modeller
{
    /// <summary>
    /// Ürünlerin Son Kullanma Tarihi (SKT) ve Parti/Lot stok takibi için model.
    /// </summary>
    public class UrunParti
    {
        public int Id { get; set; }
        public int UrunId { get; set; }
        public string PartiNo { get; set; }
        public decimal StokMiktari { get; set; }
        public DateTime SktTarihi { get; set; }
        public DateTime EklenmeTarihi { get; set; }

        public bool SktYaklasti => SktTarihi <= DateTime.Today.AddDays(15);
        public bool SktGecti => SktTarihi < DateTime.Today;
        public int KalanGunSayisi => (SktTarihi.Date - DateTime.Today).Days;
    }
}
