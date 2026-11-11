namespace AMRIE.CLI
{
	// Inherit from engine class so existing callers/serialization remain 100% compatible
	public class SingleInterpretation : AMRIE.Library.SingleInterpretation
	{
		public SingleInterpretation(string organismCode_, string antibioticCode_,
			string measurement_, string interpretation_)
			: base(organismCode_, antibioticCode_, measurement_, interpretation_)
		{
		}
	}
}
