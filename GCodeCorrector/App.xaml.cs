using GCodeCorrector.Properties;
using MugenMvvmToolkit.WPF.Infrastructure;
using MugenMvvmToolkit;
using MugenMvvmToolkit.Interfaces;
using MugenMvvmToolkit.Models;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using GCodeCorrector.Infrastructure;

namespace GCodeCorrector
{
    public partial class App
    {
        private readonly object[] _menuStructure = {
            new MenuWithSubItems("Settings",
                new[]
                {
                    MainMenuItems.ProgramSettings,
                }),
        };

        public App()
        {
            PlatformVariables.MenuStructure = _menuStructure;

            if (Settings.Default.IsNeedToMigrate)
            {
                Settings.Default.Upgrade();
                Settings.Default.IsNeedToMigrate = false;
                Settings.Default.Save();
            }

            var _ = new BootstrapperEx(this, new AutofacContainer());
        }
    }

    public class BootstrapperEx : Bootstrapper<GCodeCorrectorApp>
    {
        public BootstrapperEx(Application application, IIocContainer iocContainer,
            IEnumerable<Assembly> assemblies = null, PlatformInfo platform = null) : base(application, iocContainer,
            assemblies, platform)
        {
        }
    }
}
