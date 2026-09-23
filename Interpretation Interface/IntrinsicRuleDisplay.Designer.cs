
namespace Interpretation_Interface
{
	partial class IntrinsicRuleDisplay
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(IntrinsicRuleDisplay));
            IntrinsicRulesDataGridView = new DoubleBufferedDataGridView();
            Cancel_Button = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)IntrinsicRulesDataGridView).BeginInit();
            SuspendLayout();
            // 
            // IntrinsicRulesDataGridView
            // 
            IntrinsicRulesDataGridView.AllowUserToAddRows = false;
            IntrinsicRulesDataGridView.AllowUserToDeleteRows = false;
            IntrinsicRulesDataGridView.AllowUserToResizeRows = false;
            IntrinsicRulesDataGridView.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            IntrinsicRulesDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            IntrinsicRulesDataGridView.Location = new System.Drawing.Point(12, 12);
            IntrinsicRulesDataGridView.Name = "IntrinsicRulesDataGridView";
            IntrinsicRulesDataGridView.ReadOnly = true;
            IntrinsicRulesDataGridView.RowHeadersVisible = false;
            IntrinsicRulesDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            IntrinsicRulesDataGridView.Size = new System.Drawing.Size(776, 378);
            IntrinsicRulesDataGridView.TabIndex = 2;
            // 
            // Cancel_Button
            // 
            Cancel_Button.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            Cancel_Button.Location = new System.Drawing.Point(659, 396);
            Cancel_Button.Name = "Cancel_Button";
            Cancel_Button.Size = new System.Drawing.Size(129, 23);
            Cancel_Button.TabIndex = 3;
            Cancel_Button.Text = "&Cancel";
            Cancel_Button.UseVisualStyleBackColor = true;
            // 
            // IntrinsicRuleDisplay
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = Cancel_Button;
            ClientSize = new System.Drawing.Size(800, 431);
            Controls.Add(Cancel_Button);
            Controls.Add(IntrinsicRulesDataGridView);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "IntrinsicRuleDisplay";
            Text = "Intrinsic rules";
            ((System.ComponentModel.ISupportInitialize)IntrinsicRulesDataGridView).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private DoubleBufferedDataGridView IntrinsicRulesDataGridView;
		private System.Windows.Forms.Button Cancel_Button;
	}
}