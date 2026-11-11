using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace AMRIE.WinForms
{
	public partial class IntrinsicRuleDisplay : Form
	{
		public IntrinsicRuleDisplay(IEnumerable<AMRIE.Library.ExpectedResistancePhenotypeRule> intrinsicResistanceRules)
		{
			InitializeComponent();

			IntrinsicRulesDataGridView.DataSource = AMRIE.Library.ExpectedResistancePhenotypeRule.CreateTableFromArray(intrinsicResistanceRules);
			Text = string.Format("Matching intrinsic resistance rules: {0}", intrinsicResistanceRules.Count());

			// Disable sorting. The order is important.
			foreach (DataGridViewColumn c in IntrinsicRulesDataGridView.Columns)
				c.SortMode = DataGridViewColumnSortMode.NotSortable;
		}
	}
}
