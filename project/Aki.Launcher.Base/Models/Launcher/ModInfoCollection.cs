using Aki.Launcher.Models.Aki;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Aki.Launcher.Models.Launcher
{
    public class ModInfoCollection : INotifyPropertyChanged
    {
        private int _serverModsCount;
        public int ServerModsCount
        {
            get => _serverModsCount;
            set
            {
                if (_serverModsCount != value)
                {
                    _serverModsCount = value;
                    RaisePropertyChanged(nameof(ServerModsCount));
                }
            }
        }
        
        private int _profileModsCount;
        public int ProfileModsCount
        {
            get => _profileModsCount;
            set
            {
                if (_profileModsCount != value)
                {
                    _profileModsCount = value;
                    RaisePropertyChanged(nameof(ProfileModsCount));
                }
            }
        }

        private bool _hasMods;
        public bool HasMods
        {
            get => _hasMods;
            set
            {
                if (_hasMods != value)
                {
                    _hasMods = value;
                    RaisePropertyChanged(nameof(HasMods));
                }
            }
        }

        public ObservableCollection<AkiMod> ActiveMods { get; private set; } = new ObservableCollection<AkiMod>();
        public ObservableCollection<AkiMod> InactiveMods { get; private set; } = new ObservableCollection<AkiMod>();

        public ModInfoCollection()
        {
            var serverMods = ServerManager.GetLoadedServerMods().Values.ToList();
            var profileMods = ServerManager.GetProfileMods().ToList();

            ServerModsCount = serverMods?.Count() ?? 0;
            ProfileModsCount = profileMods?.Count() ?? 0;

            foreach (var activeMod in serverMods)
            {
                activeMod.InServer = true;
                ActiveMods.Add(activeMod);
            }

            foreach (var inactiveMod in profileMods)
            {
                var existingMod = ActiveMods.Where(x => x.Name == inactiveMod.Name && x.Version == inactiveMod.Version && x.Author == inactiveMod.Author).FirstOrDefault();

                if (existingMod != null)
                {
                    existingMod.InProfile = true;
                    continue;
                }

                inactiveMod.InProfile = true;
                InactiveMods.Add(inactiveMod);
            }

            HasMods = ActiveMods.Count > 0 || InactiveMods.Count > 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void RaisePropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
