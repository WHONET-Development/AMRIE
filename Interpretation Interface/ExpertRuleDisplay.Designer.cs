
namespace Interpretation_Interface
{
	partial class ExpertRuleDisplay
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExpertRuleDisplay));
            ExpertRulesDataGridView = new DoubleBufferedDataGridView();
            Cancel_Button = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)ExpertRulesDataGridView).BeginInit();
            SuspendLayout();
            // 
            // ExpertRulesDataGridView
            // 
            ExpertRulesDataGridView.AllowUserToAddRows = false;
            ExpertRulesDataGridView.AllowUserToDeleteRows = false;
            ExpertRulesDataGridView.AllowUserToResizeRows = false;
            ExpertRulesDataGridView.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            ExpertRulesDataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            ExpertRulesDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            ExpertRulesDataGridView.Location = new System.Drawing.Point(12, 12);
            ExpertRulesDataGridView.Name = "ExpertRulesDataGridView";
            ExpertRulesDataGridView.ReadOnly = true;
            ExpertRulesDataGridView.RowHeadersVisible = false;
            ExpertRulesDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            ExpertRulesDataGridView.Size = new System.Drawing.Size(868, 374);
            ExpertRulesDataGridView.TabIndex = 1;
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
            // ExpertRuleDisplay
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = Cancel_Button;
            ClientSize = new System.Drawing.Size(892, 427);
            Controls.Add(Cancel_Button);
            Controls.Add(ExpertRulesDataGridView);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "ExpertRuleDisplay";
            Text = "ExpertRuleDisplay";
            ((System.ComponentModel.ISupportInitialize)ExpertRulesDataGridView).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private DoubleBufferedDataGridView ExpertRulesDataGridView;
		private System.Windows.Forms.Button Cancel_Button;
	}
}