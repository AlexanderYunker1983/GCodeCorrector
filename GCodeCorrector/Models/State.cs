namespace GCodeCorrector.Models
{
    public struct State
    {
        public State(State prevState)
        {
            X = prevState.X;
            Y = prevState.Y;
            Z = prevState.Z;
            E = prevState.E;
        }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double E { get; set; }
    }
}