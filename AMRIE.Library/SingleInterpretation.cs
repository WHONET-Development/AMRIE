namespace AMRIE.Library
{
    public class SingleInterpretation(string organismCode, string antibioticCode,
        string measurement, string interpretation)
    {
        public string OrganismCode { get; private set; } = organismCode;

        public string AntibioticCode { get; private set; } = antibioticCode;

        public string Measurement { get; private set; } = measurement;

        public string Interpretation { get; private set; } = interpretation;
    }
}
