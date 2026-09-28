# 🛒 SüperMarket POS v1.0 - Kullanım Kılavuzu ve Sistem Dokümantasyonu

Bu doküman, **SüperMarket POS v1.0** Otomasyon Yazılımının tüm fonksiyonlarını, yeni eklenen Ana Menü ve Bağımsız Pencere Mimarisini, Çift Yönlü POS Terminali Onay/Red Akışını, Ayarlar Sekmeli Donanım ve Bağlantı Kılavuzlarını, Çoklu SKT ve Parti (Lot) Stok Takibini, KDV ve Kar Marjlı Ürün Kaydını ve Rozetli Akıllı Bildirim Merkezini kapsayan resmi kullanım kılavuzudur.

---

## 1. 🚀 SON EKLENEN ÖZELLİKLER VE YENİLİKLER

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
| **F4** | Sepetteki Ürünleri Temizle / İptal Et |
| **F5** | İskonto / İndirim Uygulama Penceresi |
| **F6** | Hızlı Hesap Makinesi Pop-up Penceresini Açar |
| **F7** | Satışı Beklet (Sepeti Dondurur ve Temizler) |
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
