using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.Linq;

namespace AMR_Engine
{
	/// <summary>
	/// Container and methods associated with the configuration for the interpretation engine.
	/// </summary>
	public class InterpretationConfiguration
	{

		public bool RoundHalfDilutions { get; set; } = true;
		public bool IncludeInterpretationComments { get; set; } = false;
		public bool UseIntrinsicResistanceRules { get; set; } = true;
		public List<string> EnabledExpertInterpretationRules { get; set; }
		public long GuidelineYear { get; set; }
		public List<string> PrioritizedBreakpointTypes { get; set; }
		public List<string> PrioritizedSitesOfInfection { get; set; }
		public List<string> DisabledSitesOfInfection { get; set; }
		public bool HorizontalAntibioticResults { get; set; } = true;
		public string UserDefinedBreakpointsFile { get; set; }

		public List<Breakpoint> UserDefinedBreakpoints = new List<Breakpoint>();

		public InterpretationConfiguration() { }

		/// <summary>
		/// Setup the configuration object.
		/// </summary>
		/// <param name="roundHalfDilutions"></param>
		/// <param name="includeInterpretationComments"></param>
		/// <param name="enabledExpertInterpretationRules"></param>
		/// <param name="guidelineYear"></param>
		/// <param name="prioritizedBreakpointTypes"></param>
		/// <param name="prioritizedSitesOfInfection"></param>
		/// <param name="disabledSitesOfInfection"></param>
		/// <param name="userDefinedBreakpointsFile"></param>
		public InterpretationConfiguration(bool roundHalfDilutions, bool includeInterpretationComments,
			List<string> enabledExpertInterpretationRules, long guidelineYear,
			List<string> prioritizedBreakpointTypes,
			List<string> prioritizedSitesOfInfection,
			List<string> disabledSitesOfInfection,
			string userDefinedBreakpointsFile)
		{
			RoundHalfDilutions = roundHalfDilutions;
			IncludeInterpretationComments = includeInterpretationComments;
			GuidelineYear = guidelineYear;

			// Leave these null when the lists are empty.
			if (enabledExpertInterpretationRules != null && enabledExpertInterpretationRules.Count != 0)
				EnabledExpertInterpretationRules = enabledExpertInterpretationRules;

			if (prioritizedBreakpointTypes != null && prioritizedBreakpointTypes.Count != 0)
				PrioritizedBreakpointTypes = prioritizedBreakpointTypes;

			if (prioritizedSitesOfInfection != null && prioritizedSitesOfInfection.Count != 0)
				PrioritizedSitesOfInfection = prioritizedSitesOfInfection;

			if (disabledSitesOfInfection != null && disabledSitesOfInfection.Count != 0)
				DisabledSitesOfInfection = disabledSitesOfInfection;

			UserDefinedBreakpointsFile = userDefinedBreakpointsFile;

			if (!string.IsNullOrWhiteSpace(UserDefinedBreakpointsFile) && File.Exists(UserDefinedBreakpointsFile))
				UserDefinedBreakpoints = Breakpoint.LoadBreakpoints(UserDefinedBreakpointsFile, true);

			UpdateSitesOfInfection();
		}

		/// <summary>
		/// Read a configuration JSON file into this container object.
		/// </summary>
		/// <param name="configFile"></param>
		/// <returns></returns>
		public static InterpretationConfiguration ReadConfiguration(string configFile)
		{
			DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(InterpretationConfiguration));

			InterpretationConfiguration config;
			using (StreamReader configReader = new StreamReader(configFile))
			{
				config = (InterpretationConfiguration)deserializer.ReadObject(configReader.BaseStream);
			}

			if (config.EnabledExpertInterpretationRules != null && config.EnabledExpertInterpretationRules.Count == 0)
				config.EnabledExpertInterpretationRules = null;

			if (config.PrioritizedBreakpointTypes != null && config.PrioritizedBreakpointTypes.Count == 0)
				config.PrioritizedBreakpointTypes = null;

			if (config.PrioritizedSitesOfInfection != null && config.PrioritizedSitesOfInfection.Count == 0)
				config.PrioritizedSitesOfInfection = null;

			if (!string.IsNullOrWhiteSpace(config.UserDefinedBreakpointsFile) && File.Exists(string.Format("{0}{1}", Constants.SystemRootPath, config.UserDefinedBreakpointsFile)))
				config.UserDefinedBreakpoints = 
					Breakpoint.LoadBreakpoints(
						string.Format("{0}{1}", Constants.SystemRootPath, config.UserDefinedBreakpointsFile), 
						true);

			config.UpdateSitesOfInfection();

			return config;
		}

		/// <summary>
		/// The default configuration options.
		/// </summary>
		/// <returns></returns>
		public static InterpretationConfiguration DefaultConfiguration()
		{
			List<string> defaultExpertInterpretationRules =
				new List<string> {
					ExpertInterpretationRule.RuleCodes.MRStaph,
					ExpertInterpretationRule.RuleCodes.BLNAR,
					ExpertInterpretationRule.RuleCodes.ICR
				};

			List<string> defaultBreakpointTypes = new List<string> { Breakpoint.BreakpointTypes.Human };

			// Generate an ordered list of the available sites of infection.
			List<string> defaultSitesOfInfection =
				Constants.SitesOfInfection.DefaultOrder.ToList();

			return new InterpretationConfiguration(true, true, defaultExpertInterpretationRules,
				Constants.BreakpointTableRevisionYear, defaultBreakpointTypes, defaultSitesOfInfection, new List<string>(), string.Empty);
		}

		/// <summary>
		/// Create a copy of this configuration.
		/// </summary>
		/// <returns></returns>
		public InterpretationConfiguration Clone()
		{
            InterpretationConfiguration newConfig
                = new InterpretationConfiguration(RoundHalfDilutions, IncludeInterpretationComments,
                EnabledExpertInterpretationRules == null ? null : new List<string>(EnabledExpertInterpretationRules),
                GuidelineYear,
                PrioritizedBreakpointTypes == null ? null : new List<string>(PrioritizedBreakpointTypes),
                PrioritizedSitesOfInfection == null ? null : new List<string>(PrioritizedSitesOfInfection),
                DisabledSitesOfInfection == null ? null : new List<string>(DisabledSitesOfInfection),
                UserDefinedBreakpointsFile)
                {
                    UseIntrinsicResistanceRules = UseIntrinsicResistanceRules
                };

            return newConfig;
		}

		/// <summary>
		/// Makes sure that the DisabledSitesOfInfection are respected, and that new sites of infection are included.
		/// </summary>
		public void UpdateSitesOfInfection()
		{
			if (PrioritizedSitesOfInfection != null)
			{
				// Remove invalid sites of infection.
				PrioritizedSitesOfInfection =
					PrioritizedSitesOfInfection.Intersect(Constants.SitesOfInfection.DefaultOrder).ToList();

				if (DisabledSitesOfInfection != null)
				{
					// Remove invalid sites of infection from the disabled list.
					DisabledSitesOfInfection =
						DisabledSitesOfInfection.Intersect(Constants.SitesOfInfection.DefaultOrder).ToList();

					// Append any new sites of infection.
					PrioritizedSitesOfInfection =
						PrioritizedSitesOfInfection.Concat(
							Constants.SitesOfInfection.DefaultOrder.
							Except(PrioritizedSitesOfInfection).
							Except(DisabledSitesOfInfection)).ToList();
				}
				else
				{
					// Append any new codes which aren't on the list. There is no disabled list.
					PrioritizedSitesOfInfection =
						PrioritizedSitesOfInfection.
						Concat(
							Constants.SitesOfInfection.DefaultOrder.
							Except(PrioritizedSitesOfInfection)).ToList();
				}
			}
		}
	}
}
