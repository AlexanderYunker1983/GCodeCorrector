using System;
using System.Collections.Generic;

namespace GCodeCorrector.Models
{
    public class GCodeLine
    {
        public ulong LineNumber { get; set; }
        public string OriginalCode { get; set; }
        public bool IsCode { get; set; }
        public bool HasExtrusion { get; set; }
        public State StartState { get; set; }
        public State EndState { get; set; }
        public bool PrevCodeHasExtrusion { get; set; }
        public bool NextCodeHasExtrusion { get; set; }

        public GCodeLine PrevCode { get; set; }
        public GCodeLine NextCode { get; set; }

        public double DeltaX => EndState.X - StartState.X;
        public double DeltaY => EndState.Y - StartState.Y;
        public double DeltaZ => EndState.Z - StartState.Z;
        public double DeltaE => EndState.E - StartState.E;
        public double Length => Math.Sqrt(DeltaX * DeltaX + DeltaY * DeltaY + DeltaZ * DeltaZ);
        
        public List<GCodeLine> ModifiedLines { get; set; }
    }
}