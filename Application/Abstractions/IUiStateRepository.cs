namespace JSQViewer.Application.Abstractions
{
    public interface IUiStateRepository
    {
        UiStateModel Load();

        bool Save(UiStateModel state);
    }
}
