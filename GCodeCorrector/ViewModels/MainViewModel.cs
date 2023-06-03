using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GCodeCorrector.Infrastructure;
using Microsoft.Win32;
using MugenMvvmToolkit;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;
using YMugenExtensions.Commands;
using File = System.IO.File;

namespace GCodeCorrector.ViewModels
{
    public class MainViewModel : ViewModelBase, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;
        private string _selectedFile;
        private string _selectedFileName;
        private double _endLineSize = 0.4;
        private double _endLineCount = 0.7;

        public MainViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            DisplayName = $"{_localizationManager.GetString("ProgramTitle")} v.{PlatformVariables.ProgramVersion}";
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            
            PrepareCommands();
        }

        public double EndLineSize
        {
            get => _endLineSize;
            set
            {
                if (value.Equals(_endLineSize)) return;
                _endLineSize = value;
                OnPropertyChanged();
            }
        }

        public double EndLineCount
        {
            get => _endLineCount;
            set
            {
                if (value.Equals(_endLineCount)) return;
                _endLineCount = value;
                OnPropertyChanged();
            }
        }

        private void PrepareCommands()
        {
            OpenFileCommand = new AsyncYRelayCommand(OnOpenFile);
            SaveFileCommand = new AsyncYRelayCommand(OnSaveFile, OnCanSaveFile, acceptedProperties: new []{ nameof(SelectedFile) }, notifiers: this);
        }

        private Task OnSaveFile()
        {
            var dir = Path.GetDirectoryName(SelectedFile);
            var newFile = string.Empty;
            var sfd = new SaveFileDialog
            {
                Filter = "gcode|*.gcode",
                Title = _localizationManager.GetString("SaveFileTitle"),
                InitialDirectory = dir,
                FileName = $"new_{_selectedFileName}"
            };

            if (sfd.ShowDialog() != true)
            {
                return Empty.Task;
            }

            newFile = sfd.FileName;

            var code = File.ReadAllLines(SelectedFile);
            var newCode = new List<string>();
            var prevX = double.NegativeInfinity;
            var prevY = double.NegativeInfinity;
            var prevE = double.NegativeInfinity;
            for (var i = 0; i < code.Length; i++)
            {
                var line = code[i];
                if (!line.StartsWith("G1") || double.IsNegativeInfinity(prevX) || double.IsNegativeInfinity(prevY) || double.IsNegativeInfinity(prevE))
                {
                    newCode.Add(line);
                    if (line.StartsWith("G92") || line.StartsWith("G0") || line.StartsWith("G1"))
                    {
                        ParsePrevData(line, ref prevX, ref prevY, ref prevE);
                    }

                    continue;
                }

                var parts = line.Split(' ');
                var x = double.NegativeInfinity;
                var y = double.NegativeInfinity;
                var e = double.NegativeInfinity;
                if (parts.Any(p => p.StartsWith("Z")))
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE);
                    continue;
                }

                var xPart = parts.FirstOrDefault(p => p.StartsWith("X"));
                var yPart = parts.FirstOrDefault(p => p.StartsWith("Y"));
                var ePart = parts.FirstOrDefault(p => p.StartsWith("E"));

                var xExist = false;
                var yExist = false;

                if (xPart != null)
                {
                    x = double.Parse(xPart.Substring(1).TrimEnd(';'));
                }
                else
                {
                    x = prevX;
                }
                if (yPart != null)
                {
                    y = double.Parse(yPart.Substring(1).TrimEnd(';'));
                }
                else
                {
                    y = prevY;
                }

                if (ePart != null)
                {
                    e = double.Parse(ePart.Substring(1).TrimEnd(';'));
                }

                if (e < 0)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE);
                    continue;
                }
                var deltaX = x - prevX;
                var deltaY = y - prevY;
                var extrusion = e - prevE;
                var delta = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                if (delta < EndLineSize)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE);
                    continue;
                }

                if (i >= code.Length)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE);
                    continue;
                }

                var counter = 0;
                var codeFounded = false;
                var nextCommand = string.Empty;
                while (counter + i < code.Length)
                {
                    nextCommand = code[i + counter];
                    if (!(nextCommand.StartsWith("G") || nextCommand.StartsWith("M")))
                    {
                        counter++;
                        continue;
                    }

                    if (nextCommand.StartsWith("G1"))
                    {
                        codeFounded = true;
                    }

                    break;

                }

                if (!codeFounded)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE);
                    continue;
                }

                var partsToRem = nextCommand.Split(' ');
                var eRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("E"));

                if (eRemPart == null)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE);
                    continue;
                }

                var devider = (delta - EndLineSize) / delta;
                var newDeltaX = deltaX * devider;
                var newDeltaY = deltaY * devider;
                var endX = deltaX - newDeltaX;
                var endY = deltaY - newDeltaY;
                var newExtrusion = extrusion * devider;
                var endExtrusion = (extrusion - newExtrusion) * EndLineCount;

                var cuttedLine = $"G1 X{prevX + newDeltaX} Y{prevY + newDeltaY} E{prevE + newExtrusion}";
                newCode.Add(cuttedLine);

                var endCode = $"G1 X{prevX + newDeltaX + endX} Y{prevY + newDeltaY + endY} E{endExtrusion}";
                newCode.Add(endCode);

                ParsePrevData(endCode, ref prevX, ref prevY, ref prevE);
            }

            File.WriteAllLines(newFile, newCode);

            MessageBox.Show(_localizationManager.GetString("SuccessfullySaved"),
                _localizationManager.GetString("Saving"), MessageBoxButton.OK, MessageBoxImage.None);

            return Empty.Task;
        }

        private bool OnCanSaveFile()
        {
            return !string.IsNullOrEmpty(SelectedFile);
        }

        public string SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (value == _selectedFile) return;
                _selectedFile = value;
                OnPropertyChanged();
            }
        }

        private Task OnOpenFile()
        {
            SelectedFile = string.Empty;
            _selectedFileName = string.Empty;
            var ofd = new OpenFileDialog
            {
                CheckFileExists = true,
                Filter = "gcode|*.gcode",
                Title = _localizationManager.GetString("OpenFileTitle")
            };
            if (ofd.ShowDialog() == true)
            {
                SelectedFile = ofd.FileName;
                _selectedFileName = ofd.SafeFileName;
            }
            
            return Empty.Task;
        }

        private static void ParsePrevData(string line, ref double prevX, ref double prevY, ref double prevE)
        {
            var partsToRem = line.Split(' ');
            var xRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("X"));
            var yRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("Y"));
            var eRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("E"));
            if (xRemPart != null)
            {
                var substring = xRemPart.Substring(1).TrimEnd(';');
                if (substring.StartsWith("-"))
                {
                    var sss = substring.Substring(1);

                    if (sss.StartsWith("."))
                    {
                        sss = $"0{sss}";
                    }

                    substring = $"-{sss}";
                }
                prevX = double.Parse(substring);
            }

            if (yRemPart != null)
            {
                var substring = yRemPart.Substring(1).TrimEnd(';');
                if (substring.StartsWith("-"))
                {
                    var sss = substring.Substring(1);

                    if (sss.StartsWith("."))
                    {
                        sss = $"0{sss}";
                    }

                    substring = $"-{sss}";
                }
                prevY = double.Parse(substring);
            }

            if (eRemPart != null)
            {
                var substring = eRemPart.Substring(1).TrimEnd(';');
                if (substring.StartsWith("-"))
                {
                    var sss = substring.Substring(1);
                    
                    if (sss.StartsWith("."))
                    {
                        sss = $"0{sss}";
                    }

                    substring = $"-{sss}";
                }
                prevE = double.Parse(substring);
            }

            prevE = 0;
        }

        public ICommand OpenFileCommand { get; set; }
        public ICommand SaveFileCommand { get; set; }
        public string DisplayName { get; }

    }
}