
namespace AMR_InterpretationInterface
{
	partial class MainApplicationForm
	{
		/// <summary>
		///  Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainApplicationForm));
            GetApplicableBreakpointsButton = new System.Windows.Forms.Button();
            GuidelinesCheckbox = new System.Windows.Forms.CheckBox();
            SelectedGuidelinesCheckedListBox = new System.Windows.Forms.CheckedListBox();
            YearCheckbox = new System.Windows.Forms.CheckBox();
            GuidelineYearUpDown = new System.Windows.Forms.NumericUpDown();
            BreakpointTypesCheckbox = new System.Windows.Forms.CheckBox();
            BreakpointTypesCheckedListBox = new System.Windows.Forms.CheckedListBox();
            SitesOfInfectionCheckedListBox = new System.Windows.Forms.CheckedListBox();
            SitesOfInfectionCheckbox = new System.Windows.Forms.CheckBox();
            GetApplicableIntrinsicResistanceRulesButton = new System.Windows.Forms.Button();
            GetInerpretationsButton = new System.Windows.Forms.Button();
            ResultStringTextBox = new System.Windows.Forms.TextBox();
            TestMeasurementLabel = new System.Windows.Forms.Label();
            ModeTabControl = new System.Windows.Forms.TabControl();
            MultipleInterpretationsTab = new System.Windows.Forms.TabPage();
            ProgressMeter = new System.Windows.Forms.ProgressBar();
            BrowseForConfigFileButton = new System.Windows.Forms.Button();
            ConfigFileTextBox = new System.Windows.Forms.TextBox();
            ConfigFileLabel = new System.Windows.Forms.Label();
            BrowseForOutputFileButton = new System.Windows.Forms.Button();
            OutputFileTextBox = new System.Windows.Forms.TextBox();
            OutputFileNameLabel = new System.Windows.Forms.Label();
            FieldDelimiterComboBox = new System.Windows.Forms.ComboBox();
            FieldDelimiterLabel = new System.Windows.Forms.Label();
            InterpretFileButton = new System.Windows.Forms.Button();
            BrowseForInputFileButton = new System.Windows.Forms.Button();
            InputFileTextBox = new System.Windows.Forms.TextBox();
            InputFileNameLabel = new System.Windows.Forms.Label();
            SingleInterpretationTab = new System.Windows.Forms.TabPage();
            InterpretationPanel = new System.Windows.Forms.GroupBox();
            IncludeInterpretationCommentsCheckbox = new System.Windows.Forms.CheckBox();
            ViewResourcesPanel = new System.Windows.Forms.GroupBox();
            GetApplicableExpertRulesButton = new System.Windows.Forms.Button();
            OrganismGroupBox = new System.Windows.Forms.GroupBox();
            OrganismSearchTextBox = new System.Windows.Forms.TextBox();
            OrganismComboBox = new System.Windows.Forms.ComboBox();
            BreakpointFiltersPanel = new System.Windows.Forms.GroupBox();
            TestPanel = new System.Windows.Forms.GroupBox();
            AntibioticSearchTextBox = new System.Windows.Forms.TextBox();
            AntibioticComboBox = new System.Windows.Forms.ComboBox();
            DiskContentComboBox = new System.Windows.Forms.ComboBox();
            MIC_Etest_RadioButton = new System.Windows.Forms.RadioButton();
            DiskRadioButton = new System.Windows.Forms.RadioButton();
            Cancel_Button = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)GuidelineYearUpDown).BeginInit();
            ModeTabControl.SuspendLayout();
            MultipleInterpretationsTab.SuspendLayout();
            SingleInterpretationTab.SuspendLayout();
            InterpretationPanel.SuspendLayout();
            ViewResourcesPanel.SuspendLayout();
            OrganismGroupBox.SuspendLayout();
            BreakpointFiltersPanel.SuspendLayout();
            TestPanel.SuspendLayout();
            SuspendLayout();
            // 
            // GetApplicableBreakpointsButton
            // 
            GetApplicableBreakpointsButton.Location = new System.Drawing.Point(6, 22);
            GetApplicableBreakpointsButton.Name = "GetApplicableBreakpointsButton";
            GetApplicableBreakpointsButton.Size = new System.Drawing.Size(251, 23);
            GetApplicableBreakpointsButton.TabIndex = 0;
            GetApplicableBreakpointsButton.Text = "Get applicable breakpoints";
            GetApplicableBreakpointsButton.UseVisualStyleBackColor = true;
            GetApplicableBreakpointsButton.Click += GetApplicableBreakpointsButton_Click;
            // 
            // GuidelinesCheckbox
            // 
            GuidelinesCheckbox.AutoSize = true;
            GuidelinesCheckbox.Checked = true;
            GuidelinesCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            GuidelinesCheckbox.Location = new System.Drawing.Point(21, 22);
            GuidelinesCheckbox.Name = "GuidelinesCheckbox";
            GuidelinesCheckbox.Size = new System.Drawing.Size(122, 19);
            GuidelinesCheckbox.TabIndex = 1;
            GuidelinesCheckbox.Text = "Restrict guidelines";
            GuidelinesCheckbox.UseVisualStyleBackColor = true;
            GuidelinesCheckbox.CheckedChanged += GuidelinesCheckbox_CheckedChanged;
            // 
            // SelectedGuidelinesCheckedListBox
            // 
            SelectedGuidelinesCheckedListBox.BackColor = System.Drawing.SystemColors.Control;
            SelectedGuidelinesCheckedListBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            SelectedGuidelinesCheckedListBox.CheckOnClick = true;
            SelectedGuidelinesCheckedListBox.FormattingEnabled = true;
            SelectedGuidelinesCheckedListBox.Items.AddRange(new object[] { "CLSI", "EUCAST", "SFM" });
            SelectedGuidelinesCheckedListBox.Location = new System.Drawing.Point(30, 47);
            SelectedGuidelinesCheckedListBox.Name = "SelectedGuidelinesCheckedListBox";
            SelectedGuidelinesCheckedListBox.Size = new System.Drawing.Size(139, 54);
            SelectedGuidelinesCheckedListBox.TabIndex = 2;
            SelectedGuidelinesCheckedListBox.ItemCheck += AntibioticChanged;
            // 
            // YearCheckbox
            // 
            YearCheckbox.Checked = true;
            YearCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            YearCheckbox.Location = new System.Drawing.Point(12, 13);
            YearCheckbox.Name = "YearCheckbox";
            YearCheckbox.Size = new System.Drawing.Size(236, 19);
            YearCheckbox.TabIndex = 0;
            YearCheckbox.Text = "Restrict guideline year";
            YearCheckbox.UseVisualStyleBackColor = true;
            YearCheckbox.CheckedChanged += YearCheckbox_CheckedChanged;
            // 
            // GuidelineYearUpDown
            // 
            GuidelineYearUpDown.Location = new System.Drawing.Point(254, 12);
            GuidelineYearUpDown.Maximum = new decimal(new int[] { 3000, 0, 0, 0 });
            GuidelineYearUpDown.Minimum = new decimal(new int[] { 2011, 0, 0, 0 });
            GuidelineYearUpDown.Name = "GuidelineYearUpDown";
            GuidelineYearUpDown.Size = new System.Drawing.Size(55, 23);
            GuidelineYearUpDown.TabIndex = 1;
            GuidelineYearUpDown.Value = new decimal(new int[] { 2011, 0, 0, 0 });
            // 
            // BreakpointTypesCheckbox
            // 
            BreakpointTypesCheckbox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            BreakpointTypesCheckbox.Location = new System.Drawing.Point(242, 22);
            BreakpointTypesCheckbox.Name = "BreakpointTypesCheckbox";
            BreakpointTypesCheckbox.Size = new System.Drawing.Size(224, 19);
            BreakpointTypesCheckbox.TabIndex = 3;
            BreakpointTypesCheckbox.Text = "Restrict breakpoint types";
            BreakpointTypesCheckbox.UseVisualStyleBackColor = true;
            BreakpointTypesCheckbox.CheckedChanged += BreakpointTypesCheckbox_CheckedChanged;
            // 
            // BreakpointTypesCheckedListBox
            // 
            BreakpointTypesCheckedListBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            BreakpointTypesCheckedListBox.BackColor = System.Drawing.SystemColors.Control;
            BreakpointTypesCheckedListBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            BreakpointTypesCheckedListBox.CheckOnClick = true;
            BreakpointTypesCheckedListBox.Enabled = false;
            BreakpointTypesCheckedListBox.FormattingEnabled = true;
            BreakpointTypesCheckedListBox.Items.AddRange(new object[] { "Human", "Animal", "ECOFF" });
            BreakpointTypesCheckedListBox.Location = new System.Drawing.Point(260, 47);
            BreakpointTypesCheckedListBox.Name = "BreakpointTypesCheckedListBox";
            BreakpointTypesCheckedListBox.Size = new System.Drawing.Size(209, 54);
            BreakpointTypesCheckedListBox.TabIndex = 4;
            // 
            // SitesOfInfectionCheckedListBox
            // 
            SitesOfInfectionCheckedListBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            SitesOfInfectionCheckedListBox.BackColor = System.Drawing.SystemColors.Control;
            SitesOfInfectionCheckedListBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            SitesOfInfectionCheckedListBox.CheckOnClick = true;
            SitesOfInfectionCheckedListBox.Enabled = false;
            SitesOfInfectionCheckedListBox.FormattingEnabled = true;
            SitesOfInfectionCheckedListBox.Location = new System.Drawing.Point(533, 47);
            SitesOfInfectionCheckedListBox.Name = "SitesOfInfectionCheckedListBox";
            SitesOfInfectionCheckedListBox.Size = new System.Drawing.Size(269, 72);
            SitesOfInfectionCheckedListBox.TabIndex = 6;
            // 
            // SitesOfInfectionCheckbox
            // 
            SitesOfInfectionCheckbox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            SitesOfInfectionCheckbox.Location = new System.Drawing.Point(518, 22);
            SitesOfInfectionCheckbox.Name = "SitesOfInfectionCheckbox";
            SitesOfInfectionCheckbox.Size = new System.Drawing.Size(224, 19);
            SitesOfInfectionCheckbox.TabIndex = 5;
            SitesOfInfectionCheckbox.Text = "Restrict sites of infection";
            SitesOfInfectionCheckbox.UseVisualStyleBackColor = true;
            SitesOfInfectionCheckbox.CheckedChanged += SiteOfInfectionCheckbox_CheckedChanged;
            // 
            // GetApplicableIntrinsicResistanceRulesButton
            // 
            GetApplicableIntrinsicResistanceRulesButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            GetApplicableIntrinsicResistanceRulesButton.Location = new System.Drawing.Point(518, 22);
            GetApplicableIntrinsicResistanceRulesButton.Name = "GetApplicableIntrinsicResistanceRulesButton";
            GetApplicableIntrinsicResistanceRulesButton.Size = new System.Drawing.Size(284, 23);
            GetApplicableIntrinsicResistanceRulesButton.TabIndex = 2;
            GetApplicableIntrinsicResistanceRulesButton.Text = "Get applicable intrinsic resistance";
            GetApplicableIntrinsicResistanceRulesButton.UseVisualStyleBackColor = true;
            GetApplicableIntrinsicResistanceRulesButton.Click += GetApplicableIntrinsicResistanceRulesButton_Click;
            // 
            // GetInerpretationsButton
            // 
            GetInerpretationsButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            GetInerpretationsButton.Location = new System.Drawing.Point(548, 22);
            GetInerpretationsButton.Name = "GetInerpretationsButton";
            GetInerpretationsButton.Size = new System.Drawing.Size(254, 23);
            GetInerpretationsButton.TabIndex = 2;
            GetInerpretationsButton.Text = "Get interpretations";
            GetInerpretationsButton.UseVisualStyleBackColor = true;
            GetInerpretationsButton.Click += GetInerpretationsButton_Click;
            // 
            // ResultStringTextBox
            // 
            ResultStringTextBox.Location = new System.Drawing.Point(195, 22);
            ResultStringTextBox.Name = "ResultStringTextBox";
            ResultStringTextBox.Size = new System.Drawing.Size(55, 23);
            ResultStringTextBox.TabIndex = 0;
            // 
            // TestMeasurementLabel
            // 
            TestMeasurementLabel.AutoSize = true;
            TestMeasurementLabel.Location = new System.Drawing.Point(6, 25);
            TestMeasurementLabel.Name = "TestMeasurementLabel";
            TestMeasurementLabel.Size = new System.Drawing.Size(104, 15);
            TestMeasurementLabel.TabIndex = 44;
            TestMeasurementLabel.Text = "Test measurement";
            // 
            // ModeTabControl
            // 
            ModeTabControl.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            ModeTabControl.Controls.Add(MultipleInterpretationsTab);
            ModeTabControl.Controls.Add(SingleInterpretationTab);
            ModeTabControl.Location = new System.Drawing.Point(12, 41);
            ModeTabControl.MinimumSize = new System.Drawing.Size(737, 288);
            ModeTabControl.Name = "ModeTabControl";
            ModeTabControl.SelectedIndex = 0;
            ModeTabControl.Size = new System.Drawing.Size(828, 424);
            ModeTabControl.TabIndex = 2;
            // 
            // MultipleInterpretationsTab
            // 
            MultipleInterpretationsTab.Controls.Add(ProgressMeter);
            MultipleInterpretationsTab.Controls.Add(BrowseForConfigFileButton);
            MultipleInterpretationsTab.Controls.Add(ConfigFileTextBox);
            MultipleInterpretationsTab.Controls.Add(ConfigFileLabel);
            MultipleInterpretationsTab.Controls.Add(BrowseForOutputFileButton);
            MultipleInterpretationsTab.Controls.Add(OutputFileTextBox);
            MultipleInterpretationsTab.Controls.Add(OutputFileNameLabel);
            MultipleInterpretationsTab.Controls.Add(FieldDelimiterComboBox);
            MultipleInterpretationsTab.Controls.Add(FieldDelimiterLabel);
            MultipleInterpretationsTab.Controls.Add(InterpretFileButton);
            MultipleInterpretationsTab.Controls.Add(BrowseForInputFileButton);
            MultipleInterpretationsTab.Controls.Add(InputFileTextBox);
            MultipleInterpretationsTab.Controls.Add(InputFileNameLabel);
            MultipleInterpretationsTab.Location = new System.Drawing.Point(4, 24);
            MultipleInterpretationsTab.Name = "MultipleInterpretationsTab";
            MultipleInterpretationsTab.Padding = new System.Windows.Forms.Padding(3);
            MultipleInterpretationsTab.Size = new System.Drawing.Size(820, 396);
            MultipleInterpretationsTab.TabIndex = 0;
            MultipleInterpretationsTab.Text = "File interpretation";
            MultipleInterpretationsTab.UseVisualStyleBackColor = true;
            // 
            // ProgressMeter
            // 
            ProgressMeter.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            ProgressMeter.Location = new System.Drawing.Point(111, 123);
            ProgressMeter.Name = "ProgressMeter";
            ProgressMeter.Size = new System.Drawing.Size(480, 23);
            ProgressMeter.TabIndex = 11;
            // 
            // BrowseForConfigFileButton
            // 
            BrowseForConfigFileButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            BrowseForConfigFileButton.Location = new System.Drawing.Point(713, 65);
            BrowseForConfigFileButton.Name = "BrowseForConfigFileButton";
            BrowseForConfigFileButton.Size = new System.Drawing.Size(101, 23);
            BrowseForConfigFileButton.TabIndex = 5;
            BrowseForConfigFileButton.Text = "Browse";
            BrowseForConfigFileButton.UseVisualStyleBackColor = true;
            BrowseForConfigFileButton.Click += BrowseForConfigFileButton_Click;
            // 
            // ConfigFileTextBox
            // 
            ConfigFileTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            ConfigFileTextBox.Location = new System.Drawing.Point(111, 65);
            ConfigFileTextBox.Name = "ConfigFileTextBox";
            ConfigFileTextBox.Size = new System.Drawing.Size(596, 23);
            ConfigFileTextBox.TabIndex = 4;
            ConfigFileTextBox.Text = "Resources\\SampleConfig.json";
            // 
            // ConfigFileLabel
            // 
            ConfigFileLabel.AutoSize = true;
            ConfigFileLabel.Location = new System.Drawing.Point(6, 68);
            ConfigFileLabel.Name = "ConfigFileLabel";
            ConfigFileLabel.Size = new System.Drawing.Size(62, 15);
            ConfigFileLabel.TabIndex = 9;
            ConfigFileLabel.Text = "Config file";
            // 
            // BrowseForOutputFileButton
            // 
            BrowseForOutputFileButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            BrowseForOutputFileButton.Location = new System.Drawing.Point(713, 94);
            BrowseForOutputFileButton.Name = "BrowseForOutputFileButton";
            BrowseForOutputFileButton.Size = new System.Drawing.Size(101, 23);
            BrowseForOutputFileButton.TabIndex = 7;
            BrowseForOutputFileButton.Text = "Browse";
            BrowseForOutputFileButton.UseVisualStyleBackColor = true;
            BrowseForOutputFileButton.Click += BrowseForOutputFileButton_Click;
            // 
            // OutputFileTextBox
            // 
            OutputFileTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            OutputFileTextBox.Location = new System.Drawing.Point(111, 94);
            OutputFileTextBox.Name = "OutputFileTextBox";
            OutputFileTextBox.Size = new System.Drawing.Size(596, 23);
            OutputFileTextBox.TabIndex = 6;
            OutputFileTextBox.Text = "Results.txt";
            // 
            // OutputFileNameLabel
            // 
            OutputFileNameLabel.AutoSize = true;
            OutputFileNameLabel.Location = new System.Drawing.Point(6, 97);
            OutputFileNameLabel.Name = "OutputFileNameLabel";
            OutputFileNameLabel.Size = new System.Drawing.Size(64, 15);
            OutputFileNameLabel.TabIndex = 6;
            OutputFileNameLabel.Text = "Output file";
            // 
            // FieldDelimiterComboBox
            // 
            FieldDelimiterComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            FieldDelimiterComboBox.FormattingEnabled = true;
            FieldDelimiterComboBox.Items.AddRange(new object[] { "|", ",", ";", "TAB" });
            FieldDelimiterComboBox.Location = new System.Drawing.Point(111, 36);
            FieldDelimiterComboBox.Name = "FieldDelimiterComboBox";
            FieldDelimiterComboBox.Size = new System.Drawing.Size(59, 23);
            FieldDelimiterComboBox.TabIndex = 3;
            // 
            // FieldDelimiterLabel
            // 
            FieldDelimiterLabel.AutoSize = true;
            FieldDelimiterLabel.Location = new System.Drawing.Point(6, 39);
            FieldDelimiterLabel.Name = "FieldDelimiterLabel";
            FieldDelimiterLabel.Size = new System.Drawing.Size(82, 15);
            FieldDelimiterLabel.TabIndex = 4;
            FieldDelimiterLabel.Text = "Field delimiter";
            // 
            // InterpretFileButton
            // 
            InterpretFileButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            InterpretFileButton.Location = new System.Drawing.Point(597, 123);
            InterpretFileButton.Name = "InterpretFileButton";
            InterpretFileButton.Size = new System.Drawing.Size(217, 23);
            InterpretFileButton.TabIndex = 8;
            InterpretFileButton.Text = "Interpret file";
            InterpretFileButton.UseVisualStyleBackColor = true;
            InterpretFileButton.Click += InterpretFileButton_Click;
            // 
            // BrowseForInputFileButton
            // 
            BrowseForInputFileButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            BrowseForInputFileButton.Location = new System.Drawing.Point(713, 7);
            BrowseForInputFileButton.Name = "BrowseForInputFileButton";
            BrowseForInputFileButton.Size = new System.Drawing.Size(101, 23);
            BrowseForInputFileButton.TabIndex = 2;
            BrowseForInputFileButton.Text = "Browse";
            BrowseForInputFileButton.UseVisualStyleBackColor = true;
            BrowseForInputFileButton.Click += BrowseForInputFileButton_Click;
            // 
            // InputFileTextBox
            // 
            InputFileTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            InputFileTextBox.Location = new System.Drawing.Point(111, 7);
            InputFileTextBox.Name = "InputFileTextBox";
            InputFileTextBox.Size = new System.Drawing.Size(596, 23);
            InputFileTextBox.TabIndex = 1;
            InputFileTextBox.Text = "Resources\\SampleInputFile.txt";
            // 
            // InputFileNameLabel
            // 
            InputFileNameLabel.AutoSize = true;
            InputFileNameLabel.Location = new System.Drawing.Point(6, 10);
            InputFileNameLabel.Name = "InputFileNameLabel";
            InputFileNameLabel.Size = new System.Drawing.Size(54, 15);
            InputFileNameLabel.TabIndex = 0;
            InputFileNameLabel.Text = "Input file";
            // 
            // SingleInterpretationTab
            // 
            SingleInterpretationTab.Controls.Add(InterpretationPanel);
            SingleInterpretationTab.Controls.Add(ViewResourcesPanel);
            SingleInterpretationTab.Controls.Add(OrganismGroupBox);
            SingleInterpretationTab.Controls.Add(BreakpointFiltersPanel);
            SingleInterpretationTab.Controls.Add(TestPanel);
            SingleInterpretationTab.Location = new System.Drawing.Point(4, 24);
            SingleInterpretationTab.Name = "SingleInterpretationTab";
            SingleInterpretationTab.Padding = new System.Windows.Forms.Padding(3);
            SingleInterpretationTab.Size = new System.Drawing.Size(820, 396);
            SingleInterpretationTab.TabIndex = 1;
            SingleInterpretationTab.Text = "Individual interpretations and resources";
            SingleInterpretationTab.UseVisualStyleBackColor = true;
            // 
            // InterpretationPanel
            // 
            InterpretationPanel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            InterpretationPanel.Controls.Add(TestMeasurementLabel);
            InterpretationPanel.Controls.Add(ResultStringTextBox);
            InterpretationPanel.Controls.Add(GetInerpretationsButton);
            InterpretationPanel.Controls.Add(IncludeInterpretationCommentsCheckbox);
            InterpretationPanel.Location = new System.Drawing.Point(6, 279);
            InterpretationPanel.Name = "InterpretationPanel";
            InterpretationPanel.Size = new System.Drawing.Size(808, 53);
            InterpretationPanel.TabIndex = 3;
            InterpretationPanel.TabStop = false;
            InterpretationPanel.Text = "Interpret result";
            // 
            // IncludeInterpretationCommentsCheckbox
            // 
            IncludeInterpretationCommentsCheckbox.AutoSize = true;
            IncludeInterpretationCommentsCheckbox.Location = new System.Drawing.Point(256, 24);
            IncludeInterpretationCommentsCheckbox.Name = "IncludeInterpretationCommentsCheckbox";
            IncludeInterpretationCommentsCheckbox.Size = new System.Drawing.Size(200, 19);
            IncludeInterpretationCommentsCheckbox.TabIndex = 1;
            IncludeInterpretationCommentsCheckbox.Text = "Include interpretation comments";
            IncludeInterpretationCommentsCheckbox.UseVisualStyleBackColor = true;
            // 
            // ViewResourcesPanel
            // 
            ViewResourcesPanel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            ViewResourcesPanel.Controls.Add(GetApplicableBreakpointsButton);
            ViewResourcesPanel.Controls.Add(GetApplicableExpertRulesButton);
            ViewResourcesPanel.Controls.Add(GetApplicableIntrinsicResistanceRulesButton);
            ViewResourcesPanel.Location = new System.Drawing.Point(6, 338);
            ViewResourcesPanel.Name = "ViewResourcesPanel";
            ViewResourcesPanel.Size = new System.Drawing.Size(808, 52);
            ViewResourcesPanel.TabIndex = 4;
            ViewResourcesPanel.TabStop = false;
            ViewResourcesPanel.Text = "View applicable resources";
            // 
            // GetApplicableExpertRulesButton
            // 
            GetApplicableExpertRulesButton.Location = new System.Drawing.Point(263, 22);
            GetApplicableExpertRulesButton.Name = "GetApplicableExpertRulesButton";
            GetApplicableExpertRulesButton.Size = new System.Drawing.Size(251, 23);
            GetApplicableExpertRulesButton.TabIndex = 1;
            GetApplicableExpertRulesButton.Text = "Get applicable expert rules";
            GetApplicableExpertRulesButton.UseVisualStyleBackColor = true;
            GetApplicableExpertRulesButton.Click += GetApplicableExpertRulesButton_Click;
            // 
            // OrganismGroupBox
            // 
            OrganismGroupBox.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            OrganismGroupBox.Controls.Add(OrganismSearchTextBox);
            OrganismGroupBox.Controls.Add(OrganismComboBox);
            OrganismGroupBox.Location = new System.Drawing.Point(6, 159);
            OrganismGroupBox.Name = "OrganismGroupBox";
            OrganismGroupBox.Size = new System.Drawing.Size(808, 55);
            OrganismGroupBox.TabIndex = 1;
            OrganismGroupBox.TabStop = false;
            OrganismGroupBox.Text = "Microorganism";
            // 
            // OrganismSearchTextBox
            // 
            OrganismSearchTextBox.Location = new System.Drawing.Point(6, 22);
            OrganismSearchTextBox.Name = "OrganismSearchTextBox";
            OrganismSearchTextBox.PlaceholderText = "Search";
            OrganismSearchTextBox.Size = new System.Drawing.Size(100, 23);
            OrganismSearchTextBox.TabIndex = 0;
            OrganismSearchTextBox.Tag = OrganismComboBox;
            OrganismSearchTextBox.TextChanged += SearchTextChanged;
            // 
            // OrganismComboBox
            // 
            OrganismComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            OrganismComboBox.FormattingEnabled = true;
            OrganismComboBox.Location = new System.Drawing.Point(112, 22);
            OrganismComboBox.Name = "OrganismComboBox";
            OrganismComboBox.Size = new System.Drawing.Size(315, 23);
            OrganismComboBox.TabIndex = 1;
            // 
            // BreakpointFiltersPanel
            // 
            BreakpointFiltersPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            BreakpointFiltersPanel.Controls.Add(GuidelinesCheckbox);
            BreakpointFiltersPanel.Controls.Add(SelectedGuidelinesCheckedListBox);
            BreakpointFiltersPanel.Controls.Add(SitesOfInfectionCheckbox);
            BreakpointFiltersPanel.Controls.Add(BreakpointTypesCheckbox);
            BreakpointFiltersPanel.Controls.Add(SitesOfInfectionCheckedListBox);
            BreakpointFiltersPanel.Controls.Add(BreakpointTypesCheckedListBox);
            BreakpointFiltersPanel.Location = new System.Drawing.Point(6, 6);
            BreakpointFiltersPanel.Name = "BreakpointFiltersPanel";
            BreakpointFiltersPanel.Size = new System.Drawing.Size(808, 147);
            BreakpointFiltersPanel.TabIndex = 0;
            BreakpointFiltersPanel.TabStop = false;
            BreakpointFiltersPanel.Text = "Breakpoint restrictions";
            // 
            // TestPanel
            // 
            TestPanel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            TestPanel.Controls.Add(AntibioticSearchTextBox);
            TestPanel.Controls.Add(DiskContentComboBox);
            TestPanel.Controls.Add(MIC_Etest_RadioButton);
            TestPanel.Controls.Add(DiskRadioButton);
            TestPanel.Controls.Add(AntibioticComboBox);
            TestPanel.Location = new System.Drawing.Point(6, 220);
            TestPanel.Name = "TestPanel";
            TestPanel.Size = new System.Drawing.Size(808, 53);
            TestPanel.TabIndex = 2;
            TestPanel.TabStop = false;
            TestPanel.Text = "Antimicrobial test";
            // 
            // AntibioticSearchTextBox
            // 
            AntibioticSearchTextBox.Location = new System.Drawing.Point(6, 22);
            AntibioticSearchTextBox.Name = "AntibioticSearchTextBox";
            AntibioticSearchTextBox.PlaceholderText = "Search";
            AntibioticSearchTextBox.Size = new System.Drawing.Size(100, 23);
            AntibioticSearchTextBox.TabIndex = 5;
            AntibioticSearchTextBox.Tag = AntibioticComboBox;
            AntibioticSearchTextBox.TextChanged += SearchTextChanged;
            // 
            // AntibioticComboBox
            // 
            AntibioticComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            AntibioticComboBox.FormattingEnabled = true;
            AntibioticComboBox.Location = new System.Drawing.Point(112, 22);
            AntibioticComboBox.Name = "AntibioticComboBox";
            AntibioticComboBox.Size = new System.Drawing.Size(244, 23);
            AntibioticComboBox.TabIndex = 0;
            AntibioticComboBox.SelectedValueChanged += AntibioticChanged;
            // 
            // DiskContentComboBox
            // 
            DiskContentComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            DiskContentComboBox.FormattingEnabled = true;
            DiskContentComboBox.Location = new System.Drawing.Point(456, 22);
            DiskContentComboBox.Name = "DiskContentComboBox";
            DiskContentComboBox.Size = new System.Drawing.Size(127, 23);
            DiskContentComboBox.TabIndex = 0;
            // 
            // MIC_Etest_RadioButton
            // 
            MIC_Etest_RadioButton.AutoSize = true;
            MIC_Etest_RadioButton.Location = new System.Drawing.Point(610, 24);
            MIC_Etest_RadioButton.Name = "MIC_Etest_RadioButton";
            MIC_Etest_RadioButton.Size = new System.Drawing.Size(83, 19);
            MIC_Etest_RadioButton.TabIndex = 2;
            MIC_Etest_RadioButton.Text = "MIC / Etest";
            MIC_Etest_RadioButton.UseVisualStyleBackColor = true;
            // 
            // DiskRadioButton
            // 
            DiskRadioButton.AutoSize = true;
            DiskRadioButton.Checked = true;
            DiskRadioButton.Location = new System.Drawing.Point(362, 24);
            DiskRadioButton.Name = "DiskRadioButton";
            DiskRadioButton.Size = new System.Drawing.Size(47, 19);
            DiskRadioButton.TabIndex = 1;
            DiskRadioButton.TabStop = true;
            DiskRadioButton.Text = "Disk";
            DiskRadioButton.UseVisualStyleBackColor = true;
            DiskRadioButton.CheckedChanged += TestMethodChanged;
            // 
            // Cancel_Button
            // 
            Cancel_Button.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            Cancel_Button.Location = new System.Drawing.Point(729, 12);
            Cancel_Button.Name = "Cancel_Button";
            Cancel_Button.Size = new System.Drawing.Size(111, 23);
            Cancel_Button.TabIndex = 3;
            Cancel_Button.Text = "&Cancel";
            Cancel_Button.UseVisualStyleBackColor = true;
            Cancel_Button.Click += Cancel_Button_Click;
            // 
            // MainApplicationForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(852, 477);
            Controls.Add(Cancel_Button);
            Controls.Add(ModeTabControl);
            Controls.Add(GuidelineYearUpDown);
            Controls.Add(YearCheckbox);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MinimumSize = new System.Drawing.Size(868, 516);
            Name = "MainApplicationForm";
            Text = "AMRIE";
            Load += MainApplicationForm_Load;
            ((System.ComponentModel.ISupportInitialize)GuidelineYearUpDown).EndInit();
            ModeTabControl.ResumeLayout(false);
            MultipleInterpretationsTab.ResumeLayout(false);
            MultipleInterpretationsTab.PerformLayout();
            SingleInterpretationTab.ResumeLayout(false);
            InterpretationPanel.ResumeLayout(false);
            InterpretationPanel.PerformLayout();
            ViewResourcesPanel.ResumeLayout(false);
            OrganismGroupBox.ResumeLayout(false);
            OrganismGroupBox.PerformLayout();
            BreakpointFiltersPanel.ResumeLayout(false);
            BreakpointFiltersPanel.PerformLayout();
            TestPanel.ResumeLayout(false);
            TestPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button GetApplicableBreakpointsButton;
		private System.Windows.Forms.CheckBox GuidelinesCheckbox;
		private System.Windows.Forms.CheckedListBox SelectedGuidelinesCheckedListBox;
		private System.Windows.Forms.CheckBox YearCheckbox;
		private System.Windows.Forms.NumericUpDown GuidelineYearUpDown;
		private System.Windows.Forms.CheckBox BreakpointTypesCheckbox;
		private System.Windows.Forms.CheckedListBox BreakpointTypesCheckedListBox;
		private System.Windows.Forms.CheckedListBox SitesOfInfectionCheckedListBox;
		private System.Windows.Forms.CheckBox SitesOfInfectionCheckbox;
		private System.Windows.Forms.Button GetApplicableIntrinsicResistanceRulesButton;
		private System.Windows.Forms.Button GetInerpretationsButton;
		private System.Windows.Forms.TextBox ResultStringTextBox;
		private System.Windows.Forms.Label TestMeasurementLabel;
		private System.Windows.Forms.TabControl ModeTabControl;
		private System.Windows.Forms.TabPage MultipleInterpretationsTab;
		private System.Windows.Forms.TabPage SingleInterpretationTab;
		private System.Windows.Forms.Label InputFileNameLabel;
		private System.Windows.Forms.TextBox InputFileTextBox;
		private System.Windows.Forms.Button BrowseForInputFileButton;
		private System.Windows.Forms.Button InterpretFileButton;
		private System.Windows.Forms.Label FieldDelimiterLabel;
		private System.Windows.Forms.ComboBox FieldDelimiterComboBox;
		private System.Windows.Forms.Button Cancel_Button;
		private System.Windows.Forms.Button BrowseForOutputFileButton;
		private System.Windows.Forms.TextBox OutputFileTextBox;
		private System.Windows.Forms.Label OutputFileNameLabel;
		private System.Windows.Forms.Button BrowseForConfigFileButton;
		private System.Windows.Forms.TextBox ConfigFileTextBox;
		private System.Windows.Forms.Label ConfigFileLabel;
		private System.Windows.Forms.ProgressBar ProgressMeter;
		private System.Windows.Forms.Button GetApplicableExpertRulesButton;
		private System.Windows.Forms.CheckBox IncludeInterpretationCommentsCheckbox;
		private System.Windows.Forms.GroupBox TestPanel;
		private System.Windows.Forms.RadioButton MIC_Etest_RadioButton;
		private System.Windows.Forms.RadioButton DiskRadioButton;
		private System.Windows.Forms.ComboBox AntibioticComboBox;
		private System.Windows.Forms.ComboBox DiskContentComboBox;
		private System.Windows.Forms.ComboBox OrganismComboBox;
		private System.Windows.Forms.GroupBox BreakpointFiltersPanel;
		private System.Windows.Forms.GroupBox OrganismGroupBox;
		private System.Windows.Forms.TextBox OrganismSearchTextBox;
		private System.Windows.Forms.GroupBox ViewResourcesPanel;
		private System.Windows.Forms.GroupBox InterpretationPanel;
		private System.Windows.Forms.TextBox AntibioticSearchTextBox;
	}
}

