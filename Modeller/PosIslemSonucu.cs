namespace MarketPOS.Modeller
{
    public class PosIslemSonucu
    {
        public bool Basarili { get; set; }
        public string Mesaj { get; set; }
        public string OnayKodu { get; set; }
        public string HataKodu { get; set; }
    }
}
