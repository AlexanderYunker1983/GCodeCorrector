﻿using System;
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
        private double _endLineCount = 0.8;
        private double _startLineSize = 0.4;
        private double _startLineCount = 0.6;

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

        public double StartLineSize
        {
            get => _startLineSize;
            set
            {
                if (value.Equals(_startLineSize)) return;
                _startLineSize = value;
                OnPropertyChanged();
            }
        }

        public double StartLineCount
        {
            get => _startLineCount;
            set
            {
                if (value.Equals(_startLineCount)) return;
                _startLineCount = value;
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
            var sfd = new SaveFileDialog
            {
                Filter = "gcode|*.gcode",
                Title = _localizationManager.GetString("SaveFileTitle"),
                InitialDirectory = dir ?? string.Empty,
                FileName = $"new_{_selectedFileName}"
            };

            if (sfd.ShowDialog() != true)
            {
                return Empty.Task;
            }

            var newFile = sfd.FileName;
            var code = File.ReadAllLines(SelectedFile);

            var newCode = CorrectEndOfLines(code);
            var finalCode = CorrectStartOfLines(newCode.ToArray());

            File.WriteAllLines(newFile, finalCode);

            MessageBox.Show(_localizationManager.GetString("SuccessfullySaved"),
                _localizationManager.GetString("Saving"), MessageBoxButton.OK, MessageBoxImage.None);

            return Empty.Task;
        }

        private IEnumerable<string> CorrectStartOfLines(string[] code)
        {
            var newCode = new List<string>();
            var prevX = double.NegativeInfinity;
            var prevY = double.NegativeInfinity;

            for (var i = 0; i < code.Length; i++)
            {
                var line = code[i];
                if (!line.StartsWith("G1") || double.IsNegativeInfinity(prevX) || double.IsNegativeInfinity(prevY))
                {
                    newCode.Add(line);
                    if (line.StartsWith("G92") || line.StartsWith("G0") || line.StartsWith("G1"))
                    {
                        ParsePrevData(line, ref prevX, ref prevY);
                    }

                    continue;
                }

                var parts = line.Split(' ');
                if (parts.Any(p => p.StartsWith("Z")))
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY);
                    continue;
                }

                var xPart = parts.FirstOrDefault(p => p.StartsWith("X"));
                var yPart = parts.FirstOrDefault(p => p.StartsWith("Y"));
                var ePart = parts.FirstOrDefault(p => p.StartsWith("E"));

                var x = xPart != null ? double.Parse(xPart.Substring(1).TrimEnd(';')) : prevX;
                var y = yPart != null ? double.Parse(yPart.Substring(1).TrimEnd(';')) : prevY;
                var e = ePart != null ? double.Parse(ePart.Substring(1).TrimEnd(';')) : 0.0;

                if (e < 0)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY);
                    continue;
                }

                var deltaX = x - prevX;
                var deltaY = y - prevY;
                var delta = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                if (delta < StartLineSize)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY);
                    continue;
                }

                if (i < 1)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY);
                    continue;
                }

                var counter = -1;
                var codeFounded = false;
                var prevCommand = string.Empty;

                while (counter + i >= 0)
                {
                    prevCommand = code[i + counter];
                    if (!(prevCommand.StartsWith("G") || prevCommand.StartsWith("M")))
                    {
                        counter--;
                        continue;
                    }

                    if (prevCommand.StartsWith("G1"))
                    {
                        codeFounded = true;
                    }

                    break;
                }

                if (!codeFounded)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY);
                    continue;
                }

                var partsToRem = prevCommand.Split(' ');
                var eRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("E"));

                if (eRemPart == null)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY);
                    continue;
                }

                var devider = (delta - StartLineSize) / delta;
                var newDeltaX = deltaX * devider;
                var newDeltaY = deltaY * devider;
                var startX = deltaX - newDeltaX;
                var startY = deltaY - newDeltaY;
                var newExtrusion = e * devider;
                var startExtrusion = (e - newExtrusion) * StartLineCount;

                var startCode = $"G1 X{prevX + startX} Y{prevY + startY} E{startExtrusion}";
                newCode.Add(startCode);

                var cuttedLine = $"G1 X{prevX + newDeltaX + startX} Y{prevY + newDeltaY + startY} E{newExtrusion}";
                newCode.Add(cuttedLine);

                

                ParsePrevData(cuttedLine, ref prevX, ref prevY);
            }

            return newCode;
        }

        private IEnumerable<string> CorrectEndOfLines(string[] code)
        {
            var newCode = new List<string>();

            var prevX = double.NegativeInfinity;
            var prevY = double.NegativeInfinity;

            for (var i = 0; i < code.Length; i++)
            {
                var line = code[i];
                if (!line.StartsWith("G1") || double.IsNegativeInfinity(prevX) || double.IsNegativeInfinity(prevY))
                {
                    newCode.Add(line);
                    if (line.StartsWith("G92") || line.StartsWith("G0") || line.StartsWith("G1"))
                    {
                        ParsePrevData(line, ref prevX, ref prevY);
                    }

                    continue;
                }

                var parts = line.Split(' ');
                if (parts.Any(p => p.StartsWith("Z")))
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY);
                    continue;
                }

                var xPart = parts.FirstOrDefault(p => p.StartsWith("X"));
                var yPart = parts.FirstOrDefault(p => p.StartsWith("Y"));
                var ePart = parts.FirstOrDefault(p => p.StartsWith("E"));

                var x = xPart != null ? double.Parse(xPart.Substring(1).TrimEnd(';')) : prevX;
                var y = yPart != null ? double.Parse(yPart.Substring(1).TrimEnd(';')) : prevY;
                var e = ePart != null ? double.Parse(ePart.Substring(1).TrimEnd(';')) : 0.0;

                if (e < 0)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY);
                    continue;
                }

                var deltaX = x - prevX;
                var deltaY = y - prevY;
                var delta = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                if (delta < EndLineSize)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY);
                    continue;
                }

                if (i >= code.Length - 1)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY);
                    continue;
                }

                var counter = 1;
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
                    ParsePrevData(line, ref prevX, ref prevY);
                    continue;
                }

                var partsToRem = nextCommand.Split(' ');
                var eRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("E"));

                if (eRemPart == null)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY);
                    continue;
                }

                var devider = (delta - EndLineSize) / delta;
                var newDeltaX = deltaX * devider;
                var newDeltaY = deltaY * devider;
                var endX = deltaX - newDeltaX;
                var endY = deltaY - newDeltaY;
                var newExtrusion = e * devider;
                var endExtrusion = (e - newExtrusion) * EndLineCount;

                var cuttedLine = $"G1 X{prevX + newDeltaX} Y{prevY + newDeltaY} E{newExtrusion}";
                newCode.Add(cuttedLine);

                var endCode = $"G1 X{prevX + newDeltaX + endX} Y{prevY + newDeltaY + endY} E{endExtrusion}";
                newCode.Add(endCode);

                ParsePrevData(endCode, ref prevX, ref prevY);
            }

            return newCode;
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

        private static void ParsePrevData(string line, ref double prevX, ref double prevY)
        {
            var partsToRem = line.Split(' ');
            var xRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("X"));
            if (xRemPart != null)
            {
                ParseStringPartWithCorrection(out prevX, xRemPart);
            }

            var yRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("Y"));
            if (yRemPart != null)
            {
                ParseStringPartWithCorrection(out prevY, yRemPart);
            }
        }

        private static void ParseStringPartWithCorrection(out double data, string part)
        {
            var substring = part.Substring(1).TrimEnd(';');
            if (substring.StartsWith("-"))
            {
                var sss = substring.Substring(1);

                if (sss.StartsWith("."))
                {
                    sss = $"0{sss}";
                }

                substring = $"-{sss}";
            }

            data = double.Parse(substring);
        }

        public ICommand OpenFileCommand { get; set; }
        public ICommand SaveFileCommand { get; set; }
        public string DisplayName { get; }

    }
}