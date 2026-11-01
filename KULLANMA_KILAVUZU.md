# 🛒 SüperMarket POS v3.0 - Kullanım Kılavuzu ve Sistem Dokümantasyonu

Bu doküman, **SüperMarket POS v3.0** Otomasyon Yazılımının tüm fonksiyonlarını, yeni eklenen Ana Menü ve Bağımsız Pencere Mimarisini, Çift Yönlü POS Terminali Onay/Red Akışını, Ayarlar Sekmeli Donanım ve Bağlantı Kılavuzlarını, Çoklu SKT ve Parti (Lot) Stok Takibini, KDV ve Kar Marjlı Ürün Kaydını ve Rozetli Akıllı Bildirim Merkezini kapsayan resmi kullanım kılavuzudur.

---

## 0. 🆕 v3.0 — MARKET / BAKKAL KULLANIMINA HAZIRLIK GÜNCELLEMESİ

### 0.1 İlk Kurulum (Mutlaka Yapın)
1. Programı açın, **admin / 1234** ile giriş yapın. Program sizden hemen **yeni şifre** belirlemenizi ister.
2. **👤 KULLANICILAR** kartından her kasiyer için ayrı hesap açın (Rol: Kasiyer). Kullanıcılar silinmez, ayrılan personel **pasif** yapılır.
3. **⚙️ AYARLAR > Donanım Ayarları** ekranında:
   - Dükkan adı, adres, telefon, vergi dairesi/no (fiş başlığında çıkar)
   - **Termal fiş yazıcısı** (seçilmezse fiş basılmaz), kağıt genişliği (58 / 80 mm), satış sonrası fiş: Otomatik / Sor / Yazdırma
   - Nakit satışta **para çekmecesi** açılsın mı
   - Stoğu yetersiz ürün satışına izin (kapalıysa stokta olmayan ürün satılamaz)
   - **Terazi barkodu** önekleri (varsayılan 27,28,29) ve son 5 hanenin **ağırlık (gram)** mı **fiyat (kuruş)** mı olduğu
   - Kritik stok uyarı seviyesi (ana menü rozeti, uyarı merkezi, sipariş listesi bunu kullanır)
4. Terazili ürünlerde ürün kartının barkod alanına terazideki **5 haneli ürün kodunu** (örn. `00123`) yazın.

### 0.2 Satış Ekranı
| İşlem | Nasıl |
| :--- | :--- |
| Barkod okutma | Okuyucu ile okutun; okumayan barkodu klavyeden/numpad'den yazıp **ENTER** veya **GİR ↵** |
| Çoklu adet | `3*` yazıp ürünü okutun (veya numpad'de `3` → **X (Adet)** → okut). Ondalık için `0,5*` |
| Terazi ürünü | Teraziden çıkan 13 haneli etiketi okutun; her tartım ayrı satır olarak eklenir |
| Barkodsuz ürün | **F12: Ürün Ara** (ad/barkod/kategori, Türkçe karakter uyumlu). Manav/Şarküteri butonları kategori araması açar |
| Adet artır/azalt | Satırı seçip **＋ / －** butonları veya numpad **+ / -** |
| Satır silme | Satırı seçip **🗑 SATIR SİL** veya **Delete** (kasiyerde yönetici şifresi istenir) |
| Hatalı girişi temizle | **ESC** veya numpad **C**, son karakter için **⌫ / Backspace** |

- Sepette ürün varken satış penceresi kapatılmak istenirse uyarı verilir.
- Satır silme, adet azaltma, sepet iptali (F4) ve iskonto (F5) kasiyer hesabında **yönetici onayı** ister; tüm onaylar log dosyasına yazılır.
- Fiş: dükkan bilgileri, satış no, kasiyer, müşteri, satırlar (KDV oranıyla), ara toplam, iskonto, **KDV dökümü**, ödeme dağılımı ve **"MALİ DEĞERİ YOKTUR"** ibaresi. Program ÖKC (yazar kasa) değildir; yasal fiş/fatura için ÖKC veya e-Arşiv kullanılmalıdır.

### 0.3 İade (F10 veya Ana Menü > İADE / DEĞİŞİM)
1. Satış No (`SAT-yyyyMMdd-0001`) veya Fiş No yazıp ENTER.
2. Her ürün için satılan, **önceden iade edilen** ve **iade edilebilir** miktar görünür; fazlası iade edilemez.
3. "İade Adet" alanına miktarı yazın, sebep ve yöntemi seçin:
   - **Nakit İade**: kasadan nakit düşer (Z raporunda "Nakit İade").
   - **Veresiye Bakiyeye İade**: müşterinin borcundan düşülür (sadece müşterili satışlarda).
4. Fiş altı iskonto varsa iade tutarı da aynı oranda indirimli hesaplanır.
5. Sebep **Bozuk/Hasarlı** veya **SKT Geçmiş** ise ürün stoğa geri eklenmez.
6. Kasiyer için yönetici onayı istenir, yazıcı tanımlıysa imzalı **iade fişi** basılır.
- **Tam iptal** (Kasa Özeti / Detaylı Raporlar): stokları geri yükler, bölünmüş ödemedeki veresiye kısmını da müşteri borcundan düşer. Kısmi iade yapılmış satış tam iptal edilemez.

### 0.4 Gün Sonu — Z Raporu (Ana Menü > Z RAPORU veya Kasa Özeti > Z Raporu)
1. **Kasa Açılış (Devir)** bir önceki Z raporunun sayılan nakdiyle otomatik dolar.
2. Ekran **Beklenen Nakit = Açılış + Nakit Satış + Veresiye Tahsilatı − Nakit İade − Giderler** hesaplar.
3. Kasadaki parayı sayıp **Sayılan Nakit** alanına yazın; **Kasa Farkı** (fazla/eksik) anında görünür.
4. Ayrıca ödeme dağılımı (nakit/kart/veresiye — bölünmüş ödemeler doğru ayrılır), iade, iskonto, **KDV oranına göre matrah ve KDV**, mal maliyeti, brüt ve net kâr gösterilir.
5. **Z Raporunu Kaydet**: her gün için yalnızca bir kez kaydedilir, ardından otomatik yedek alınır ve rapor yazdırılır.

### 0.5 Kullanıcı ve Güvenlik
- Şifreler tuzlu PBKDF2 ile saklanır; eski şifreler ilk girişte otomatik güvenli biçime dönüştürülür.
- 5 hatalı girişte giriş 30 saniye kilitlenir.
- Ana menüde **🔑 Şifremi Değiştir** ve vardiya değişimi için **🔄 Kullanıcı Değiştir** (program kapanmadan oturum kapatma).
- Sistemde en az bir aktif yönetici kalması zorunludur.
- Kasiyer: Satış, İade (yönetici onaylı). Yönetici: tüm menüler.

### 0.6 Yedekleme ve Kayıtlar
- Yedek; program açılışında, her gün 23:59'da ve her Z raporunda **SQLite çevrimiçi yedekleme** ile alınır (program açıkken bile bozulmaz). Son 10 yedek her zaman korunur, 30 günden eski diğerleri silinir.
- **Ayarlar > Yedekleme**: Şimdi Yedek Al, Yedek Klasörünü Aç, **Yedekten Geri Yükle** (geri yüklemeden önce mevcut durumun güvenlik yedeği `GeriYuklemeOncesi_*.db` alınır).
- Yedekleri düzenli olarak USB belleğe / buluta kopyalayın.
- Hatalar ve kritik işlemler (giriş, iptal, iade, yönetici onayları, Z raporu) program klasöründeki **`Loglar/log_yyyyMMdd.txt`** dosyalarına yazılır.
- Program aynı bilgisayarda iki kez açılamaz (veritabanı çakışmasını önler).

### 0.7 Geliştirici Notları (v3.0)
- Veritabanı yolu: program klasöründeki `MarketVeritabani.sqlite` (yoksa eski sürüm uyumluluğu için çalışma klasöründeki dosya). WAL modu ve sorgu indeksleri açık.
- Yeni kolonlar: `Satislar.IskontoTutari`, `SatisDetaylari.KdvOrani/AlisFiyati` (satış anındaki maliyet ve KDV), `Iadeler.KullaniciAdi`, `ZRaporlari.GunTarihi/BeklenenNakit/SayilanNakit/KasaFarki`. Migrasyon otomatiktir.
- Yeni servisler: `AyarServisi`, `SifreServisi`, `LogServisi`, `KasaServisi` (Kasa Özeti ve Z Raporu ortak hesap), `SatisServisi` (iptal ve fiş tekrar basımı).
- Yeni ekranlar: `KullaniciYonetimiWindow`, `SifreDegistirWindow`, `UrunAraWindow`.
- Açılışta örnek (demo) ürün eklenmesi kaldırıldı.

---

## 1. 🚀 ÖNCEKİ SÜRÜM ÖZELLİKLERİ

- **🛠️ BAML / TypeConverterMarkupExtension Hatası Tam Düzeltmesi (`UrunEkleDuzenleWindow` & `GiderUserControl`):** Ekran görüntüsündeki `Satır numarası '81' ve satır konumu '37' (System.Windows.Baml2006.TypeConverterMarkupExtension)` BAML ayrıştırma hatası kesin olarak çözüldü. WPF XAML standardında geçersiz olan CSS kökenli `HorizontalAlignment="SpaceBetween"` ifadesi XAML dosyalarından temizlendi, yerine standart WPF `Grid` sol/sağ hizalama düzeni yerleştirildi.
- **💳 Çift Yönlü POS Terminali Onay & Red Akışı (`PosBeklemeWindow` & `PosCihazServisi`):** `F2: Kredi Kartı` seçildiğinde POS cihazına tutar gönderilir ve `PosBeklemeWindow` onay ekranı açılır.
  - **POS Onaylandı:** Ekranda yeşil `✅ POS ONAYLANDI - Satış Tamamlandı` uyarısı çıkar, satış kasaya ve stoklara (FIFO) işlenir.
  - **POS Reddedildi (Yetersiz Bakiye / İptal):** Ekranda kırmızı `❌ POS İşlemi Reddedildi: Yetersiz Bakiye!` uyarısı gösterilir. **SATIŞ TAMAMLATILMAZ**, sepetteki ürünler korunur. Kasiyer başka bir ödeme yöntemine geçebilir.
- **📚 Ayarlar Ekranı Donanım & Bağlantı Kılavuzları (`AyarlarUserControl`):** Ayarlar ekranı sekmeli (Tabbed) yapıya dönüştürüldü:
  - **Sekme 1 (⚙️ Donanım Ayarları):** Dükkan Unvanı, Fiş Alt Notu, COM Port seçimi, Termal Yazıcı seçimi, `⚡ POS Test Ping` ve `🖨️ Deneme Fişi Yazdır` test butonları.
  - **Sekme 2 (💳 POS Kılavuzu):** ÖKC ve Banka POS Seri Port (COM 9600 8N1) bağlantı rehberi, STX/ETX mesajlaşma yapısı ve Yetersiz Bakiye protokol detayları.
  - **Sekme 3 (🖨️ Termal Yazıcı Kılavuzu):** ESC/POS USB, Ethernet, Bluetooth 80mm/58mm sürücü kurulum adımları.
  - **Sekme 4 (⚖️ Terazi & Barkod Okuyucu Rehberi):** USB HID barkod okuyucu ve 27 ile başlayan gramajlı terazi barkod çözümleme mantığı.
- **🏠 Her Ekrandan "Ana Menü"ye Anında Geçiş Butonu:** Tüm ekranların (`SatisWindow`, `StokYonetimWindow`, `MusteriWindow`, `KasaRaporWindow`, `ToptanciWindow`, `GiderWindow`, `RaporlarWindow`, `UyarilarWindow`, `AyarlarWindow`) en üst alanına **`🏠 Ana Menü`** geçiş butonu eklendi.

---

## 2. 📖 KASİYER KULLANIM REHBERİ (ADIM ADIM)

### 📦 Yeni Ürün Kaydı:
1. Ana Menüden **📦 STOK YÖNETİMİ** kartına tıklayın.
2. `Yeni Ürün Ekle` butonuna basın. Form artık BAML ayrıştırma hatası vermeden ekranın tam ortasında %100 sorunsuz açılacaktır.
3. Barkod, Ürün Adı, Alış Fiyatı, KDV Oranı, Satış Fiyatı ve SKT / Parti No bilgilerini girip **Kaydet** butonuna basın.

### 🛒 Satış Yapma ve POS Ödeme Alma:
1. Barkod okutun veya dokunmatik ürün butonlarına basın.
2. **`F2: Kredi Kartı`** butonuna bastığınızda ekrana `💳 POS Cihazı Ödeme İşlemi` penceresi gelir.
3. POS cihazı kartı onayladığında satış tamamlanır; yetersiz bakiye veya iptal durumunda kırmızı uyarı dökülür ve sepet sıfırlanmaz.

---

## 3. ⌨️ KLAVYE KISAYOLLARI VE HIZLI TUŞ LİSTESİ

| Kısayol Tuşu | İşlev / Açıklama |
| :--- | :--- |
| **F1** | Nakit Satışı Onayla ve Tamamla |
| **F2** | Kredi Kartı Satışı (POS Onayı Bekler ve İşler) |
| **F3** | Veresiye Satış (Müşteri Seçim Penceresini Açar) |
| **F4** | Sepetteki Ürünleri Temizle / İptal Et (kasiyerde yönetici onayı) |
| **F5** | İskonto / İndirim Uygulama Penceresi (kasiyerde yönetici onayı) |
| **F6** | Para Üstü Hesaplama |
| **F7** | Satışı Beklet (Sepeti Dondurur ve Temizler) |
| **F8** | Bölünmüş Ödeme (Nakit + Kart + Veresiye) |
| **F9** | Fiyat Gör |
| **F10** | İade / Değişim |
| **F11** | Son Fişi Tekrar Yazdır |
| **F12** | Ürün Ara (ad / barkod / kategori) |
| **3*** + barkod | 3 adet ekle (ondalık: `0,5*`) |
| **Numpad + / -** | Seçili satırın adedini artır / azalt |
| **Delete** | Seçili satırı sil |
| **ENTER** | Modal Pencerelerde "Kaydet / Onayla" Butonunu Tetikler |
| **ESC** | Modal Pencereleri ve Pop-up'ları Kapatır |

---

## 4. 🛠️ TEKNİK VE VERİTABANI DEĞİŞİKLİKLERİ (Geliştirici Notları)

### BAML / XAML Düzeltmesi:
- **`UrunEkleDuzenleWindow.xaml` (Satır 81):** WPF'te geçersiz olan `HorizontalAlignment="SpaceBetween"` parametresi kaldırılarak standart WPF `<Grid>` sol/sağ hizalamasına dönüştürüldü.
- **`GiderUserControl.xaml` (Satır 76):** Benzer şekilde `HorizontalAlignment="SpaceBetween"` parametresi `<Grid>` yapısına dönüştürüldü.

---

## 5. 💡 BİLİNMESİ GEREKENLER VE SORUN GİDERME (Troubleshooting)

- **BAML Hatası:** XAML dosyasındaki geçersiz hizalama tanımı düzeltildiğinden "Ürün Ekle" ve "Düzenle" butonları artık hata vermeden anında açılır.
- **Tüm Pencerelerde Kararlılık:** Projedeki tüm ekranlar WPF standartlarına %100 uygundur ve `dotnet build` 0 uyarı/0 hata verir.
