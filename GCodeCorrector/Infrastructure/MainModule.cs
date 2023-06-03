using MugenMvvmToolkit;
using MugenMvvmToolkit.Binding;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.Models.IoC;
using System;
using System.Reflection;
using YLocalization;
using YMugenExtensions;
using IModule = MugenMvvmToolkit.Interfaces.IModule;

namespace GCodeCorrector.Infrastructure
{
    public class MainModule : IModule
    {
        public bool Load(IModuleContext context)
        {
            var resourceResolver = BindingServiceProvider.ResourceResolver;
            resourceResolver.AddType("DateTimeOffset", typeof(DateTimeOffset));

            var iocContainer = context.IocContainer;
            if (!iocContainer.CanResolve<ILocalizationManager>()) iocContainer.Bind<ILocalizationManager, MugenLocalizationManager>(DependencyLifecycle.SingleInstance);
           
            var version = Assembly.GetAssembly(GetType()).GetName().Version;
            PlatformVariables.ProgramVersion = version.Build == 0 ? $"{version.Major}.{version.Minor}" : $"{version.Major}.{version.Minor}.{version.Build}-Developer Version";
            
            return true;
        }

        public void Unload(IModuleContext context)
        {
        }

        public int Priority => ApplicationSettings.ModulePriorityDefault;
    }
}