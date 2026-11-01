using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MarketPOS.Modeller;

namespace MarketPOS.Servisler
{
    /// <summary>
    /// Fiş üzerine basılacak satış bilgisi.
    /// </summary>
    public class FisBilgisi
    {
        public string FisNo { get; set; }
        public string SatisNumarasi { get; set; }
        public DateTime Tarih { get; set; } = DateTime.Now;
        public List<SepetUrunu> Urunler { get; set; } = new List<SepetUrunu>();
        public decimal Iskonto { get; set; }
        public decimal ToplamTutar { get; set; }
        public string OdemeTuru { get; set; }
        public decimal NakitTutar { get; set; }
        public decimal KartTutar { get; set; }
        public decimal VeresiyeTutar { get; set; }
        public string MusteriAdi { get; set; }
        public string Kasiyer { get; set; }
        public bool Kopya { get; set; }
    }

    /// <summary>
    /// Termal fiş yazıcılarına ESC/POS komutları ile döküm gönderen servis.
    /// Raw Printing (Win32 Spooler API) kullanır. Yazıcı ve fiş başlığı Ayarlar ekranından okunur.
    /// </summary>
    public class YaziciServisi
    {
        #region Win32 Raw Printer API

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        private static bool HamVeriGonder(string yaziciAdi, byte[] veri)
        {
            IntPtr pBytes = Marshal.AllocCoTaskMem(veri.Length);
            Marshal.Copy(veri, 0, pBytes, veri.Length);

            DOCINFOA di = new DOCINFOA
            {
                pDocName = "Market POS Fişi",
                pDataType = "RAW"
            };

            bool basarili = false;
            if (OpenPrinter(yaziciAdi, out IntPtr hPrinter, IntPtr.Zero))
            {
                if (StartDocPrinter(hPrinter, 1, di))
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        basarili = WritePrinter(hPrinter, pBytes, veri.Length, out _);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }
            Marshal.FreeCoTaskMem(pBytes);
            return basarili;
        }

        #endregion

        #region ESC/POS Komutları

        private static readonly byte[] YaziciBaslat = { 0x1B, 0x40 };                 // ESC @
        private static readonly byte[] TurkceKodSayfasi = { 0x1B, 0x74, 0x0D };       // ESC t 13 (PC857 Türkçe)
        private static readonly byte[] BeslemeVeKes = { 0x1D, 0x56, 0x42, 0x03 };     // GS V B 3 (besle + kısmi kes)
        private static readonly byte[] CekmeceDarbesi = { 0x1B, 0x70, 0x00, 0x19, 0xFA }; // ESC p 0 25 250

        #endregion

        /// <summary>Ayarlarda yazıcı seçilmiş mi?</summary>
        public static bool YaziciTanimli => !string.IsNullOrWhiteSpace(AyarServisi.Oku(AyarServisi.YaziciAdi));

        private static int Genislik => AyarServisi.OkuInt(AyarServisi.FisGenisligi, 32) >= 48 ? 48 : 32;

        /// <summary>
        /// Satış fişini oluşturur ve ayarlardaki yazıcıya gönderir.
        /// </summary>
        public bool FisYazdir(FisBilgisi fis, bool cekmeceAc = false)
        {
            return Gonder(FisMetniOlustur(fis), cekmeceAc, AyarServisi.Oku(AyarServisi.YaziciAdi));
        }

        /// <summary>
        /// Ayarlar ekranındaki deneme butonu için: belirtilen yazıcıya örnek fiş gönderir.
        /// </summary>
        public bool DenemeFisiYazdir(string yaziciAdi)
        {
            var fis = new FisBilgisi
            {
                FisNo = "TEST-001",
                SatisNumarasi = "DENEME",
                Urunler = new List<SepetUrunu> { new SepetUrunu { UrunAdi = "TEST ÜRÜNÜ (DENEME)", Adet = 1, BirimFiyat = 10.00m, KdvOrani = 10 } },
                ToplamTutar = 10.00m,
                OdemeTuru = "Nakit",
                NakitTutar = 10.00m,
                Kasiyer = AktifOturum.AdSoyad ?? "-"
            };
            return Gonder(FisMetniOlustur(fis), false, yaziciAdi);
        }

        /// <summary>
        /// Z raporu gibi serbest metinleri yazıcıya gönderir.
        /// </summary>
        public bool MetinYazdir(string metin)
        {
            return Gonder(metin, false, AyarServisi.Oku(AyarServisi.YaziciAdi));
        }

        /// <summary>
        /// Fiş basmadan sadece para çekmecesini açar (yazıcıya bağlı çekmeceler için).
        /// </summary>
        public bool CekmeceyiAc()
        {
            string yazici = AyarServisi.Oku(AyarServisi.YaziciAdi);
            if (string.IsNullOrWhiteSpace(yazici)) return false;
            try { return HamVeriGonder(yazici, YaziciBaslat.Concat(CekmeceDarbesi).ToArray()); }
            catch (Exception ex) { LogServisi.Hata("Çekmece açılamadı", ex); return false; }
        }

        public string FisMetniOlustur(FisBilgisi fis)
        {
            int w = Genislik;
            string cizgi = new string('-', w);
            var sb = new StringBuilder();

            sb.AppendLine(Ortala(AyarServisi.Oku(AyarServisi.DukkanAdi), w));
            foreach (var satir in SatirlaraBol(AyarServisi.Oku(AyarServisi.DukkanAdres), w)) sb.AppendLine(Ortala(satir, w));
            string tel = AyarServisi.Oku(AyarServisi.DukkanTelefon);
            if (!string.IsNullOrWhiteSpace(tel)) sb.AppendLine(Ortala("Tel: " + tel, w));
            string vergi = AyarServisi.Oku(AyarServisi.VergiBilgisi);
            if (!string.IsNullOrWhiteSpace(vergi)) sb.AppendLine(Ortala(vergi, w));
            sb.AppendLine(cizgi);
            sb.AppendLine(Ortala(fis.Kopya ? "BİLGİ FİŞİ (KOPYA)" : "BİLGİ FİŞİ", w));
            sb.AppendLine(IkiyeBol($"Tarih: {fis.Tarih:dd.MM.yyyy}", $"Saat: {fis.Tarih:HH:mm}", w));
            if (!string.IsNullOrEmpty(fis.SatisNumarasi)) sb.AppendLine($"Satış No: {fis.SatisNumarasi}");
            sb.AppendLine($"Fiş No: {fis.FisNo}");
            if (!string.IsNullOrEmpty(fis.Kasiyer)) sb.AppendLine($"Kasiyer: {fis.Kasiyer}");
            if (!string.IsNullOrEmpty(fis.MusteriAdi)) sb.AppendLine($"Müşteri: {fis.MusteriAdi}");
            sb.AppendLine(cizgi);

            foreach (var urun in fis.Urunler)
            {
                string ad = (urun.UrunAdi ?? "").Length > w ? urun.UrunAdi.Substring(0, w) : urun.UrunAdi;
                sb.AppendLine(ad);
                string miktar = $"  {urun.Adet:0.###} x {urun.BirimFiyat:N2}";
                if (urun.KdvOrani > 0) miktar += $" %{urun.KdvOrani:0}";
                sb.AppendLine(IkiyeBol(miktar, $"{urun.ToplamFiyat:N2}", w));
            }

            sb.AppendLine(cizgi);
            decimal araToplam = fis.Urunler.Sum(x => x.ToplamFiyat);
            if (fis.Iskonto > 0)
            {
                sb.AppendLine(IkiyeBol("ARA TOPLAM", $"{araToplam:N2}", w));
                sb.AppendLine(IkiyeBol("İSKONTO", $"-{fis.Iskonto:N2}", w));
            }
            sb.AppendLine(IkiyeBol("TOPLAM", $"{fis.ToplamTutar:N2} TL", w));

            // KDV dökümü (iskonto satırlara oransal dağıtılır)
            decimal oran = araToplam > 0 ? fis.ToplamTutar / araToplam : 1m;
            var kdvGruplari = fis.Urunler.Where(x => x.KdvOrani > 0).GroupBy(x => x.KdvOrani).OrderBy(g => g.Key);
            decimal toplamKdv = 0;
            foreach (var grup in kdvGruplari)
            {
                decimal brut = grup.Sum(x => x.ToplamFiyat) * oran;
                decimal kdv = brut - brut / (1m + grup.Key / 100m);
                toplamKdv += kdv;
                sb.AppendLine(IkiyeBol($"KDV %{grup.Key:0}", $"{kdv:N2}", w));
            }
            if (toplamKdv > 0) sb.AppendLine(IkiyeBol("TOPLAM KDV", $"{toplamKdv:N2}", w));

            sb.AppendLine(cizgi);
            if (fis.NakitTutar > 0) sb.AppendLine(IkiyeBol("NAKİT", $"{fis.NakitTutar:N2}", w));
            if (fis.KartTutar > 0) sb.AppendLine(IkiyeBol("KREDİ KARTI", $"{fis.KartTutar:N2}", w));
            if (fis.VeresiyeTutar > 0) sb.AppendLine(IkiyeBol("VERESİYE", $"{fis.VeresiyeTutar:N2}", w));
            if (fis.NakitTutar == 0 && fis.KartTutar == 0 && fis.VeresiyeTutar == 0) sb.AppendLine($"Ödeme: {fis.OdemeTuru}");
            sb.AppendLine(cizgi);

            foreach (var satir in SatirlaraBol(AyarServisi.Oku(AyarServisi.FisAltNotu), w)) sb.AppendLine(Ortala(satir, w));
            sb.AppendLine(Ortala("MALİ DEĞERİ YOKTUR", w));
            sb.AppendLine();
            return sb.ToString();
        }

        private bool Gonder(string metin, bool cekmeceAc, string yaziciAdi)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(yaziciAdi))
                {
                    // Yazıcı seçilmemişse (örn. sadece ekran kullanımı) varsayılan lazer yazıcıya çöp basılmasın
                    LogServisi.Bilgi("Fiş yazdırılmadı: Ayarlarda yazıcı seçilmemiş.");
                    return false;
                }

                var veri = new List<byte>();
                veri.AddRange(YaziciBaslat);
                veri.AddRange(TurkceKodSayfasi);
                veri.AddRange(Encoding.GetEncoding(857).GetBytes(metin + "\n\n\n"));
                veri.AddRange(BeslemeVeKes);
                if (cekmeceAc) veri.AddRange(CekmeceDarbesi);

                bool basarili = HamVeriGonder(yaziciAdi, veri.ToArray());
                if (!basarili) LogServisi.Hata($"Yazıcıya gönderilemedi: {yaziciAdi}");
                return basarili;
            }
            catch (Exception ex)
            {
                LogServisi.Hata("Yazıcı servisi hatası", ex);
                return false;
            }
        }

        private static string IkiyeBol(string sol, string sag, int genislik)
        {
            int bosluk = genislik - sol.Length - sag.Length;
            if (bosluk < 1)
            {
                sol = sol.Substring(0, Math.Max(0, genislik - sag.Length - 1));
                bosluk = 1;
            }
            return sol + new string(' ', bosluk) + sag;
        }

        private static IEnumerable<string> SatirlaraBol(string metin, int genislik)
        {
            if (string.IsNullOrWhiteSpace(metin)) yield break;
            var satir = new StringBuilder();
            foreach (var kelime in metin.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (satir.Length > 0 && satir.Length + 1 + kelime.Length > genislik)
                {
                    yield return satir.ToString();
                    satir.Clear();
                }
                if (satir.Length > 0) satir.Append(' ');
                satir.Append(kelime.Length > genislik ? kelime.Substring(0, genislik) : kelime);
            }
            if (satir.Length > 0) yield return satir.ToString();
        }

        private static string Ortala(string metin, int genislik)
        {
            if (string.IsNullOrEmpty(metin)) return "";
            if (metin.Length >= genislik) return metin.Substring(0, genislik);
            return new string(' ', (genislik - metin.Length) / 2) + metin;
        }

        public static int FisGenisligiKarakter => Genislik;
        public static string IkiSutun(string sol, string sag) => IkiyeBol(sol, sag, Genislik);
        public static string OrtalaSatir(string metin) => Ortala(metin, Genislik);
    }
}
