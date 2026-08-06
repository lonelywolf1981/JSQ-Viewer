using JSQViewer.Application.Database;

namespace JSQViewer.Application.Abstractions
{
    public interface IDatabaseSettingsRepository
    {
        DatabaseConnectionSettings Load();

        bool Save(DatabaseConnectionSettings settings);

        string LoadPassword();

        bool SavePassword(DatabaseConnectionSettings settings, string password);
    }
}
