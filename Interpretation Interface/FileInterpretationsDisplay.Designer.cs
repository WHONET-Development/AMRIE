
namespace Interpretation_Interface
{
	partial class FileInterpretationsDisplay
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FileInterpretationsDisplay));
            ResultsGrid = new DoubleBufferedDataGridView();
            ((System.ComponentModel.ISupportInitialize)ResultsGrid).BeginInit();
            SuspendLayout();
            // 
            // ResultsGrid
            // 
            ResultsGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            ResultsGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            ResultsGrid.Location = new System.Drawing.Point(0, 0);
            ResultsGrid.Name = "ResultsGrid";
            ResultsGrid.Size = new System.Drawing.Size(800, 450);
            ResultsGrid.TabIndex = 0;
            // 
            // FileInterpretationsDisplay
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(800, 450);
            Controls.Add(ResultsGrid);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "FileInterpretationsDisplay";
            Text = "Interpretations";
            ((System.ComponentModel.ISupportInitialize)ResultsGrid).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private DoubleBufferedDataGridView ResultsGrid;
	}
}