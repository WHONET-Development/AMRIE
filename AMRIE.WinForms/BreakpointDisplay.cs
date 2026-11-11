using System.Collections.Generic;
using System.Windows.Forms;

namespace AMRIE.WinForms
{
	public partial class BreakpointDisplay : Form
	{
		public BreakpointDisplay(List<AMRIE.Library.Breakpoint> breakpoints)
		{
			InitializeComponent();

			BreakpointsDataGridView.DataSource = AMRIE.Library.Breakpoint.CreateTableFromArray(breakpoints);
			Text = string.Format("Matching breakpoints: {0}", breakpoints.Count);

			// Disable sorting. The order is important.
			foreach (DataGridViewColumn c in BreakpointsDataGridView.Columns)
				c.SortMode = DataGridViewColumnSortMode.NotSortable;
		}
	}
}
