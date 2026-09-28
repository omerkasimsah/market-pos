using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MarketPOS.Modeller
{
    /// <summary>
    /// Satış ekranındaki sepette bulunan ürünleri temsil eden model.
    /// Arayüzün güncellenmesi için INotifyPropertyChanged kullanır.
    /// </summary>
    public class SepetUrunu : INotifyPropertyChanged
    {
        private decimal _adet;
        
        public int UrunId { get; set; }
        public string Barkod { get; set; }
        public string UrunAdi { get; set; }
        public decimal BirimFiyat { get; set; }

        public decimal Adet
        {
            get => _adet;
            set
            {
                if (_adet != value)
                {
                    _adet = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ToplamFiyat));
                }
            }
        }

        public decimal ToplamFiyat => Adet * BirimFiyat;

        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
