using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AMRIE.Library;

namespace AMRIE.WinForms
{
	public partial class MainInterface : Form
	{
		#region Init

		public MainInterface()
		{
			InitializeComponent();
		}

		private void MainApplicationForm_Load(object sender, EventArgs e)
		{
			GuidelineYearUpDown.Value = Constants.BreakpointTableRevisionYear;

			// Recode the empty string as "(Blank)" here for display purposes.
			// (Blank) will be processed as the empty string when we determine the applicable breakpoints.
			SitesOfInfectionCheckedListBox.Items.AddRange(
                [.. Constants.SitesOfInfection.DefaultOrder]);

			SelectedGuidelinesCheckedListBox.SetItemChecked(0, true);
			FieldDelimiterComboBox.SelectedIndex = 0;

			// Add the full list of organisms.
			List<Tuple<string, string>> Organisms = 
				new([DefaultSelection]);

			Organisms.AddRange(
				Organism.AllOrganisms.OrderBy(o => o.ORGANISM).
				Select(o => new Tuple<string, string>(string.Format("{0} - ({1})", o.ORGANISM, o.WHONET_ORG_CODE), o.WHONET_ORG_CODE)).
				Distinct());

			OrganismComboBox.Tag = Organisms;
			OrganismComboBox.DisplayMember = nameof(DefaultSelection.Item1);
			OrganismComboBox.ValueMember = nameof(DefaultSelection.Item2);
			OrganismComboBox.DataSource = Organisms;


			// Add the full list of antibiotics.
			List<Tuple<string, string>> Antibiotics = 
				new([DefaultSelection]);

			Antibiotics.AddRange(
				Antibiotic.AllAntibiotics.OrderBy(a => a.ANTIBIOTIC).
				Select(a => new Tuple<string, string>(string.Format("{0} - ({1})", a.ANTIBIOTIC, a.WHONET_ABX_CODE), a.WHONET_ABX_CODE)).
				Distinct());

			AntibioticComboBox.Tag = Antibiotics;
			AntibioticComboBox.DisplayMember = nameof(DefaultSelection.Item1);
			AntibioticComboBox.ValueMember = nameof(DefaultSelection.Item2);
			AntibioticComboBox.DataSource = Antibiotics;
		}

		#endregion

		#region Private

		private BackgroundWorker Worker;

		private readonly Tuple<string, string> DefaultSelection =
			new("[Select a value]", string.Empty);

		private static readonly string MIC_TestFormat =
			@"{0}_{1}" + Antibiotic.TestMethodCodes.MIC;

		private static readonly string DiskTestFormat =
			@"{0}_{1}" + Antibiotic.TestMethodCodes.Disk + @"{2}";

		private static readonly Regex MatchCombinationAgents =
			new(@"/.+$", RegexOptions.Compiled);

		#endregion

		#region Events

		private void InterpretFileButton_Click(object sender, EventArgs e)
		{
			ToggleUI(false);

			try
			{
				string inputFile = InputFileTextBox.Text.Trim();
				if (!Path.IsPathFullyQualified(inputFile))
					inputFile = Path.GetFullPath(Constants.SystemRootPath + inputFile);

				string configFile = ConfigFileTextBox.Text.Trim();
				if (!Path.IsPathFullyQualified(configFile))
					configFile = Path.GetFullPath(Constants.SystemRootPath + configFile);

				// Keep this one relative to the current working dir.
				// We don't have permission to put data in the install location usually.
				string outputFile = OutputFileTextBox.Text.Trim();
				if (!Path.IsPathFullyQualified(outputFile))
					outputFile = Path.GetFullPath(outputFile);

				if (!string.IsNullOrWhiteSpace(inputFile)
					&& File.Exists(inputFile)
					&& YearCheckbox.Checked
					&& !string.IsNullOrWhiteSpace(configFile)
					&& File.Exists(configFile)
					&& !string.IsNullOrWhiteSpace(outputFile))
				{
					char delimiter;
					if (FieldDelimiterComboBox.SelectedItem.ToString() == "TAB")
						delimiter = Constants.Delimiters.TabChar;
					else
						delimiter = FieldDelimiterComboBox.SelectedItem.ToString().First();

					int guidelineYear = Convert.ToInt32(GuidelineYearUpDown.Value);

					if (Worker == null)
					{
						// Setup for first use.
						Worker = new BackgroundWorker
						{
							WorkerReportsProgress = true,
							WorkerSupportsCancellation = true
						};
						Worker.DoWork += IO_Library.InterpretDataFileBounded;
						Worker.RunWorkerCompleted += HandlerInterpretationCompletion;
						Worker.ProgressChanged += ProgressMeterEventHandler;
					}

					FileInterpretationParameters args =
						new(inputFile, delimiter, guidelineYear, configFile,
						outputFile, Worker);

					Worker.RunWorkerAsync(args);
				}
				else
				{
					MessageBox.Show("Please check the file names or guideline year selection.");
					ToggleUI(true);
				}
			}
			catch
			{
				// Ensure that user control returns if there's an exception.
				ToggleUI(true);
				throw;
			}
		}

		private void HandlerInterpretationCompletion(object s, RunWorkerCompletedEventArgs e)
		{
			// No message is required if the user cancelled the operation.
			if (!e.Cancelled)
			{
				if (e.Error != null)
					MessageBox.Show(e.Error.Message);

				else
					MessageBox.Show("Interpretation completed.");
			}

			ToggleUI(true);
		}

		private void GetInerpretationsButton_Click(object sender, EventArgs e)
		{
			if (ValidateSelectionsForInterpretation(true))
			{
				// Recalculate the breakpoints based on the current option set.
				AntibioticSpecificInterpretationRules.ClearBreakpoints();

                InterpretationConfiguration interpretationConfig = new()
                {
                    IncludeInterpretationComments = IncludeInterpretationCommentsCheckbox.Checked,
                    PrioritizedBreakpointTypes = []
                };

                if (BreakpointTypesCheckbox.Checked)
					foreach (string item in BreakpointTypesCheckedListBox.CheckedItems)
						interpretationConfig.PrioritizedBreakpointTypes.Add(item);
				else
					interpretationConfig.PrioritizedBreakpointTypes.Add(Breakpoint.BreakpointTypes.Human);

				if (SitesOfInfectionCheckbox.Checked)
				{
					interpretationConfig.PrioritizedSitesOfInfection = [.. SitesOfInfectionCheckedListBox.CheckedItems.Cast<string>()];
				}

				interpretationConfig.GuidelineYear = Convert.ToInt64(GuidelineYearUpDown.Value);

				string orgCode = OrganismComboBox.SelectedValue as string;

				List<string> antibioticTestCodes = GetFullTestCodes();

				if (antibioticTestCodes.Count == 0)
				{
					MessageBox.Show("There was a problem creating the antibiotic code. Please ensure that you have either CLSI or EUCAST checked above.");
					return;
				}

				string measurement = ResultStringTextBox.Text.Trim();

				if (string.IsNullOrWhiteSpace(measurement))
				{
					MessageBox.Show("Please enter a test measurement.");
					return;
				}

				List<string> interpretations =
                    [.. antibioticTestCodes.Select(
						fullTestCode =>
						{
							string rawInterpretation =
								IsolateInterpretation.GetSingleInterpretation(interpretationConfig, orgCode, fullTestCode, measurement);

							if (!interpretationConfig.IncludeInterpretationComments)
								rawInterpretation = IsolateInterpretation.RemoveComments(rawInterpretation);

							return string.Format(@"{0}: {1}", fullTestCode, rawInterpretation);
						})];

				MessageBox.Show(string.Join(Environment.NewLine, interpretations));
			}
		}

		private void GetApplicableBreakpointsButton_Click(object sender, EventArgs e)
		{
			if (ValidateSelectionsForBreakpoints(true))
			{
				List<string> prioritizedGuidelines = null;
				List<int> prioritizedGuidelineYears = null;
				List<string> prioritizedBreakpointTypes = null;
				List<string> prioritizedSitesOfInfection = null;
				string orgCode = OrganismComboBox.SelectedValue as string;

				if (GuidelinesCheckbox.Checked)
				{
					prioritizedGuidelines = [.. SelectedGuidelinesCheckedListBox.CheckedItems.Cast<string>()];
				}

				if (BreakpointTypesCheckbox.Checked)
				{
					prioritizedBreakpointTypes = [.. BreakpointTypesCheckedListBox.CheckedItems.Cast<string>()];
				}

				if (SitesOfInfectionCheckbox.Checked)
				{
					prioritizedSitesOfInfection = [.. SitesOfInfectionCheckedListBox.CheckedItems.Cast<string>()];
				}

				if (YearCheckbox.Checked)
				{
					prioritizedGuidelineYears = [Convert.ToInt32(GuidelineYearUpDown.Value)];
				}

				if (ValidateAntibioticSelection())
				{
                    List<string> prioritizedWhonetAbxFullDrugCodes = GetFullTestCodes();

					List<Breakpoint> applicableBreakpoints =
						Breakpoint.GetApplicableBreakpoints(
							orgCode,
							[],
							prioritizedGuidelines: prioritizedGuidelines,
							prioritizedGuidelineYears: prioritizedGuidelineYears,
							prioritizedBreakpointTypes: prioritizedBreakpointTypes,
							prioritizedSitesOfInfection: prioritizedSitesOfInfection,
							prioritizedWhonetAbxFullDrugCodes: prioritizedWhonetAbxFullDrugCodes);

					using AMRIE.WinForms.BreakpointDisplay bp = new(applicableBreakpoints);
					bp.ShowDialog();
				}					
			}
			else
			{
				MessageBox.Show("One or more selections is invalid.");
			}
		}

		private void GetApplicableExpertRulesButton_Click(object sender, EventArgs e)
		{
			if (ValidateSelectionsForExpertRules())
			{
				string orgCode = OrganismComboBox.SelectedValue as string;

				string[] abxAndTests = [.. GetFullTestCodes()];
				List<string> antibioticCodes = [.. abxAndTests.Where(abx => IsolateInterpretation.ValidAntibioticCode.IsMatch(abx))];
				List<string> otherTestsCodes = [.. abxAndTests.Except(antibioticCodes)];

				List<ExpertInterpretationRule> expertRules =
					ExpertInterpretationRule.GetApplicableExpertRules(orgCode, antibioticCodes, otherTestsCodes, ExpertInterpretationRule.RuleCodes.All);

				using AMRIE.WinForms.ExpertRuleDisplay expertRuleDisplay = new(expertRules);
				expertRuleDisplay.ShowDialog();
			}
			else MessageBox.Show(Translations.Resources.OneOrMoreSelectionsIsInvalid);
		}

		private void GetApplicableIntrinsicResistanceRulesButton_Click(object sender, EventArgs e)
		{
			if (ValidateCommonSelections(true))
			{
				string orgCode = OrganismComboBox.SelectedValue as string;

				List<string> prioritizedGuidelines = null;
				if (GuidelinesCheckbox.Checked)
				{
					prioritizedGuidelines = [.. SelectedGuidelinesCheckedListBox.CheckedItems.Cast<string>()];
				}

				IEnumerable<ExpectedResistancePhenotypeRule> applicableIntrinsicRules =
					ExpectedResistancePhenotypeRule.GetApplicableExpectedResistanceRules(orgCode, prioritizedGuidelines: prioritizedGuidelines);

				using AMRIE.WinForms.IntrinsicRuleDisplay ir = new(applicableIntrinsicRules);
				ir.ShowDialog();
			}
			else
			{
				MessageBox.Show(Translations.Resources.OneOrMoreSelectionsIsInvalid);
			}
		}

		private void GuidelinesCheckbox_CheckedChanged(object sender, EventArgs e)
		{
			CheckBox cb = (CheckBox)sender;
			SelectedGuidelinesCheckedListBox.Enabled = cb.Checked;
		}

		private void YearCheckbox_CheckedChanged(object sender, EventArgs e)
		{
			CheckBox cb = (CheckBox)sender;
			GuidelineYearUpDown.Enabled = cb.Checked;
		}

		private void BreakpointTypesCheckbox_CheckedChanged(object sender, EventArgs e)
		{
			CheckBox cb = (CheckBox)sender;
			BreakpointTypesCheckedListBox.Enabled = cb.Checked;
		}

		private void SiteOfInfectionCheckbox_CheckedChanged(object sender, EventArgs e)
		{
			CheckBox cb = (CheckBox)sender;
			SitesOfInfectionCheckedListBox.Enabled = cb.Checked;
		}

		private void BrowseForInputFileButton_Click(object sender, EventArgs e)
		{
			using OpenFileDialog openFile = new();
			if (openFile.ShowDialog() == DialogResult.OK)
				InputFileTextBox.Text = openFile.FileName;
		}


		private void BrowseForConfigFileButton_Click(object sender, EventArgs e)
		{
			using OpenFileDialog openFile = new();
			if (openFile.ShowDialog() == DialogResult.OK)
				ConfigFileTextBox.Text = openFile.FileName;
		}

		private void BrowseForOutputFileButton_Click(object sender, EventArgs e)
		{
			using SaveFileDialog saveFile = new()
			{
				Filter = string.Format("{0} (*.txt)|*.txt|{1} (*.*)|*.*",
				Translations.Resources.TextFiles,
				Translations.Resources.AllFiles)
			};

			if (saveFile.ShowDialog() == DialogResult.OK)
				OutputFileTextBox.Text = saveFile.FileName;
		}

		private void ProgressMeterEventHandler(object s, ProgressChangedEventArgs e)
		{
			ProgressMeter.Value = e.ProgressPercentage;
		}

		private void Cancel_Button_Click(object sender, EventArgs e)
		{
			if (Worker != null && Worker.IsBusy)
				Worker.CancelAsync();
		}

		private void TestMethodChanged(object sender, EventArgs e)
		{
			RadioButton radioButton = sender as RadioButton;
			DiskContentComboBox.Enabled = radioButton.Checked;
		}

		private void SearchTextChanged(object sender, EventArgs e)
		{
			TextBox searchTextBox = sender as TextBox;
			ComboBox searchComboBox = searchTextBox.Tag as ComboBox;

			SearchList(searchComboBox, searchTextBox.Text);
		}

		private void AntibioticChanged(object sender, EventArgs e) 
		{
			if (AntibioticComboBox == null || AntibioticComboBox.SelectedItem == null || AntibioticComboBox.SelectedItem == DefaultSelection)
				// Clear the disk content combo box.
				DiskContentComboBox.DataSource = new List<string>();

			else
			{
				// Attempt to look up the potencies for the given drug and guidelines.
				string abxCode = 
					(AntibioticComboBox.SelectedItem as Tuple<string, string>).Item2;
				
				List<string> guidelines =
                    [.. SelectedGuidelinesCheckedListBox.CheckedItems.Cast<string>()];

				if (sender == SelectedGuidelinesCheckedListBox)
				{
					ItemCheckEventArgs itemChangeArgs = e as ItemCheckEventArgs;
					string updatedGuideline = 
						SelectedGuidelinesCheckedListBox.Items[itemChangeArgs.Index].ToString();

					if (itemChangeArgs.NewValue == CheckState.Checked)
						guidelines.Add(updatedGuideline);
					else
						guidelines.Remove(updatedGuideline);
				}

				List<string> diskContentOptions =
                    [.. Antibiotic.AllAntibiotics.Where(abx => abx.WHONET_ABX_CODE == abxCode && guidelines.Any(
						g => (g == nameof(abx.CLSI) && abx.CLSI) || (g == nameof(abx.EUCAST) && abx.EUCAST) || (g == nameof(abx.SFM)))).
					Select(abx => abx.POTENCY).
					Distinct()];

				DiskContentComboBox.DataSource = diskContentOptions;
			}
		}

		#endregion

		#region Library

		private static void SearchList(ComboBox targetControl, string searchString)
		{
			// A reference to the full list of items is stored in the control's Tag.
			List<Tuple<string, string>> fullList = 
				targetControl.Tag as List<Tuple<string, string>>;

			if (string.IsNullOrWhiteSpace(searchString))
				// No search terms. Reset the list.
				targetControl.DataSource = fullList;

			else
			{
				// Filter the items using the search terms provided by the user.
				List<string> searchTerms =
                    [.. searchString.Split(' ').
					Where(s => !string.IsNullOrWhiteSpace(s)).
					Distinct(StringComparer.InvariantCultureIgnoreCase)];

				List<Tuple<string, string>> filteredList =
                    [.. fullList.Skip(1).Where(item => searchTerms.All(s => item.Item1.Contains(s, StringComparison.InvariantCultureIgnoreCase) || item.Item2.Contains(s, StringComparison.InvariantCultureIgnoreCase)))];

				targetControl.DataSource = filteredList;
			}
		}

		private bool ValidateCommonSelections(bool organismCodeRequired)
		{
			if (GuidelinesCheckbox.Checked && SelectedGuidelinesCheckedListBox.CheckedItems.Count == 0)
			{
				MessageBox.Show("You must enable at least one antibiotic guideline.");
				return false;
			}
			else if (organismCodeRequired && !ValidateOrganismSelection())
			{
				return false;
			}
			else return true;
		}

		private bool ValidateSelectionsForBreakpoints(bool organismCodeRequired)
		{
			if (!ValidateCommonSelections(organismCodeRequired))
				return false;

			else if (BreakpointTypesCheckbox.Checked && BreakpointTypesCheckedListBox.CheckedItems.Count == 0)
			{
				MessageBox.Show("Please choose one or more breakpoint types.");
				return false;
			}				
			else if (SitesOfInfectionCheckbox.Checked && SitesOfInfectionCheckedListBox.CheckedItems.Count == 0)
			{
				MessageBox.Show("Please choose one or more sites of infection.");
				return false;
			}
			else return true;
		}

		private bool ValidateSelectionsForInterpretation(bool organismCodeRequired)
		{
			if (!ValidateSelectionsForBreakpoints(organismCodeRequired))
				return false;

			else if (!YearCheckbox.Checked)
			{
				MessageBox.Show("Please select a guideline year.");
				return false;
			}				
			else if (!ValidateAntibioticSelection())
				return false;

			else if (!ValidateTestMeasurement())
				return false;

			else return true;
		}

		private bool ValidateSelectionsForExpertRules()
		{
			return ValidateOrganismSelection() && ValidateAntibioticSelection();
		}

		private bool ValidateOrganismSelection()
		{
			if (OrganismComboBox.SelectedValue != null && OrganismComboBox.SelectedItem != DefaultSelection)
				return true;
			else
				MessageBox.Show("Please select an organism.");
		
			return false;
		}

		private bool ValidateAntibioticSelection()
		{
			if (AntibioticComboBox.SelectedValue != null && AntibioticComboBox.SelectedItem != DefaultSelection)
				return true;
			else
				MessageBox.Show("Please select an antibiotic.");

			return false;
		}

		private bool ValidateTestMeasurement()
		{
			string testMethod;
			if (DiskRadioButton.Checked)
				testMethod = Antibiotic.TestMethods.Disk;
			else
				testMethod = Antibiotic.TestMethods.MIC;

			decimal discardedNumericResult = decimal.Zero;
			string discardedResultModifier = string.Empty;

			if (InterpretationLibrary.ParseResult(testMethod, ResultStringTextBox.Text,
				ref discardedNumericResult, ref discardedResultModifier))
				return true;
			else
				MessageBox.Show("The test measurement could not be read. Please verify the input.");

			return false;
		}

		private void ToggleUI(bool enabled)
		{
			YearCheckbox.Enabled = enabled;
			GuidelineYearUpDown.Enabled = enabled;
			Cancel_Button.Enabled = !enabled;
			ModeTabControl.Enabled = enabled;
		}

		private List<string> GetFullTestCodes()
		{
			List<string> guidelines =
                [.. SelectedGuidelinesCheckedListBox.CheckedItems.Cast<string>()];

			string selectedAntibioticCode =
				AntibioticComboBox.SelectedValue as string;

			string diskContent = string.Empty;
			if (DiskRadioButton.Checked && DiskContentComboBox.SelectedValue != null)
				diskContent = DiskContentComboBox.SelectedValue as string;

			return [.. guidelines.
				Select(g => Create_FullTestCode(g, selectedAntibioticCode, DiskRadioButton.Checked, diskContent)).
				Where(fullCode => !string.IsNullOrWhiteSpace(fullCode))];
		}

		private static string Create_FullTestCode(string guidelineName, string antibioticCode, bool diskTest, string diskContent = "")
		{
			char guidelineCode;
			switch (guidelineName)
			{
				case nameof(Antibiotic.GuidelineNames.CLSI):
					guidelineCode = Antibiotic.GuidelineCodes.CLSI;
					break;

				case nameof(Antibiotic.GuidelineNames.EUCAST):
					guidelineCode = Antibiotic.GuidelineCodes.EUCAST;
					break;

				case nameof(Antibiotic.GuidelineNames.SFM):
					guidelineCode = Antibiotic.GuidelineCodes.SFM;
					break;

				default:
					// Other guidelines not supported on screen at this time.
					// They continue to work through the library interpretations,
					// and could be added to this interface. They are rare enough
					// that we have left them off avoid clutter that is not relevant for most users.
					return null;
			}

			if (diskTest)
			{
				if (!string.IsNullOrWhiteSpace(diskContent))
				{
					// Convert the disk content values from the antibiotic table
					// into the format used by the breakpoints/test codes.
					diskContent = diskContent.
						Replace("µg", string.Empty).
						Replace("units", string.Empty).
						Replace(".", "_");

					// If the agent has two components separated by a "/",
					// truncate the second part for use in the test code.
					if (MatchCombinationAgents.IsMatch(diskContent))
						diskContent =
							MatchCombinationAgents.Replace(diskContent, string.Empty);

					// Important special case for "1_25" which should result in "1_2" as in "SXT_ND1_2".
					if (diskContent == "1_25")
						diskContent = "1_2";
				}

				return string.Format(DiskTestFormat, antibioticCode, guidelineCode, diskContent);
			}
			else return string.Format(MIC_TestFormat, antibioticCode, guidelineCode);
		}				

		#endregion
	}
}
