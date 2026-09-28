using System;

namespace MarketPOS.Modeller
{
    public static class AktifOturum
    {
        public static int KullaniciId { get; set; }
        public static string KullaniciAdi { get; set; }
        public static string AdSoyad { get; set; }
        public static string Rol { get; set; } // 'Yonetici' veya 'Kasiyer'
        
        public static bool YoneticiMi => Rol == "Yonetici";

        public static void OturumAc(int id, string kullaniciAdi, string adSoyad, string rol)
        {
            KullaniciId = id;
            KullaniciAdi = kullaniciAdi;
            AdSoyad = adSoyad;
            Rol = rol;
        }

        public static void OturumKapat()
        {
            KullaniciId = 0;
            KullaniciAdi = null;
            AdSoyad = null;
            Rol = null;
        }
    }
}
