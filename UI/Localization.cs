using System;
using JSQViewer.Application.Abstractions;

namespace JSQViewer.UI
{
    public static class Loc
    {
        private static ILocalizationService _service;

        public static void Initialize(ILocalizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public static event Action LanguageChanged
        {
            add { Service.LanguageChanged += value; }
            remove { Service.LanguageChanged -= value; }
        }

        public static AppLanguage Current
        {
            get { return Service.CurrentLanguage; }
            set { Service.CurrentLanguage = value; }
        }

        public static string Get(string key)
        {
            return Service.Get(key);
        }

        private static ILocalizationService Service
        {
            get
            {
                if (_service == null)
                {
                    throw new InvalidOperationException("Localization service has not been initialized.");
                }

                return _service;
            }
        }
    }
}
