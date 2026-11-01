using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MarketPOS.Servisler
{
    /// <summary>
    /// Kullanıcı şifrelerini tuzlu PBKDF2 ile saklar.
    /// Eski sürümlerden kalan tuzsuz SHA256 kayıtlarını da doğrular (girişte otomatik yükseltilir).
    /// Biçim: PBKDF2$iterasyon$tuzBase64$hashBase64
    /// </summary>
    public static class SifreServisi
    {
        private const int Iterasyon = 10000;
        private const string Onek = "PBKDF2$";

        public static string Hashle(string sifre)
        {
            byte[] tuz = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(tuz);

            using (var pbkdf2 = new Rfc2898DeriveBytes(sifre, tuz, Iterasyon))
            {
                byte[] hash = pbkdf2.GetBytes(32);
                return $"{Onek}{Iterasyon}${Convert.ToBase64String(tuz)}${Convert.ToBase64String(hash)}";
            }
        }

        public static bool Dogrula(string sifre, string kayitliHash)
        {
            if (string.IsNullOrEmpty(sifre) || string.IsNullOrEmpty(kayitliHash)) return false;

            if (!kayitliHash.StartsWith(Onek))
            {
                return string.Equals(EskiSha256(sifre), kayitliHash, StringComparison.OrdinalIgnoreCase);
            }

            string[] parcalar = kayitliHash.Split('$');
            if (parcalar.Length != 4 || !int.TryParse(parcalar[1], out int iterasyon)) return false;

            byte[] tuz = Convert.FromBase64String(parcalar[2]);
            byte[] beklenen = Convert.FromBase64String(parcalar[3]);

            using (var pbkdf2 = new Rfc2898DeriveBytes(sifre, tuz, iterasyon))
            {
                byte[] hesaplanan = pbkdf2.GetBytes(beklenen.Length);
                // Zamanlama saldırılarına karşı sabit süreli karşılaştırma
                int fark = 0;
                for (int i = 0; i < beklenen.Length; i++) fark |= beklenen[i] ^ hesaplanan[i];
                return fark == 0;
            }
        }

        /// <summary>Kayıt eski (tuzsuz) biçimdeyse true döner; giriş sonrası yeniden hashlenmelidir.</summary>
        public static bool YukseltmeGerekli(string kayitliHash) => kayitliHash != null && !kayitliHash.StartsWith(Onek);

        private static string EskiSha256(string sifre)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sifre));
                return string.Concat(bytes.Select(b => b.ToString("x2")));
            }
        }
    }
}
