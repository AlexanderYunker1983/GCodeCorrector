using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GCodeCorrector.Infrastructure;
using GCodeCorrector.Models;
using Microsoft.Win32;
using MugenMvvmToolkit;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;
using YMugenExtensions.Commands;
using File = System.IO.File;
using State = GCodeCorrector.Models.State;

namespace GCodeCorrector.ViewModels
{
    public class MainViewModel : ViewModelBase, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;
        private string _selectedFile;
        private string _selectedFileName;
        private double _endLineSize = 0.6;
        private double _endLineCount = 0.7;
        private double _startLineSize = 0.5;
        private double _startLineCount = 1.5;
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

            var data = ReadAndParseFile().ToList();
            
            CorrectLines(data);

            var code = FormCodeFromData(data);
            
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
        }

        private string[] FormCodeFromData(IEnumerable<GCodeLine> data)
        {
            var result = new List<string>();

            foreach (var gCodeLine in data)
            {
                if (gCodeLine.ModifiedLines == null)
                {
                    result.Add(gCodeLine.OriginalCode);
                    continue;
                }

                foreach (var modifiedLine in gCodeLine.ModifiedLines)
                {
                    result.Add(modifiedLine.OriginalCode);
                }
            }

            return result.ToArray();
        }

        private void CorrectLines(IEnumerable<GCodeLine> data)
        {
            if (!(StartLineEnabled || EndLineEnabled))
            {
                return;
            }
            var dataWithCode = data.Where(d => d.IsCode).ToList();
            foreach (var gCodeLine in dataWithCode)
            {
                var prevCode = dataWithCode.LastOrDefault(d => d.LineNumber < gCodeLine.LineNumber);
                var nextCode = dataWithCode.FirstOrDefault(d => d.LineNumber > gCodeLine.LineNumber);
                gCodeLine.PrevCode = prevCode;
                gCodeLine.NextCode = nextCode;
                gCodeLine.PrevCodeHasExtrusion = prevCode != null && prevCode.HasExtrusion && prevCode.DeltaE > 0;
                gCodeLine.NextCodeHasExtrusion = nextCode != null && nextCode.HasExtrusion && nextCode.DeltaE > 0;
            }

            var dataWithExtrusion = dataWithCode.Where(d => d.HasExtrusion && d.DeltaE > 0).ToList();
            var dataWithExtrusionForCorrection = dataWithExtrusion.Where(d => 
                    (d.NextCodeHasExtrusion && EndLineEnabled ||
                     d.PrevCodeHasExtrusion && StartLineEnabled) &&
                    d.Length > (EndLineEnabled ? EndLineSize : 0.0) + (StartLineEnabled ? StartLineSize : 0.0) &&
                    Math.Abs(d.DeltaZ) < float.Epsilon)
                .ToList();
            foreach (var gCodeLine in dataWithExtrusionForCorrection)
            {
                var startAngle = FindAngleInDegreesWithCorrection(gCodeLine, gCodeLine.PrevCode);
                var endAngle = FindAngleInDegreesWithCorrection(gCodeLine, gCodeLine.NextCode);

                var relativeE = gCodeLine.DeltaE / gCodeLine.Length;

                var startLength = StartLineEnabled ? StartLineSize : 0.0;
                var endLength = EndLineEnabled ? EndLineSize : 0.0;

                var startPart = startLength / gCodeLine.Length;
                var endPart = endLength / gCodeLine.Length;

                var startExtrusionInFile = startLength * relativeE;
                var endExtrusionInFile = endLength * relativeE;
                var mainExtrusion = gCodeLine.DeltaE - startExtrusionInFile - endExtrusionInFile;

                var startExtrusion = startExtrusionInFile * (1 - (1 - StartLineCount) * Math.Sin(startAngle / (180 / Math.PI)));
                var endExtrusion = endExtrusionInFile * (1 - (1 - StartLineCount) * Math.Sin(endAngle / (180 / Math.PI)));

                gCodeLine.ModifiedLines = new List<GCodeLine>();

                var prevState = gCodeLine.StartState;

                if (StartLineEnabled)
                {
                    var startLine = new GCodeLine
                    {
                        HasExtrusion = true,
                        IsCode = true,
                        StartState = prevState
                    };
                    var endState = new State(startLine.StartState);
                    endState.X += gCodeLine.DeltaX * startPart;
                    endState.Y += gCodeLine.DeltaY * startPart;
                    endState.E = _useRelativeExtrusion ? startExtrusion : startExtrusion + startLine.StartState.E;
                    startLine.EndState = endState;

                    gCodeLine.ModifiedLines.Add(startLine);
                    prevState = endState;
                    if (!_useRelativeExtrusion)
                    {
                        prevState.E = startLine.StartState.E + startExtrusionInFile;
                        gCodeLine.ModifiedLines.Add(new GCodeLine
                        {
                            OriginalCode = $"G92 E{prevState.E}"
                        });
                    }

                }

                var mainLine = new GCodeLine
                {
                    HasExtrusion = true,
                    IsCode = true,
                    StartState = prevState
                };
                var mainEndState = new State(mainLine.StartState);
                mainEndState.X += gCodeLine.DeltaX * (1 - startPart - endPart);
                mainEndState.Y += gCodeLine.DeltaY * (1 - startPart - endPart);
                mainEndState.E = _useRelativeExtrusion ? mainExtrusion : mainExtrusion + mainLine.StartState.E;
                mainLine.EndState = mainEndState;

                gCodeLine.ModifiedLines.Add(mainLine);
                prevState = mainEndState;
                if (!_useRelativeExtrusion)
                {
                    prevState.E = mainLine.StartState.E + mainExtrusion;
                    gCodeLine.ModifiedLines.Add(new GCodeLine
                    {
                        OriginalCode = $"G92 E{prevState.E}"
                    });
                }

                if (EndLineEnabled)
                {
                    var endLine = new GCodeLine
                    {
                        HasExtrusion = true,
                        IsCode = true,
                        StartState = prevState
                    };
                    var endState = new State(endLine.StartState);
                    endState.X += gCodeLine.DeltaX * endPart;
                    endState.Y += gCodeLine.DeltaY * endPart;
                    endState.E = _useRelativeExtrusion ? endExtrusion : endExtrusion + endLine.StartState.E;
                    endLine.EndState = endState;

                    gCodeLine.ModifiedLines.Add(endLine);
                    prevState = endState;
                    if (!_useRelativeExtrusion)
                    {
                        prevState.E = endLine.StartState.E + startExtrusionInFile;
                        gCodeLine.ModifiedLines.Add(new GCodeLine
                        {
                            OriginalCode = $"G92 E{prevState.E}"
                        });
                    }
                }
            }

            foreach (var gCodeLine in dataWithExtrusionForCorrection.Where(d => d.ModifiedLines != null))
            {
                foreach (var codeLine in gCodeLine.ModifiedLines.Where(ml => string.IsNullOrEmpty(ml.OriginalCode)))
                {
                    codeLine.OriginalCode = FormCodeLine(codeLine);
                }
            }
        }

        private string FormCodeLine(GCodeLine codeLine)
        {
            var sb = new StringBuilder();
            sb.Append("G1 ");
            if (Math.Abs(codeLine.DeltaX) > float.Epsilon)
            {
                sb.Append($"X{codeLine.EndState.X:F5} ");
            }
            if (Math.Abs(codeLine.DeltaY) > float.Epsilon)
            {
                sb.Append($"Y{codeLine.EndState.Y:F5} ");
            }
            if (Math.Abs(codeLine.DeltaZ) > float.Epsilon)
            {
                sb.Append($"Z{codeLine.EndState.Z:F5} ");
            }
            if (Math.Abs(codeLine.DeltaE) > float.Epsilon)
            {
                sb.Append($"E{codeLine.EndState.E:F7} ");
            }
            return sb.ToString();
        }

        private double FindAngleInDegreesWithCorrection(GCodeLine gCodeLine, GCodeLine prevCode)
        {
            if (prevCode == null || !prevCode.HasExtrusion) return 0.0;
            var sin = gCodeLine.DeltaX * prevCode.DeltaY - prevCode.DeltaX * gCodeLine.DeltaY;
            var cos = gCodeLine.DeltaX * prevCode.DeltaX + gCodeLine.DeltaY * prevCode.DeltaY;
            var angle = Math.Atan2(sin, cos) * (180 / Math.PI);
            if (angle < 0)
            {
                angle += 180;
            }

            if (angle > 180)
            {
                angle -= 180;
            }

            return angle;
        }

        private IEnumerable<GCodeLine> ReadAndParseFile()
        {
            var result = new List<GCodeLine>();
            var absolutePositing = true;
            var code = File.ReadAllLines(SelectedFile);

            _useRelativeExtrusion = FindRelativeExtrusion(code);

            var prevState = new State();
            for (var index = 0; index < code.Length; index++)
            {
                var codeLine = code[index];
                var gCodeLine = new GCodeLine
                {
                    OriginalCode = codeLine,
                    LineNumber = (ulong)index,
                    StartState = prevState,
                    EndState = prevState
                };
                
                var line = codeLine.Trim(' ');
                
                if (!(line.StartsWith("G") || line.StartsWith("M")))
                {
                    result.Add(gCodeLine);
                    continue;
                }

                gCodeLine.IsCode = true;
                if (line.StartsWith("G90") || line.StartsWith("G91"))
                {
                    absolutePositing = line.StartsWith("G90");
                    result.Add(gCodeLine);
                    continue;
                }

                if (line.StartsWith("M") || !(line.StartsWith("G0") || line.StartsWith("G1") || line.StartsWith("G2") || line.StartsWith("G3") || line.StartsWith("G92")))
                {
                    result.Add(gCodeLine);
                    continue;
                }

                gCodeLine.HasExtrusion = true;
                
                gCodeLine.EndState = ParseLine(line, prevState, absolutePositing);
                prevState = gCodeLine.EndState;
                if (_useRelativeExtrusion)
                {
                    prevState.E = 0.0;
                }

                if (line.StartsWith("G0"))
                {
                    gCodeLine.HasExtrusion = false;
                    result.Add(gCodeLine);
                    continue;
                }

                if (!(line.StartsWith("G92") || line.StartsWith("G0") || line.StartsWith("G1") || line.StartsWith("G2") || line.StartsWith("G3")))
                {
                    gCodeLine.HasExtrusion = false;
                    result.Add(gCodeLine);
                    continue;
                }

                result.Add(gCodeLine);
            }
            return result;
        }

        private State ParseLine(string line, State prevState, bool absolutePositing)
        {
            var result = new State(prevState);
            var partsToRem = line.Split(' ');
            var xRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("X"));
            if (xRemPart != null)
            {
                ParseStringPartWithCorrection(out var resultX, xRemPart);
                result.X = absolutePositing ? resultX : result.X;
            }

            var yRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("Y"));
            if (yRemPart != null)
            {
                ParseStringPartWithCorrection(out var resultY, yRemPart);
                result.Y = absolutePositing ? resultY : result.Y;
            }

            var zRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("Z"));
            if (zRemPart != null)
            {
                ParseStringPartWithCorrection(out var resultZ, zRemPart);
                result.Z = absolutePositing ? resultZ : result.Z;
            }

            var eRemPart = partsToRem.FirstOrDefault(p => p.StartsWith("E"));
            if (eRemPart != null)
            {
                ParseStringPartWithCorrection(out var resultE, eRemPart);
                result.E = absolutePositing ? resultE : result.E;
            }

            return result;
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