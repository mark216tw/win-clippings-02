using Clippings.Core.Models;

namespace Clippings.Core.Services;

public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
