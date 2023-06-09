using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using GCodeCorrector.Infrastructure;
using Microsoft.Win32;
using MugenMvvmToolkit;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;
using YMugenExtensions.Commands;
using static System.Windows.Forms.AxHost;
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
        private bool _startLineEnabled = true;
        private bool _endLineEnabled = true;
        private bool _useRelativeExtrusion;

        public MainViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            DisplayName = $"{_localizationManager.GetString("ProgramTitle")} v.{PlatformVariables.ProgramVersion}";
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            
            PrepareCommands();
        }

        public bool EndLineEnabled
        {
            get => _endLineEnabled;
            set
            {
                if (value == _endLineEnabled) return;
                _endLineEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool StartLineEnabled
        {
            get => _startLineEnabled;
            set
            {
                if (value == _startLineEnabled) return;
                _startLineEnabled = value;
                OnPropertyChanged();
            }
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

        private async Task OnSaveFile()
        {
            ShowProgress = StartLineEnabled;
            Progress = 0;
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
                return;
            }
            var newFile = sfd.FileName;

            var code = await ReadFileAndCorrect(sfd);

            try
            {
                File.WriteAllLines(newFile, code);
                MessageBox.Show(_localizationManager.GetString("SuccessfullySaved"),
                    _localizationManager.GetString("Saving"), MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (Exception e)
            {
                MessageBox.Show(_localizationManager.GetString("ErrorDuringRecordingToFile"),
                    _localizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine(e);
            }

            Progress = 100;
            ShowProgress = false;
        }

        private string[] tempCode;
        private int _progress;
        private bool _showProgress;
        private bool _findOtherLines;

        private async Task<string[]> ReadFileAndCorrect(SaveFileDialog sfd)
        {
            var code = File.ReadAllLines(SelectedFile);

            _useRelativeExtrusion = FindRelativeExtrusion(code);
            
            if (EndLineEnabled)
            {
                code = CorrectEndOfLines(code.ToArray()).ToArray();
            }
            
            if (StartLineEnabled)
            {
                tempCode = code.ToArray();
                await Task.Run(() =>
                {
                    tempCode = CorrectStartOfLines(tempCode.ToArray()).ToArray();
                });
                code = tempCode.ToArray();
            }

            return code;
        }

        private bool FindRelativeExtrusion(string[] code)
        {
            var result = false;
            foreach (var codeString in code)
            {
                var s = codeString.Trim(' ');
                if (s.StartsWith(";")) continue;
                s = s.Trim(';');
                if (s.StartsWith("M83"))
                {
                    result = true;
                    break;
                }
            }
            return result;
        }

        private void FindParam(string[] code, int index, ref double x, ref double y, ref double e,
            bool useRelativeExtrusion)
        {
            var prevX = double.NegativeInfinity;
            var prevY = double.NegativeInfinity;
            var prevE = double.NegativeInfinity;
            for (var i = 0; i < index; i++)
            {
                var line = code[i].Trim(' ');
                if (!line.StartsWith("G1") || double.IsNegativeInfinity(prevX) || double.IsNegativeInfinity(prevY))
                {
                    if (line.StartsWith("G92") || line.StartsWith("G0") || line.StartsWith("G1"))
                    {
                        ParsePrevData(line, ref prevX, ref prevY, ref prevE, useRelativeExtrusion);
                    }

                    continue;
                }
                ParsePrevData(line, ref prevX, ref prevY, ref prevE, useRelativeExtrusion);
            }
            x = prevX; 
            y = prevY; 
            e = prevE;
        }

        public int Progress
        {
            get => _progress;
            set
            {
                if (value == _progress) return;
                _progress = value;
                OnPropertyChanged();
            }
        }

        public bool ShowProgress
        {
            get => _showProgress;
            set
            {
                if (value == _showProgress) return;
                _showProgress = value;
                OnPropertyChanged();
            }
        }

        public bool FindOtherLines
        {
            get => _findOtherLines;
            set
            {
                if (value == _findOtherLines) return;
                _findOtherLines = value;
                OnPropertyChanged();
            }
        }

        private IEnumerable<string> CorrectStartOfLines(string[] code)
        {
            var newCode = new List<string>();

            var prevX = double.NegativeInfinity;
            var prevY = double.NegativeInfinity;
            var prevE = double.NegativeInfinity;

            for (var i = 0; i < code.Length; i++)
            {
                Progress = (int)(i * 100.0 / code.Length);
                var line = code[i].Trim(' ');
                if (!line.StartsWith("G1") || double.IsNegativeInfinity(prevX) || double.IsNegativeInfinity(prevY))
                {
                    newCode.Add(line);
                    if (line.StartsWith("G92") || line.StartsWith("G0") || line.StartsWith("G1"))
                    {
                        ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                    }

                    continue;
                }

                var parts = line.Split(' ');
                if (parts.Any(p => p.StartsWith("Z")))
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                    continue;
                }

                var x = prevX;
                var y = prevY;
                var e = prevE;

                ParsePrevData(line, ref x, ref y, ref e, false);

                var extrusion = e - prevE;

                if (extrusion <= 0)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                    continue;
                }

                var deltaX = x - prevX;
                var deltaY = y - prevY;
                var delta = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                if (delta <= StartLineSize)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                    continue;
                }

                var angle = 90.0;
                if (FindOtherLines)
                {
                    if (i < 1)
                    {
                        newCode.Add(line);
                        ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                        continue;
                    }

                    var counter = -1;
                    var codeFounded = false;
                    var nextCommand = string.Empty;

                    while (counter + i >= 0)
                    {
                        nextCommand = code[i + counter].Trim(' ');
                        if (!nextCommand.StartsWith("G"))
                        {
                            counter--;
                            continue;
                        }

                        if (nextCommand.StartsWith("G1"))
                        {
                            var nextParts = nextCommand.Split(' ');
                            if (nextParts.Any(p => p.StartsWith("E")))
                            {
                                codeFounded = true;
                            }
                        }

                        break;
                    }

                    if (!codeFounded)
                    {
                        newCode.Add(line);
                        ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                        continue;
                    }

                    var newX = x;
                    var newY = y;
                    var newE = e;
                    var newEndX = x;
                    var newEndY = y;
                    var newEndE = e;
                    FindParam(code, i + counter + 1, ref newEndX, ref newEndY, ref newEndE, _useRelativeExtrusion);
                    FindParam(code, i + counter, ref newX, ref newY, ref newE, _useRelativeExtrusion);


                    if (newEndE - newE <= 0)
                    {
                        newCode.Add(line);
                        ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                        continue;
                    }

                    var nextDeltaX = newEndX - newX;
                    var nextDeltaY = newEndY - newY;
                    var nextDelta = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    if (nextDelta <= 0)
                    {
                        newCode.Add(line);
                        ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                        continue;
                    }

                    var sin = deltaX * nextDeltaY - nextDeltaX * deltaY;
                    var cos = deltaX * nextDeltaX + deltaY * nextDeltaY;
                    angle = Math.Atan2(sin, cos) * (180 / Math.PI);
                    if (angle < 0)
                    {
                        angle += 180;
                    }

                    if (angle > 180)
                    {
                        angle -= 180;
                    }
                }
                
                var devider = (delta - EndLineSize) / delta;
                if (devider <= 0)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                    continue;
                }

                var newDeltaX = deltaX * devider;
                var newDeltaY = deltaY * devider;
                var startX = deltaX - newDeltaX;
                var startY = deltaY - newDeltaY;
                var newExtrusion = extrusion * devider;
                var oldExtrusion = extrusion - newExtrusion;
                var endExtrusion = (extrusion - newExtrusion) * (1 - (1 - StartLineCount) * Math.Sin(angle / (180 / Math.PI)));
                if (endExtrusion > oldExtrusion)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                    continue;
                }

                var startCode = $"G1 X{prevX + startX} Y{prevY + startY} E{prevE + endExtrusion}";
                newCode.Add(startCode);
                if (!_useRelativeExtrusion)
                {
                    var deltaCode = $"G92 E{prevE + oldExtrusion}";
                    newCode.Add(deltaCode);
                }
                var endCode = $"G1 X{x} Y{y} E{e}";
                newCode.Add(endCode);
                
                prevX = x;
                prevY = y;
                prevE = _useRelativeExtrusion ? 0.0 : e;
            }

            return newCode;
        }

        private IEnumerable<string> CorrectEndOfLines(string[] code)
        {
            var newCode = new List<string>();

            var prevX = double.NegativeInfinity;
            var prevY = double.NegativeInfinity;
            var prevE = double.NegativeInfinity;

            for (var i = 0; i < code.Length; i++)
            {
                var line = code[i].Trim(' ');
                if (!line.StartsWith("G1") || double.IsNegativeInfinity(prevX) || double.IsNegativeInfinity(prevY))
                {
                    newCode.Add(line);
                    if (line.StartsWith("G92") || line.StartsWith("G0") || line.StartsWith("G1"))
                    {
                        ParsePrevData(line, ref prevX, ref prevY, ref  prevE, _useRelativeExtrusion);
                    }

                    continue;
                }

                var parts = line.Split(' ');
                if (parts.Any(p => p.StartsWith("Z")))
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                    continue;
                }

                var x = prevX;
                var y = prevY;
                var e = prevE;

                ParsePrevData(line, ref x, ref y, ref e, false);
                
                var extrusion = e - prevE;

                if (extrusion <= 0)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                    continue;
                }

                var deltaX = x - prevX;
                var deltaY = y - prevY;
                var delta = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                if (delta <= EndLineSize)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                    continue;
                }

                var angle = 90.0;
                if (FindOtherLines)
                {
                    if (i >= code.Length - 1)
                    {
                        newCode.Add(line);
                        ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                        continue;
                    }

                    var counter = 1;
                    var codeFounded = false;
                    var nextCommand = string.Empty;

                    while (counter + i < code.Length)
                    {
                        nextCommand = code[i + counter].Trim(' ');
                        if (!nextCommand.StartsWith("G"))
                        {
                            counter++;
                            continue;
                        }

                        if (nextCommand.StartsWith("G1"))
                        {
                            var nextParts = nextCommand.Split(' ');
                            if (nextParts.Any(p => p.StartsWith("E")))
                            {
                                codeFounded = true;
                            }
                        }

                        break;
                    }

                    if (!codeFounded)
                    {
                        newCode.Add(line);
                        ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                        continue;
                    }

                    var newX = x;
                    var newY = y;
                    var newE = e;

                    ParsePrevData(nextCommand, ref newX, ref newY, ref newE, false);

                    if (newE - e <= 0)
                    {
                        newCode.Add(line);
                        ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                        continue;
                    }

                    var nextDeltaX = newX - x;
                    var nextDeltaY = newY - y;
                    var nextDelta = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    if (nextDelta <= 0)
                    {
                        newCode.Add(line);
                        ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                        continue;
                    }

                    var sin = deltaX * nextDeltaY - nextDeltaX * deltaY;
                    var cos = deltaX * nextDeltaX + deltaY * nextDeltaY;
                    angle = Math.Atan2(sin, cos) * (180 / Math.PI);
                    if (angle < 0)
                    {
                        angle += 180;
                    }

                    if (angle > 180)
                    {
                        angle -= 180;
                    }
                }

                var devider = (delta - EndLineSize) / delta;
                if (devider <= 0)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                    continue;
                }
                var newDeltaX = deltaX * devider;
                var newDeltaY = deltaY * devider;

                var newExtrusion = extrusion * devider;
                var oldExtrusion = extrusion - newExtrusion;
                var endExtrusion = (extrusion - newExtrusion) * (1 - (1 - EndLineCount) * Math.Sin(angle / (180 / Math.PI)));
                if (endExtrusion > oldExtrusion)
                {
                    newCode.Add(line);
                    ParsePrevData(line, ref prevX, ref prevY, ref prevE, _useRelativeExtrusion);
                    continue;
                }
                var cuttedLine = $"G1 X{prevX + newDeltaX} Y{prevY + newDeltaY} E{prevE + newExtrusion}";
                newCode.Add(cuttedLine);

                var endCode = $"G1 X{x} Y{y} E{prevE + (_useRelativeExtrusion ? 0.0 : newExtrusion) + endExtrusion}";
                newCode.Add(endCode);
                if (!_useRelativeExtrusion)
                {
                    var finalCode = $"G92 E{e}";
                    newCode.Add(finalCode);
                }

                prevX = x;
                prevY = y;
                prevE = _useRelativeExtrusion ? 0.0 : e;
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

        private static void ParsePrevData(string line, ref double prevX, ref double prevY, ref double prevE, bool useRelativeExtrusion)
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

            var eRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("E"));
            if (eRemPart != null)
            {
                ParseStringPartWithCorrection(out prevE, eRemPart);
            }

            if (useRelativeExtrusion)
            {
                prevE = 0.0;
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