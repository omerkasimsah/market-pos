using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dapper;
using MarketPOS.Veritabani;
using MarketPOS.Modeller;

namespace MarketPOS.Arayuzler
{
    public class FaturaKalemDto : System.ComponentModel.INotifyPropertyChanged
    {
        public int UrunId { get; set; }
        public string Barkod { get; set; }
        public string UrunAdi { get; set; }
        
        private decimal _adet;
        public decimal Adet 
        { 
            get => _adet; 
            set { _adet = value; ToplamTutarHesapla(); OnPropertyChanged(nameof(Adet)); } 
        }

        private decimal _alisFiyati;
        public decimal AlisFiyati 
        { 
            get => _alisFiyati; 
            set { _alisFiyati = value; ToplamTutarHesapla(); OnPropertyChanged(nameof(AlisFiyati)); } 
        }

        private decimal _kdvOrani;
        public decimal KdvOrani 
        { 
            get => _kdvOrani; 
            set { _kdvOrani = value; ToplamTutarHesapla(); OnPropertyChanged(nameof(KdvOrani)); } 
        }

        private decimal _toplamTutar;
        public decimal ToplamTutar 
        { 
            get => _toplamTutar; 
            set { _toplamTutar = value; OnPropertyChanged(nameof(ToplamTutar)); } 
        }

        private void ToplamTutarHesapla()
        {
            ToplamTutar = Adet * AlisFiyati * (1 + (KdvOrani / 100m));
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    public partial class ToptanciFaturaWindow : Window
    {
        private readonly VeritabaniYoneticisi _db;
        private ObservableCollection<FaturaKalemDto> _faturaKalemleri = new ObservableCollection<FaturaKalemDto>();

        public ToptanciFaturaWindow()
        {
            InitializeComponent();
            _db = new VeritabaniYoneticisi();
            dgFaturaKalemleri.ItemsSource = _faturaKalemleri;
            
            _faturaKalemleri.CollectionChanged += (s, e) => FaturaToplamHesapla();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dpFaturaTarihi.SelectedDate = DateTime.Today;
            txtBarkod.Focus();
        }

        private void TxtBarkod_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string barkod = txtBarkod.Text.Trim();
                if (!string.IsNullOrEmpty(barkod))
                {
                    UrunEkleBorkoddan(barkod);
                    txtBarkod.Clear();
                    txtBarkod.Focus();
                }
            }
        }

        private void UrunEkleBorkoddan(string barkod)
        {
            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    var urun = baglanti.QueryFirstOrDefault<Urun>(
                        "SELECT * FROM Urunler WHERE Barkod = @Barkod", new { Barkod = barkod });

                    if (urun != null)
                    {
                        var mevcut = _faturaKalemleri.FirstOrDefault(x => x.UrunId == urun.Id);
                        if (mevcut != null)
                        {
                            mevcut.Adet += 1;
                        }
                        else
                        {
                            _faturaKalemleri.Add(new FaturaKalemDto
                            {
                                UrunId = urun.Id,
                                Barkod = urun.Barkod,
                                UrunAdi = urun.UrunAdi,
                                Adet = 1,
                                AlisFiyati = urun.AlisFiyati,
                                KdvOrani = urun.KdvOrani
                            });
                        }
                        FaturaToplamHesapla();
                    }
                    else
                    {
                        var cevap = MessageBox.Show("Bu barkoda ait ürün bulunamadı. Yeni ürün eklemek ister misiniz?", "Ürün Bulunamadı", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (cevap == MessageBoxResult.Yes)
                        {
                            UrunEkleDuzenleWindow urunEkleme = new UrunEkleDuzenleWindow(_db);
                            urunEkleme.ShowDialog();
                            // Eklendiyse otomatik tekrar deneriz
                            if (urunEkleme.DialogResult == true)
                            {
                                UrunEkleBorkoddan(barkod);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ürün aranırken hata oluştu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnManuelUrunEkle_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Manuel ürün bulma ekranı (Geliştirilme Aşamasında)", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnKalemSil_Click(object sender, RoutedEventArgs e)
        {
            if (dgFaturaKalemleri.SelectedItem is FaturaKalemDto secili)
            {
                _faturaKalemleri.Remove(secili);
            }
        }

        private void DgFaturaKalemleri_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => FaturaToplamHesapla()), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void FaturaToplamHesapla()
        {
            decimal toplam = _faturaKalemleri.Sum(x => x.ToplamTutar);
            txtFaturaToplam.Text = $"{toplam:N2} ₺";
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (_faturaKalemleri.Count == 0)
            {
                MessageBox.Show("Faturada hiç ürün bulunmuyor.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtFaturaNo.Text))
            {
                MessageBox.Show("Lütfen fatura / irsaliye numarasını girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtFaturaNo.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtToptanciAdi.Text))
            {
                MessageBox.Show("Lütfen toptancı / firma adını girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtToptanciAdi.Focus();
                return;
            }

            try
            {
                using (var baglanti = _db.BaglantiGetir())
                {
                    baglanti.Open();
                    using (var islem = baglanti.BeginTransaction())
                    {
                        // 1. Toptancı Faturası Ana Kaydı (ToptanciId 0 yapıyoruz, basitlik açısından adını Açıklama veya Fatura tablosuna yazıyoruz)
                        // Mevcut ToptanciFaturalari yapısı: Id, ToptanciId, FaturaNo, ToplamTutar, Tarih, Aciklama
                        decimal genelToplam = _faturaKalemleri.Sum(x => x.ToplamTutar);
                        string toptanciFaturaSql = @"
                            INSERT INTO ToptanciFaturalari (ToptanciId, FaturaNo, ToplamTutar, Tarih, Aciklama) 
                            VALUES (0, @FaturaNo, @ToplamTutar, @Tarih, @Aciklama);
                            SELECT last_insert_rowid();";
                        
                        int faturaId = baglanti.ExecuteScalar<int>(toptanciFaturaSql, new 
                        {
                            FaturaNo = txtFaturaNo.Text.Trim(),
                            ToplamTutar = genelToplam,
                            Tarih = dpFaturaTarihi.SelectedDate ?? DateTime.Today,
                            Aciklama = "Tedarikçi: " + txtToptanciAdi.Text.Trim()
                        }, islem);

                        // 2. Fatura Detayları ve Stok Artırma
                        string detaySql = @"
                            INSERT INTO ToptanciFaturaDetaylari (FaturaId, UrunId, UrunAdi, Adet, BirimFiyat, ToplamFiyat)
                            VALUES (@FaturaId, @UrunId, @UrunAdi, @Adet, @BirimFiyat, @ToplamFiyat)";

                        string stokSql = "UPDATE Urunler SET StokMiktari = StokMiktari + @Adet, AlisFiyati = @AlisFiyati WHERE Id = @Id";

                        foreach (var kalem in _faturaKalemleri)
                        {
                            baglanti.Execute(detaySql, new
                            {
                                FaturaId = faturaId,
                                UrunId = kalem.UrunId,
                                UrunAdi = kalem.UrunAdi,
                                Adet = kalem.Adet,
                                BirimFiyat = kalem.AlisFiyati,
                                ToplamFiyat = kalem.ToplamTutar
                            }, islem);

                            // Stoğu ve alış fiyatını güncelle (Eğer toptancı fiyatı değiştiyse)
                            baglanti.Execute(stokSql, new 
                            { 
                                Adet = kalem.Adet,
                                AlisFiyati = kalem.AlisFiyati,
                                Id = kalem.UrunId
                            }, islem);
                        }

                        islem.Commit();
                    }
                }

                MessageBox.Show("Toptancı faturası başarıyla kaydedildi ve stoklar güncellendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fatura kaydedilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
