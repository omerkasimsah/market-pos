using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Text;
using MarketPOS.Modeller;

namespace MarketPOS.Servisler
{
    /// <summary>
    /// Termal fiş yazıcılarına ESC/POS komutları ile döküm gönderen servis.
    /// Raw Printing (Win32 Spooler API) kullanır.
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

        /// <summary>
        /// Sepet verileriyle termal fiş dökümü oluşturur ve varsayılan/seçilen yazıcıya gönderir.
        /// </summary>
        public bool FisYazdir(string fisNo, DateTime tarih, IEnumerable<SepetUrunu> urunler, decimal toplamTutar, string odemeTipi, string dukkanAd = "SÜPERMARKET POS")
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                // Fiş Başlığı
                sb.AppendLine("--------------------------------");
                sb.AppendLine(OrtalaMetin(dukkanAd, 32));
                sb.AppendLine(OrtalaMetin("SATIŞ FİŞİ / BİLGİ", 32));
                sb.AppendLine($"Tarih: {tarih:dd.MM.yyyy}  Saat: {tarih:HH:mm}");
                sb.AppendLine($"Fiş No: {fisNo}");
                sb.AppendLine("--------------------------------");
                sb.AppendLine("Ürün Adı        Adet   Tutar");
                sb.AppendLine("--------------------------------");

                foreach (var urun in urunler)
                {
                    string ad = urun.UrunAdi.Length > 15 ? urun.UrunAdi.Substring(0, 15) : urun.UrunAdi.PadRight(15);
                    string adet = urun.Adet.ToString("G").PadLeft(6);
                    string tutar = $"{urun.ToplamFiyat:N2} TL".PadLeft(9);

                    sb.AppendLine($"{ad}{adet} {tutar}");
                }

                sb.AppendLine("--------------------------------");
                sb.AppendLine($"TOPLAM TUTAR:          {toplamTutar:N2} TL");
                sb.AppendLine($"Ödeme Tipi: {odemeTipi}");
                sb.AppendLine("--------------------------------");
                sb.AppendLine(OrtalaMetin("BİZİ TERCİH ETTİĞİNİZ", 32));
                sb.AppendLine(OrtalaMetin("İÇİN TEŞEKKÜRLER!", 32));
                sb.AppendLine("--------------------------------");
                sb.AppendLine("\n\n");

                string fisMetni = sb.ToString();

                // ESC/POS Komutları:
                // Basit metin modunda 857 (CP857 Turkish) veya 1254 Türkçe kod sayfası kullanılabilir.
                byte[] metinBytes = Encoding.GetEncoding(857).GetBytes(fisMetni);

                // ESC/POS Kağıt Kesme Komutu: GS V 0 (0x1D 0x56 0x00)
                byte[] kagitKesme = new byte[] { 0x1D, 0x56, 0x00 };

                byte[] tumBytes = new byte[metinBytes.Length + kagitKesme.Length];
                Buffer.BlockCopy(metinBytes, 0, tumBytes, 0, metinBytes.Length);
                Buffer.BlockCopy(kagitKesme, 0, tumBytes, metinBytes.Length, kagitKesme.Length);

                // Varsayılan Yazıcıyı Al
                PrinterSettings settings = new PrinterSettings();
                string yaziciAdi = settings.PrinterName;

                Debug.WriteLine($"Fiş Oluşturuldu:\n{fisMetni}");

                if (!string.IsNullOrEmpty(yaziciAdi))
                {
                    return HamVeriGonder(yaziciAdi, tumBytes);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Yazıcı Servisi Hatası: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Geçmiş satış detayları için aşırı yüklenmiş fiş yazdırma metodu.
        /// </summary>
        public bool FisYazdir(Satis satis, IEnumerable<SatisDetay> detaylar, string dukkanAd = "SÜPERMARKET POS")
        {
            List<SepetUrunu> sepetUrunleri = new List<SepetUrunu>();
            foreach (var d in detaylar)
            {
                sepetUrunleri.Add(new SepetUrunu
                {
                    UrunId = d.UrunId,
                    UrunAdi = d.UrunAdi,
                    Adet = d.Adet,
                    BirimFiyat = d.BirimFiyat
                });
            }

            return FisYazdir(satis.FisNo, satis.Tarih, sepetUrunleri, satis.ToplamTutar, satis.OdemeTuru, dukkanAd);
        }

        private string OrtalaMetin(string metin, int genislik)
        {
            if (string.IsNullOrEmpty(metin)) return new string(' ', genislik);
            if (metin.Length >= genislik) return metin.Substring(0, genislik);
            int solBosluk = (genislik - metin.Length) / 2;
            return metin.PadLeft(solBosluk + metin.Length).PadRight(genislik);
        }
    }
}
