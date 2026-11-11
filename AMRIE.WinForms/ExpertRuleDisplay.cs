using System.Collections.Generic;
using System.Windows.Forms;

namespace AMRIE.WinForms
{
	public partial class ExpertRuleDisplay : Form
	{
		public ExpertRuleDisplay(List<AMRIE.Library.ExpertInterpretationRule> expertRules)
		{
			InitializeComponent();

			ExpertRulesDataGridView.DataSource = 
				AMRIE.Library.ExpertInterpretationRule.CreateTableFromArray(expertRules);

			Text = string.Format("Matching expert rules: {0}", expertRules.Count);
		}
	}
}
