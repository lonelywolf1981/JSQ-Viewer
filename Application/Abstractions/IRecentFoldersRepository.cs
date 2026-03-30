using System.Collections.Generic;

namespace JSQViewer.Application.Abstractions
{
    public interface IRecentFoldersRepository
    {
        List<string> Load();

        bool Save(IList<string> folders);
    }
}
