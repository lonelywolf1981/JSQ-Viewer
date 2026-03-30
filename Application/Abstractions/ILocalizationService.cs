namespace JSQViewer.Application.Abstractions
{
    public enum AppLanguage
    {
        Ru,
        En
    }

    public interface ILocalizationService
    {
        AppLanguage CurrentLanguage { get; set; }
        event System.Action LanguageChanged;
        string Get(string key);
    }
}
