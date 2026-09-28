using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using MarketPOS.Modeller;

namespace MarketPOS.Servisler
{
    /// <summary>
    /// ÖKC ve Banka POS terminallerine Seri Port (COM) üzerinden STX/ETX protokolü ile tutar gönderen ve onay/red yanıtını okuyan servis.
    /// </summary>
    public class PosCihazServisi
    {
        private const char STX = (char)0x02; // Start of Text
        private const char ETX = (char)0x03; // End of Text
        private const char ACK = (char)0x06; // Acknowledge / Onay
        private const char NAK = (char)0x15; // Negative Acknowledge / Red

        /// <summary>
        /// Belirtilen COM portuna STX/ETX formatında satış tutarını gönderir ve cihaz yanıtını bekler.
        /// </summary>
        public PosIslemSonucu SatisGonder(decimal tutar, string fisNo, string portAdi = "COM3", int beklemeSuresiSaniye = 15)
        {
            SerialPort seriPort = null;
            try
            {
                seriPort = new SerialPort(portAdi, 9600, Parity.None, 8, StopBits.One)
                {
                    WriteTimeout = 3000,
                    ReadTimeout = beklemeSuresiSaniye * 1000
                };

                seriPort.Open();

                // STX + Komut + ETX Paket Formatı
                string paket = $"{STX}SATIS;{tutar:F2};{fisNo}{ETX}";
                byte[] veri = Encoding.ASCII.GetBytes(paket);

                seriPort.Write(veri, 0, veri.Length);
                Debug.WriteLine($"POS Cihazına Tutar Gönderildi -> Port: {portAdi}, Paket: {paket}");

                // Yanıt Bekleme
                string yanitMetni = "";
                DateTime baslangic = DateTime.Now;

                while ((DateTime.Now - baslangic).TotalSeconds < beklemeSuresiSaniye)
                {
                    if (seriPort.BytesToRead > 0)
                    {
                        yanitMetni += seriPort.ReadExisting();

                        if (yanitMetni.Contains(((char)ETX).ToString()) || yanitMetni.Contains(((char)ACK).ToString()) || yanitMetni.Contains(((char)NAK).ToString()))
                        {
                            break;
                        }
                    }
                    System.Threading.Thread.Sleep(100);
                }

                seriPort.Close();

                if (string.IsNullOrWhiteSpace(yanitMetni))
                {
                    return new PosIslemSonucu
                    {
                        Basarili = false,
                        Mesaj = "❌ POS Cihazından Yanıt Alınamadı (Zaman Aşımı)",
                        HataKodu = "TIMEOUT"
                    };
                }

                if (yanitMetni.Contains("ONAY") || yanitMetni.Contains(ACK.ToString()) || yanitMetni.Contains("00"))
                {
                    return new PosIslemSonucu
                    {
                        Basarili = true,
                        Mesaj = "✅ POS İşlemi Başarıyla Onaylandı",
                        OnayKodu = "ONAY-" + DateTime.Now.ToString("HHmmss")
                    };
                }
                else if (yanitMetni.Contains("YETERSIZ") || yanitMetni.Contains("BAKIYE"))
                {
                    return new PosIslemSonucu
                    {
                        Basarili = false,
                        Mesaj = "❌ POS İşlemi Reddedildi: Yetersiz Bakiye!",
                        HataKodu = "YETERSIZ_BAKIYE"
                    };
                }
                else if (yanitMetni.Contains("SIFRE") || yanitMetni.Contains("PIN"))
                {
                    return new PosIslemSonucu
                    {
                        Basarili = false,
                        Mesaj = "❌ POS İşlemi Reddedildi: Hatalı Şifre / Müşteri İptali",
                        HataKodu = "HATALI_PIN"
                    };
                }
                else
                {
                    return new PosIslemSonucu
                    {
                        Basarili = false,
                        Mesaj = $"❌ POS İşlemi Reddedildi ({yanitMetni.Trim()})",
                        HataKodu = "ISLEM_RED"
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"POS Cihazı Bağlantı Hatası ({portAdi}): {ex.Message}");
                return new PosIslemSonucu
                {
                    Basarili = false,
                    Mesaj = $"❌ POS Cihazına Bağlanılamadı (Port: {portAdi})",
                    HataKodu = "PORT_ERROR"
                };
            }
            finally
            {
                if (seriPort != null && seriPort.IsOpen)
                {
                    try { seriPort.Close(); } catch { }
                }
            }
        }
    }
}
