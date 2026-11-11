namespace AMRIE.WinForms
{
	class DoubleBufferedDataGridView : System.Windows.Forms.DataGridView
	{
		public DoubleBufferedDataGridView()
		{
			this.DoubleBuffered = true;
		}
	}
}
