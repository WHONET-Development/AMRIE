namespace AMR_Engine
{
    public class SingleInterpretation
    {
        public SingleInterpretation(string organismCode, string antibioticCode,
            string measurement, string interpretation)
        {
            OrganismCode = organismCode;
            AntibioticCode = antibioticCode;
            Measurement = measurement;
            Interpretation = interpretation;
        }

        public string OrganismCode { get; private set; }

        public string AntibioticCode { get; private set; }

        public string Measurement { get; private set; }

        public string Interpretation { get; private set; }
    }
}
