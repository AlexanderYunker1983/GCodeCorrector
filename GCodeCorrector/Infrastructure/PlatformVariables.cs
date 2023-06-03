using System;
using MugenMvvmToolkit.Models;
using MugenMvvmToolkit;
using YLocalization;

namespace GCodeCorrector.Infrastructure
{
    public class PlatformVariables
    {
        public static ILocalizationManager LocalizationManager { get; set; }
        public static string ProgramVersion { get; set; }
        public static bool IsWpfPlatform => ServiceProvider.Application.PlatformInfo.Platform == PlatformType.WPF;
        public static object[] MenuStructure { get; set; } = Array.Empty<object>();
    }
}