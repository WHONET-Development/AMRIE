
namespace Interpretation_Interface
{
	partial class BreakpointDisplay
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BreakpointDisplay));
            Cancel_Button = new System.Windows.Forms.Button();
            BreakpointsDataGridView = new DoubleBufferedDataGridView();
            ((System.ComponentModel.ISupportInitialize)BreakpointsDataGridView).BeginInit();
            SuspendLayout();
            // 
            // Cancel_Button
            // 
            Cancel_Button.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            Cancel_Button.Location = new System.Drawing.Point(751, 392);
            Cancel_Button.Name = "Cancel_Button";
            Cancel_Button.Size = new System.Drawing.Size(129, 23);
            Cancel_Button.TabIndex = 0;
            Cancel_Button.Text = "&Cancel";
            Cancel_Button.UseVisualStyleBackColor = true;
            // 
            // BreakpointsDataGridView
            // 
            BreakpointsDataGridView.AllowUserToAddRows = false;
            BreakpointsDataGridView.AllowUserToDeleteRows = false;
            BreakpointsDataGridView.AllowUserToResizeRows = false;
            BreakpointsDataGridView.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            BreakpointsDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            BreakpointsDataGridView.Location = new System.Drawing.Point(12, 12);
            BreakpointsDataGridView.Name = "BreakpointsDataGridView";
            BreakpointsDataGridView.ReadOnly = true;
            BreakpointsDataGridView.RowHeadersVisible = false;
            BreakpointsDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            BreakpointsDataGridView.Size = new System.Drawing.Size(868, 374);
            BreakpointsDataGridView.TabIndex = 1;
            // 
            // BreakpointDisplay
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = Cancel_Button;
            ClientSize = new System.Drawing.Size(892, 427);
            Controls.Add(BreakpointsDataGridView);
            Controls.Add(Cancel_Button);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "BreakpointDisplay";
            Text = "Breakpoints";
            ((System.ComponentModel.ISupportInitialize)BreakpointsDataGridView).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button Cancel_Button;
		private DoubleBufferedDataGridView BreakpointsDataGridView;
	}
}