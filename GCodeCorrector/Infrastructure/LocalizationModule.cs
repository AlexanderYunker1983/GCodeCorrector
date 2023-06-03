using MugenMvvmToolkit;
using MugenMvvmToolkit.Interfaces;
using MugenMvvmToolkit.Interfaces.Models;
using YLocalization;

namespace GCodeCorrector.Infrastructure
{
    public class LocalizationModule : IModule
    {
        public bool Load(IModuleContext context)
        {
            var iocContainer = context.IocContainer;
            var localizationManager = iocContainer.Get<ILocalizationManager>();
            localizationManager.AddAssembly("GCodeCorrector");
            PlatformVariables.LocalizationManager = localizationManager;
            return true;
        }

        public void Unload(IModuleContext context)
        {

        }

        public int Priority => ApplicationSettings.ModulePriorityDefault - 1;
    }
}