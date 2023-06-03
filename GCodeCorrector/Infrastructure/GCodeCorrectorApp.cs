using MugenMvvmToolkit;
using System;
using GCodeCorrector.ViewModels;

namespace GCodeCorrector.Infrastructure
{
    public class GCodeCorrectorApp : MvvmApplication
    {
        public override Type GetStartViewModelType()
        {
            return typeof(MainViewModel);
        }
    }
}