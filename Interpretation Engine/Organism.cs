using Microsoft.VisualBasic.FileIO;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AMR_Engine
{
    public class Organism
    {
        #region Constants

        private const string CurrentOrgCode = "C";

        private const string ReplacedByColumnName = "REPLACED_BY";

        public static readonly List<Organism> AllOrganisms = LoadAllOrganisms();

        public static readonly Dictionary<string, Organism> CurrentOrganisms = LoadCurrentOrganisms();

        public static readonly Dictionary<string, string> MergedOrganisms = LoadMergedOrganisms();

        #endregion

        #region Init

        /// <summary>
        /// Used only to support static nameof()
        /// </summary>
        public Organism() { }

        /// <summary>
        /// Setup one organism from the organism list.
        /// </summary>
        /// <param name="WHONET_ORG_CODE_"></param>
        /// <param name="ORGANISM_"></param>
        /// <param name="TAXONOMIC_STATUS_"></param>
        /// <param name="COMMON_"></param>
        /// <param name="ORGANISM_TYPE_"></param>
        /// <param name="ANAEROBE_"></param>
        /// <param name="MORPHOLOGY_"></param>
        /// <param name="SUBKINGDOM_CODE_"></param>
        /// <param name="FAMILY_CODE_"></param>
        /// <param name="GENUS_GROUP_"></param>
        /// <param name="GENUS_CODE_"></param>
        /// <param name="SPECIES_GROUP_"></param>
        /// <param name="SEROVAR_GROUP_"></param>
        /// <param name="SCT_CODE_"></param>
        /// <param name="SCT_TEXT_"></param>
        /// <param name="GBIF_TAXON_ID_"></param>
        /// <param name="GBIF_DATASET_ID_"></param>
        /// <param name="GBIF_TAXONOMIC_STATUS_"></param>
        /// <param name="KINGDOM_"></param>
        /// <param name="PHYLUM_"></param>
        /// <param name="CLASS_"></param>
        /// <param name="ORDER_"></param>
        /// <param name="FAMILY_"></param>
        /// <param name="GENUS_"></param>
        private Organism(string WHONET_ORG_CODE_, string ORGANISM_, string TAXONOMIC_STATUS_, bool COMMON_, bool COMMON_COMMENSAL_,
            string ORGANISM_TYPE_, bool ANAEROBE_, string MORPHOLOGY_, string SUBKINGDOM_CODE_, string FAMILY_CODE_,
            string GENUS_GROUP_, string GENUS_CODE_, string SPECIES_GROUP_, string SEROVAR_GROUP_, string SCT_CODE_,
            string SCT_TEXT_, string GBIF_TAXON_ID_, string GBIF_DATASET_ID_,
            string GBIF_TAXONOMIC_STATUS_, string KINGDOM_, string PHYLUM_, string CLASS_, string ORDER_, string FAMILY_, string GENUS_)
        {
            WHONET_ORG_CODE = WHONET_ORG_CODE_;
            ORGANISM = ORGANISM_;
            TAXONOMIC_STATUS = TAXONOMIC_STATUS_;
            COMMON = COMMON_;
            COMMON_COMMENSAL = COMMON_COMMENSAL_;
            ORGANISM_TYPE = ORGANISM_TYPE_;
            ANAEROBE = ANAEROBE_;
            MORPHOLOGY = MORPHOLOGY_;
            SUBKINGDOM_CODE = SUBKINGDOM_CODE_;
            FAMILY_CODE = FAMILY_CODE_;
            GENUS_GROUP = GENUS_GROUP_;
            GENUS_CODE = GENUS_CODE_;
            SPECIES_GROUP = SPECIES_GROUP_;
            SEROVAR_GROUP = SEROVAR_GROUP_;
            SCT_CODE = SCT_CODE_;
            SCT_TEXT = SCT_TEXT_;
            GBIF_TAXON_ID = GBIF_TAXON_ID_;
            GBIF_DATASET_ID = GBIF_DATASET_ID_;
            GBIF_TAXONOMIC_STATUS = GBIF_TAXONOMIC_STATUS_;
            KINGDOM = KINGDOM_;
            PHYLUM = PHYLUM_;
            CLASS = CLASS_;
            ORDER = ORDER_;
            FAMILY = FAMILY_;
            GENUS = GENUS_;
        }

        #endregion

        #region Properties

        public string WHONET_ORG_CODE;
        public readonly string ORGANISM;
        public readonly string TAXONOMIC_STATUS;
        public readonly bool COMMON;
        public readonly bool COMMON_COMMENSAL;
        public readonly string ORGANISM_TYPE;
        public readonly bool ANAEROBE;
        public readonly string MORPHOLOGY;
        public readonly string SUBKINGDOM_CODE;
        public readonly string FAMILY_CODE;
        public readonly string GENUS_GROUP;
        public readonly string GENUS_CODE;
        public readonly string SPECIES_GROUP;
        public readonly string SEROVAR_GROUP;
        public readonly string SCT_CODE;
        public readonly string SCT_TEXT;
        public readonly string GBIF_TAXON_ID;
        public readonly string GBIF_DATASET_ID;
        public readonly string GBIF_TAXONOMIC_STATUS;
        public readonly string KINGDOM;
        public readonly string PHYLUM;
        public readonly string CLASS;
        public readonly string ORDER;
        public readonly string FAMILY;
        public readonly string GENUS;

        #endregion

        #region Private

		/// <summary>
		/// Loads all organisms, including those with nomenclature changes, into a list.
		/// This full list is not used by the AMR system directly, except that it is
		/// filtered down to the CurrentOrganisms (which is used by the AMR system).
		/// The full AllOrganisms list is used primarily by 3rd party applications,
		/// such as WHONET and BacLink, which need to know about nomenclature changes
		/// that are not relevant for the AMR Engine's work. The AMR system only deals
		/// with the codes, which don't change, ignoring the full text names which would
		/// need to be known for mapping and searching purposes in the other applications.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="FileNotFoundException"></exception>
		private static List<Organism> LoadAllOrganisms()
		{
			string organismsTableFile;
			string relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), "Resources", "Organisms.txt");
			if (string.IsNullOrWhiteSpace(Constants.SystemRootPath))
				organismsTableFile = relativePath;
			else
				organismsTableFile = string.Join(Path.DirectorySeparatorChar.ToString(), Constants.SystemRootPath, relativePath);

            if (File.Exists(organismsTableFile))
            {
                List<Organism> allOrgs = new List<Organism>();

                using (TextFieldParser parser = new TextFieldParser(organismsTableFile, System.Text.Encoding.UTF8))
                {
                    parser.SetDelimiters(Constants.Delimiters.TabChar.ToString());
                    parser.HasFieldsEnclosedInQuotes = true;

                    string[] headers = parser.ReadFields();
                    Dictionary<string, int> headerMap = IO_Library.GetResourceHeaders(headers);

                    while (!parser.EndOfData)
                    {
                        string[] values = parser.ReadFields();

                        string taxonomicStatus = values[headerMap[nameof(TAXONOMIC_STATUS)]];

                        Organism newOrganism = new Organism(
                            values[headerMap[nameof(WHONET_ORG_CODE)]],
                            values[headerMap[nameof(ORGANISM)]],
                            taxonomicStatus,
                            values[headerMap[nameof(COMMON)]] == "X",
                            values[headerMap[nameof(COMMON_COMMENSAL)]] == "X",
                            values[headerMap[nameof(ORGANISM_TYPE)]],
                            values[headerMap[nameof(ANAEROBE)]] == "X",
                            values[headerMap[nameof(MORPHOLOGY)]],
                            values[headerMap[nameof(SUBKINGDOM_CODE)]],
                            values[headerMap[nameof(FAMILY_CODE)]],
                            values[headerMap[nameof(GENUS_GROUP)]],
                            values[headerMap[nameof(GENUS_CODE)]],
                            values[headerMap[nameof(SPECIES_GROUP)]],
                            values[headerMap[nameof(SEROVAR_GROUP)]],
                            values[headerMap[nameof(SCT_CODE)]],
                            values[headerMap[nameof(SCT_TEXT)]],
                            values[headerMap[nameof(GBIF_TAXON_ID)]],
                            values[headerMap[nameof(GBIF_DATASET_ID)]],
                            values[headerMap[nameof(GBIF_TAXONOMIC_STATUS)]],
                            values[headerMap[nameof(KINGDOM)]],
                            values[headerMap[nameof(PHYLUM)]],
                            values[headerMap[nameof(CLASS)]],
                            values[headerMap[nameof(ORDER)]],
                            values[headerMap[nameof(FAMILY)]],
                            values[headerMap[nameof(GENUS)]]);

                        allOrgs.Add(newOrganism);
                    }

                    return allOrgs;
                }
            }
            else throw new FileNotFoundException(organismsTableFile);
        }

        /// <summary>
        /// Load the full organism list into a dictionary with the organism code as the key.
        /// Note that only the "current" version of the code is used (when organisms have been
        /// renamed, the old names are retained as "old").
        /// </summary>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        private static Dictionary<string, Organism> LoadCurrentOrganisms()
        {
            Dictionary<string, Organism> currentOrgMap = new Dictionary<string, Organism>();

            foreach (Organism currentOrg in AllOrganisms.Where(o => o.TAXONOMIC_STATUS == CurrentOrgCode))
                currentOrgMap.Add(currentOrg.WHONET_ORG_CODE, currentOrg);

            return currentOrgMap;
        }

		/// <summary>
		/// Handles the situation where one or more deprecated organism codes have been merged into a new "organism" for taxonomic reasons.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="FileNotFoundException"></exception>
		private static Dictionary<string, string> LoadMergedOrganisms()
		{
			string organismsTableFile;
			string relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), "Resources", "Organisms.txt");
			if (string.IsNullOrWhiteSpace(Constants.SystemRootPath))
				organismsTableFile = relativePath;
			else
				organismsTableFile = string.Join(Path.DirectorySeparatorChar.ToString(), Constants.SystemRootPath, relativePath);

            if (File.Exists(organismsTableFile))
            {
                Dictionary<string, string> mergedOrganismMap = new Dictionary<string, string>();

                using (TextFieldParser parser = new TextFieldParser(organismsTableFile, System.Text.Encoding.UTF8))
                {
                    parser.SetDelimiters(Constants.Delimiters.TabChar.ToString());
                    parser.HasFieldsEnclosedInQuotes = true;

                    string[] headers = parser.ReadFields();
                    Dictionary<string, int> headerMap = IO_Library.GetResourceHeaders(headers);

                    while (!parser.EndOfData)
                    {
                        string[] values = parser.ReadFields();

                        string taxonomicStatus = values[headerMap[nameof(TAXONOMIC_STATUS)]];

                        string oldOrgCode = values[headerMap[nameof(WHONET_ORG_CODE)]];
                        string newOrganismCode = values[headerMap[ReplacedByColumnName]];

                        if (taxonomicStatus != CurrentOrgCode && !string.IsNullOrEmpty(newOrganismCode) && !mergedOrganismMap.ContainsKey(oldOrgCode))
                            mergedOrganismMap.Add(oldOrgCode, newOrganismCode);
                    }
                }

                return mergedOrganismMap;
            }
            else throw new FileNotFoundException(organismsTableFile);
        }

        #endregion
    }
}
