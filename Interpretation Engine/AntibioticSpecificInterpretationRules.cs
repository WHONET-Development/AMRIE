using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace AMR_Engine
{
	public class AntibioticSpecificInterpretationRules
	{
		#region Init

		/// <summary>
		/// Populates the intrinsic resistance rules and breakpoints associated with the drug/bug combination.
		/// Use the lookup tables to collect this information so that we only need to make the determinations once.
		/// </summary>
		/// <param name="whonetOrganismCode"></param>
		/// <param name="whonetAntimicrobialFullCode"></param>
		/// <param name="antimicrobialResult_"></param>
		/// <param name="userDefinedBreakpoints"></param>
		/// <param name="guidelineYear"></param>
		/// <param name="roundHalfDilutions"></param>
		/// <param name="prioritizedBreakpointTypes"></param>
		/// <param name="prioritizedSitesOfInfection"></param>
		public AntibioticSpecificInterpretationRules(
			string whonetOrganismCode,
			string whonetAntimicrobialFullCode,
			string antimicrobialResult_,
			List<Breakpoint> userDefinedBreakpoints,
			int guidelineYear = -1,
			bool roundHalfDilutions = true,
			List<string> prioritizedBreakpointTypes = null,
			List<string> prioritizedSitesOfInfection = null)
		{
			RoundHalfDilutions = roundHalfDilutions;

			AntibioticComponents thisAntibiotic = new AntibioticComponents(whonetAntimicrobialFullCode);
			AntimicrobialTestMethod = thisAntibiotic.TestMethod;

			AntimicrobialResult = antimicrobialResult_;

			InterpretationLibrary.ParseResult(AntimicrobialTestMethod, AntimicrobialResult, ref NumericResult, ref ResultModifier);

			lock (IntrinsicResistanceRuleLookupLock)
			{
				if (IntrinsicResistanceRuleLookup.ContainsKey(whonetOrganismCode)
				&& IntrinsicResistanceRuleLookup[whonetOrganismCode].ContainsKey(thisAntibiotic.Guideline)
				&& IntrinsicResistanceRuleLookup[whonetOrganismCode][thisAntibiotic.Guideline].ContainsKey(thisAntibiotic.Code))
					// We have seen this combination previously, so there no need for us to determine the applicable intrinsic resistance rules again.
					MostApplicableIntrinsicResistanceRule = IntrinsicResistanceRuleLookup[whonetOrganismCode][thisAntibiotic.Guideline][thisAntibiotic.Code];
				else
				{
					// We haven't seen this combination before, so we need to evaluate it.
					MostApplicableIntrinsicResistanceRule =
						ExpectedResistancePhenotypeRule.GetApplicableExpectedResistanceRules(
							whonetOrganismCode,
							new List<string>() { thisAntibiotic.Guideline },
							new List<string> { thisAntibiotic.Code }).FirstOrDefault();

					if (IntrinsicResistanceRuleLookup.ContainsKey(whonetOrganismCode))
					{
						if (IntrinsicResistanceRuleLookup[whonetOrganismCode].ContainsKey(thisAntibiotic.Guideline))
						{
							// Only missing the antibiotic part.
							IntrinsicResistanceRuleLookup[whonetOrganismCode][thisAntibiotic.Guideline].Add(thisAntibiotic.Code, MostApplicableIntrinsicResistanceRule);
						}
						else
						{
							Dictionary<string, ExpectedResistancePhenotypeRule> abxSet = new Dictionary<string, ExpectedResistancePhenotypeRule>
                            {
                                { thisAntibiotic.Code, MostApplicableIntrinsicResistanceRule }
                            };

							IntrinsicResistanceRuleLookup[whonetOrganismCode].Add(thisAntibiotic.Guideline, abxSet);
						}
					}
					else
					{
						// The organism code missing, which means we need the whole structure.
						Dictionary<string, ExpectedResistancePhenotypeRule> abxSet = new Dictionary<string, ExpectedResistancePhenotypeRule>
                        {
                            { thisAntibiotic.Code, MostApplicableIntrinsicResistanceRule }
                        };

						Dictionary<string, Dictionary<string, ExpectedResistancePhenotypeRule>> guidelineSet = new Dictionary<string, Dictionary<string, ExpectedResistancePhenotypeRule>>
                        {
                            { thisAntibiotic.Guideline, abxSet }
                        };

						IntrinsicResistanceRuleLookup.Add(whonetOrganismCode, guidelineSet);
					}
				}
			}

			if (NumericResult > 0M)
			{
				MostApplicableBreakpoint =
					DetermineMostApplicableBreakpoint(
						userDefinedBreakpoints, guidelineYear, prioritizedBreakpointTypes, prioritizedSitesOfInfection,
						whonetOrganismCode, thisAntibiotic.Guideline, whonetAntimicrobialFullCode);
			}
		}

		#endregion

		#region Properties

		private readonly string AntimicrobialTestMethod;

		private readonly string AntimicrobialResult;

		private readonly string ResultModifier;

		private readonly decimal NumericResult;

		private readonly bool RoundHalfDilutions;

		private readonly ExpectedResistancePhenotypeRule MostApplicableIntrinsicResistanceRule;

		private static readonly Dictionary<string, Dictionary<string, Dictionary<string, ExpectedResistancePhenotypeRule>>> IntrinsicResistanceRuleLookup = new Dictionary<string, Dictionary<string, Dictionary<string, ExpectedResistancePhenotypeRule>>>();

		private static readonly object IntrinsicResistanceRuleLookupLock = new object();

		private readonly Breakpoint MostApplicableBreakpoint;

		private static readonly Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, Breakpoint>>>> BreakpointLookup =
			new Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, Breakpoint>>>>();

		private static readonly object BreakpointLookupModificationLock = new object();

		#endregion

		#region Public

		public static void ClearBreakpoints()
		{
			lock (BreakpointLookupModificationLock)
			{
				BreakpointLookup.Clear();
			}
		}

		/// <summary>
		/// Prepopulate the breakpoint cache with the "MostApplicableBreakpoint" for each combination provided.
		/// This will greatly improve performance rather than looking them up ad hoc during processing because
		/// the BreakpointLookup has to be locked (blocking the other threads) during lookup.
		/// </summary>
		/// <param name="distinctInterpretationKeys"></param>
		public static void PreheatBreakpointCache(
			List<Breakpoint> userDefinedBreakpoints,
			int guidelineYear,
			List<string> prioritizedBreakpointTypes,
			List<string> prioritizedSitesOfInfection,
			List<Tuple<string, string, string>> distinctInterpretationKeys,
			BackgroundWorker worker = null)
		{
			// We don't need to look up keys that already exist in the cache.
			distinctInterpretationKeys =
				distinctInterpretationKeys.
				Where(k => !(BreakpointLookup.ContainsKey(k.Item1)
				&& BreakpointLookup[k.Item1].ContainsKey(k.Item2)
				&& BreakpointLookup[k.Item1][k.Item2].ContainsKey(guidelineYear)
				&& BreakpointLookup[k.Item1][k.Item2][guidelineYear].ContainsKey(k.Item3))).
				ToList();

			object syncObject = new();
			int totalKeys = distinctInterpretationKeys.Count();
			int lastReportedProgress = 0;
			int completedKeys = 0;

			Parallel.ForEach(
				distinctInterpretationKeys,
				key =>
			{
				// Evaluating the breakpoint for the side effect that the cache will be populated.
				// We don't need a reference to the breakpoint itself here.
				DetermineMostApplicableBreakpoint(
					userDefinedBreakpoints,
					guidelineYear,
					prioritizedBreakpointTypes,
					prioritizedSitesOfInfection,
					key.Item1, key.Item2, key.Item3);

				// Update the UI's progress
				if (worker != null)
				{
					lock (syncObject)
					{
						completedKeys++;
						int currentProgress = (completedKeys * 100) / totalKeys;

						if (currentProgress > lastReportedProgress)
						{
							lastReportedProgress = currentProgress;
							worker.ReportProgress(lastReportedProgress);
						}
					}
				}
			});
		}

		/// <summary>
		/// Apply any matching intrinsic resistance rules and breakpoints for this result.
		/// </summary>
		/// <returns></returns>
		public string GetInterpretation()
		{
			string intrinsicResistanceInterp = ApplyIntrinsicResistanceRules();

			if (intrinsicResistanceInterp == Constants.InterpretationCodes.Uninterpretable)
				// The drug/bug pair not intrinsically resistant. Apply the breakpoints.
				return ApplyBreakpoints();
			else
				return intrinsicResistanceInterp;
		}

		#endregion

		#region Private

		/// <summary>
		/// If this drug/bug matches an intrinsic resistance rule, then we don't need to evaluate breakpoints.
		/// </summary>
		/// <returns></returns>
		private string ApplyIntrinsicResistanceRules()
		{
			if (MostApplicableIntrinsicResistanceRule != null)
				// This isolate intrinsically resistant. We don't need to apply the breakpoints.
				return Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.Asterisk;

			else
				return Constants.InterpretationCodes.Uninterpretable;
		}

		/// <summary>
		/// Evaluate the numeric measurement against the most-applicable breakpoint as determined by the users configuration and breakpoint hierarchy.
		/// </summary>
		/// <returns></returns>
		private string ApplyBreakpoints()
		{
			if (NumericResult > 0M)
			{
				if (MostApplicableBreakpoint != null)
				{
					if (MostApplicableBreakpoint.BREAKPOINT_TYPE == Breakpoint.BreakpointTypes.ECOFF)
					{
						// Use the ECOFF values instead of the R, I, S columns.
						switch (AntimicrobialTestMethod)
						{
							case Antibiotic.TestMethods.Disk:
								if (NumericResult >= MostApplicableBreakpoint.ECV_ECOFF)
									return Constants.InterpretationCodes.WildType;
								else
									return Constants.InterpretationCodes.NonWildType;

							case Antibiotic.TestMethods.MIC:
								// ETest also uses the MIC breakpoints.
								decimal tempNumericResult;
								if (RoundHalfDilutions)
									tempNumericResult = InterpretationLibrary.Round_ETestHalfDilutionsUp(NumericResult);
								else
									tempNumericResult = NumericResult;

								if (string.IsNullOrWhiteSpace(ResultModifier))
								{
									// No > or < symbol involved.
									if (tempNumericResult <= MostApplicableBreakpoint.ECV_ECOFF)
										return Constants.InterpretationCodes.WildType;

									else
										return Constants.InterpretationCodes.NonWildType;
								}
								else
								{
									// Must take the > or < into account.
									if (ResultModifier.StartsWith(Constants.MeasurementModifiers.GreaterThan))
									{
										if (tempNumericResult > MostApplicableBreakpoint.ECV_ECOFF)
											// Must include the question mark because of the > symbol.
											return Constants.InterpretationCodes.NonWildType;
										else
											return Constants.InterpretationCodes.NonWildType + Constants.InterpretationCodes.QuestionMark;
									}
									else
									{
										// Modifier begins with less than symbol.
										if (tempNumericResult <= MostApplicableBreakpoint.ECV_ECOFF)
											return Constants.InterpretationCodes.WildType;
										else
											// Must include the question mark because of the < symbol.
											return Constants.InterpretationCodes.WildType + Constants.InterpretationCodes.QuestionMark;
									}
								}
						}
					}
					else
					{
						// Normal human or animal breakpoint.
						switch (AntimicrobialTestMethod)
						{
							case Antibiotic.TestMethods.Disk:
								if (MostApplicableBreakpoint.S > 0M)
								{
									if (NumericResult <= MostApplicableBreakpoint.R)
										return Constants.InterpretationCodes.Resistant;

									else if (NumericResult >= MostApplicableBreakpoint.S)
										return Constants.InterpretationCodes.Susceptible;

									else if (MostApplicableBreakpoint.R > 0M)
										if (!string.IsNullOrEmpty(MostApplicableBreakpoint.I))
											return Constants.InterpretationCodes.Intermediate;
										else
											return Constants.InterpretationCodes.SusceptibleDoseDependent;
									else
										return Constants.InterpretationCodes.NonSusceptible;
								}
								break;

							case Antibiotic.TestMethods.MIC:
								// ETest also uses the MIC breakpoints.
								if (MostApplicableBreakpoint.S > 0M)
								{
									decimal tempNumericResult;
									if (RoundHalfDilutions)
										tempNumericResult = InterpretationLibrary.Round_ETestHalfDilutionsUp(NumericResult);
									else
										tempNumericResult = NumericResult;

									if (string.IsNullOrWhiteSpace(ResultModifier) || ResultModifier == Constants.MeasurementModifiers.EqualsSign)
									{
										// No > or < symbol involved.
										if (tempNumericResult <= MostApplicableBreakpoint.S)
											return Constants.InterpretationCodes.Susceptible;

										else if (tempNumericResult >= MostApplicableBreakpoint.R)
										{
											if (MostApplicableBreakpoint.R > 0M)
												return Constants.InterpretationCodes.Resistant;
											else
												return Constants.InterpretationCodes.NonSusceptible;
										}
										else
										{
											if (!string.IsNullOrEmpty(MostApplicableBreakpoint.I))
												return Constants.InterpretationCodes.Intermediate;
											else
												return Constants.InterpretationCodes.SusceptibleDoseDependent;
										}
									}
									else
									{
										// Must take the > or < into account.
										if (ResultModifier.StartsWith(Constants.MeasurementModifiers.GreaterThan))
										{
											tempNumericResult *= 2M;
											if (tempNumericResult >= MostApplicableBreakpoint.R)
											{
												if (MostApplicableBreakpoint.R > 0M)
													return Constants.InterpretationCodes.Resistant;
												else
												{
													// No "R" breakpoint.
													if (tempNumericResult > MostApplicableBreakpoint.S)
														return Constants.InterpretationCodes.NonSusceptible;

													else
														// Must include the question mark because of the > symbol.
														return Constants.InterpretationCodes.NonSusceptible + Constants.InterpretationCodes.QuestionMark;
												}
											}
											else
											{
												if (tempNumericResult > MostApplicableBreakpoint.S)
													return Constants.InterpretationCodes.NonSusceptible;

												else
													// Must include the question mark because of the > symbol.
													return Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.QuestionMark;
											}
										}
										else
										{
											// Modifier begins with less than symbol.
											if (tempNumericResult <= MostApplicableBreakpoint.S)
												return Constants.InterpretationCodes.Susceptible;

											else if (MostApplicableBreakpoint.S == Constants.MIC.MinimumMIC_Measurement)
												if (tempNumericResult >= MostApplicableBreakpoint.R)
												{
													if (!string.IsNullOrEmpty(MostApplicableBreakpoint.I))
														return Constants.InterpretationCodes.Intermediate + Constants.InterpretationCodes.QuestionMark;
													else
														return Constants.InterpretationCodes.SusceptibleDoseDependent + Constants.InterpretationCodes.QuestionMark;
												}
												else
												{
													if (!string.IsNullOrEmpty(MostApplicableBreakpoint.I))
														return Constants.InterpretationCodes.Intermediate;
													else
														return Constants.InterpretationCodes.SusceptibleDoseDependent;
												}
											else
												// Must include the question mark because of the < symbol.
												return Constants.InterpretationCodes.Susceptible + Constants.InterpretationCodes.QuestionMark;
										}
									}
								}
								break;
						}
					}
				}
			}

			return Constants.InterpretationCodes.Uninterpretable;
		}

		private static Breakpoint DetermineMostApplicableBreakpoint(
			List<Breakpoint> userDefinedBreakpoints,
			int guidelineYear,
			List<string> prioritizedBreakpointTypes,
			List<string> prioritizedSitesOfInfection,
			string whonetOrganismCode,
			string guideline,
			string whonetAntimicrobialFullCode)
		{
			// Create the missing levels in our lookup for next time, and store this breakpoint set.
			if (BreakpointLookup.ContainsKey(whonetOrganismCode)
				&& BreakpointLookup[whonetOrganismCode].ContainsKey(guideline)
				&& BreakpointLookup[whonetOrganismCode][guideline].ContainsKey(guidelineYear)
				&& BreakpointLookup[whonetOrganismCode][guideline][guidelineYear].ContainsKey(whonetAntimicrobialFullCode))
			{
				// We have seen this combination previously, so retrieve it from the cache.
				return BreakpointLookup[whonetOrganismCode][guideline][guidelineYear][whonetAntimicrobialFullCode];
			}
			else
			{
				lock (BreakpointLookupModificationLock)
				{
					// We have to check if the breakpoint exists once again now that we are inside the lock to avoid potential race conditions
					// while preserving fast (lockless) lookups that are cache hits while avoiding a duplicate lookup.
					if (BreakpointLookup.ContainsKey(whonetOrganismCode)
						&& BreakpointLookup[whonetOrganismCode].ContainsKey(guideline)
						&& BreakpointLookup[whonetOrganismCode][guideline].ContainsKey(guidelineYear)
						&& BreakpointLookup[whonetOrganismCode][guideline][guidelineYear].ContainsKey(whonetAntimicrobialFullCode))
					{
						// We have seen this combination previously, so retrieve it from the cache and return.
						return BreakpointLookup[whonetOrganismCode][guideline][guidelineYear][whonetAntimicrobialFullCode];
					}

					// We haven't seen this combination before, so we need to evaluate it.
					// If there is no breakpoint matching these requirements, then we will return Null here
					// and save that value for future lookups to indicate that there is no applicable breakpoint.
					Breakpoint mostApplicableBreakpoint =
						Breakpoint.GetApplicableBreakpoints(
							whonetOrganismCode,
							userDefinedBreakpoints,
							prioritizedGuidelines: new List<string>() { guideline },
							prioritizedGuidelineYears: new List<int> { guidelineYear },
							prioritizedBreakpointTypes: prioritizedBreakpointTypes,
							prioritizedSitesOfInfection: prioritizedSitesOfInfection,
							prioritizedWhonetAbxFullDrugCodes: new List<string>() { whonetAntimicrobialFullCode },
							returnFirstBreakpointOnly: true).FirstOrDefault();

					if (BreakpointLookup.ContainsKey(whonetOrganismCode))
					{
						if (BreakpointLookup[whonetOrganismCode].ContainsKey(guideline))
						{
							if (BreakpointLookup[whonetOrganismCode][guideline].ContainsKey(guidelineYear))
							{
								// Only the antibiotic info missing.
								BreakpointLookup[whonetOrganismCode][guideline][guidelineYear].Add(whonetAntimicrobialFullCode, mostApplicableBreakpoint);
							}
							else
							{
								Dictionary<string, Breakpoint> abxSet = new Dictionary<string, Breakpoint>
                                {
                                    { whonetAntimicrobialFullCode, mostApplicableBreakpoint }
                                };

								BreakpointLookup[whonetOrganismCode][guideline].Add(guidelineYear, abxSet);
							}
						}
						else
						{
							// Create everything below the organism.
							Dictionary<string, Breakpoint> abxSet = new Dictionary<string, Breakpoint>
                            {
                                { whonetAntimicrobialFullCode, mostApplicableBreakpoint }
                            };

							Dictionary<int, Dictionary<string, Breakpoint>> yearSet = new Dictionary<int, Dictionary<string, Breakpoint>>
                            {
                                { guidelineYear, abxSet }
                            };

							BreakpointLookup[whonetOrganismCode].Add(guideline, yearSet);
						}
					}
					else
					{
						// The organism key missing, which means we have to create the whole structure.
						Dictionary<string, Breakpoint> abxSet = new Dictionary<string, Breakpoint>
                        {
                            { whonetAntimicrobialFullCode, mostApplicableBreakpoint }
                        };

						Dictionary<int, Dictionary<string, Breakpoint>> yearSet = new Dictionary<int, Dictionary<string, Breakpoint>>
                        {
                            { guidelineYear, abxSet }
                        };

						Dictionary<string, Dictionary<int, Dictionary<string, Breakpoint>>> guidelineSet = new Dictionary<string, Dictionary<int, Dictionary<string, Breakpoint>>>
                        {
                            { guideline, yearSet }
                        };

						BreakpointLookup.Add(whonetOrganismCode, guidelineSet);
					}

					return mostApplicableBreakpoint;
				}
			}
		}

		#endregion

	}
}
