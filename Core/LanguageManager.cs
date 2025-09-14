using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace EZHolodotNet.Core
{
    public class LanguageManager:INotifyPropertyChanged
    {
        private readonly ResourceManager _resourceManager;

        private static readonly Lazy<LanguageManager> _instance = new(() => new LanguageManager());
        public static LanguageManager Instance => _instance.Value;
        public event PropertyChangedEventHandler? PropertyChanged;

        public LanguageManager()
        {
            _resourceManager = new ResourceManager("EZHolodotNet.Properties.Resources", typeof(LanguageManager).Assembly);
        }

        public string this[string key]
        {
            get
            {
                string value = _resourceManager.GetString(key) ?? string.Empty;
                return value;
            }
            set
            {
                return;
            }
        }
        public void ChangeLanguage(CultureInfo cultureInfo)
        {
            CultureInfo.CurrentUICulture = cultureInfo;
            CultureInfo.CurrentCulture = cultureInfo;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }
}
