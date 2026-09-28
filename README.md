# 🛒 MarketPOS — Masaüstü Barkodlu Satış & Stok Otomasyonu
### Desktop Barcode POS & Inventory Management System

> **C#**, **WPF (.NET)**, **Dapper** ve **SQLite** ile geliştirilmiş; hızlı barkod okuma, ESC/POS termal fiş basımı, çoklu ödeme ve otomatik yedekleme özelliklerine sahip market otomasyonu.  
> *A full-featured desktop Point of Sale (POS) and inventory system built with C#, WPF (.NET), Dapper, and SQLite.*

---

## 🇹🇷 Türkçe Açıklama

### 🎯 Projenin Amacı
Market ve perakende satış noktalarında günlük satış, stok ve kasa operasyonlarını hızlandırmak, barkodlu ürün satışını ve termal fiş yazdırma süreçlerini kolaylaştırmak amacıyla geliştirilmiştir.

### ✨ Temel Özellikler
- **⚡ Hızlı Satış Ekranı:** Klavye kısayolları (F1-F7) ile tam uyumlu, barkod okuyucu ve dokunmatik ürün butonlarıyla anında sepete ekleme.
- **💳 Çift Yönlü POS ve Çoklu Ödeme:** Nakit (F1), Kredi Kartı/POS (F2), Veresiye Müşteri Hesabı (F3) ve Parçalı Ödeme desteği.
- **📦 Stok ve Parti (Lot) Takibi:** Ürünlerin kritik stok seviyeleri, KDV oranları, kar marjı hesaplamaları ve Son Kullanma Tarihi (SKT) uyarıları.
- **🖨️ Termal Fiş & Yazıcı Entegrasyonu:** ESC/POS komutlarıyla doğrudan termal yazıcılara (80mm/58mm) Türkçe karakter destekli fiş basımı ve otomatik kağıt kesme.
- **👥 Müşteri & Toptancı Cari Takibi:** Veresiye defteri, müşteri bakiye takibi, borç tahsilatları ve toptancı ödemeleri.
- **📊 Kasa & Gün Sonu Raporları:** Z-Raporu, anlık kasa durumu, gelir/gider kalemleri ve kârlılık dökümleri.
- **💾 Otomatik Yedekleme:** Uygulama kapanışında SQLite veritabanının otomatik sıkıştırılmış yedeğini alma.

### 🛠️ Kullanılan Teknolojiler
- **Dil & Arayüz:** C#, WPF (.NET Framework 4.8 / XAML)
- **Veritabanı & ORM:** SQLite (`System.Data.SQLite`), Dapper
- **Yazıcı & Donanım:** ESC/POS RAW Printing (`winspool.drv`), Seri Port / USB HID Barkod Okuyucu haberleşmesi

---

## 🇬🇧 English Overview

### 🎯 Purpose
Developed to streamline checkout lines, inventory management, and receipt printing in grocery stores and small-to-medium retail shops. Built to be fast, responsive, and completely operable via keyboard shortcuts without touching a mouse.

### ✨ Key Features
- **⚡ High-Speed Checkout:** Keyboard-driven workflow (F1–F7) with barcode reader integration and fast product touch buttons.
- **💳 Multi-Payment Support:** Cash (F1), Credit Card / POS Terminal (F2), Customer Credit Ledger / Veresiye (F3), and Split Payments.
- **📦 Inventory & Lot Tracking:** FIFO tracking, critical stock alerts, profit margin calculations, and expiration date notifications.
- **🖨️ ESC/POS Thermal Printing:** Native raw printing to 80mm/58mm receipt printers with auto-cutter support.
- **👥 Customer & Supplier Ledger:** Debt and credit tracking, customer transaction history, and supplier payments.
- **📊 Cash Flow & Daily Reports:** End-of-day Z-reports, cash drawer status, and expense tracking.
- **💾 Auto Backup:** Automated SQLite backups upon application exit.

---

## 🚀 Kurulum & Çalıştırma / Getting Started

1. **Depoyu klonlayın / Clone the repository:**
   ```bash
   git clone https://github.com/omerkasimsah/market-pos.git
   cd market-pos
   ```
2. **Visual Studio ile Açın / Open in Visual Studio:**
   `MarketPOS.csproj` dosyasını Visual Studio 2022 veya üzeri ile açın.
3. **NuGet Paketlerini Yükleyin / Restore NuGet Packages:**
   Proje açıldığında `Dapper` ve `System.Data.SQLite.Core` paketleri otomatik indirilecektir.
4. **Çalıştırın / Run:**
   `F5` veya `Ctrl+F5` ile projeyi derleyip çalıştırabilirsiniz. SQLite veritabanı ilk açılışta otomatik başlatılır.

---

## 📄 Lisans / License
Bu proje [MIT Lisansı](LICENSE) altında açık kaynak olarak paylaşılmıştır.
