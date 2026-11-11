-- Find the expected resistance phenotypes (intrinsic resistance) rules for the organism specified in the WHERE clause.
-- The organism might only match at a higher level. For example, a rule applying to all Enterobacterales should be returned
-- when the isolate's organism is a Salmonella sp.
SELECT DISTINCT i.*
FROM Organisms o
	INNER JOIN (
		SELECT *,
			-- This would need to be expanded if we needed to support more than two exceptions for a single rule.
			-- In C# it will be implemented to handle an arbitrary number of items.
			-- Tried a CTE here, but couldn't get EXCEPTION_ORGANISM_CODE to feed into the base case of the recurrsion.
			substr(EXCEPTION_ORGANISM_CODE, 1, instr(EXCEPTION_ORGANISM_CODE, ',') - 1)  AS [FirstException], 
			substr(EXCEPTION_ORGANISM_CODE, instr(EXCEPTION_ORGANISM_CODE, ',') + 1) AS [SecondException]
		FROM ExpectedResistancePhenotypes
	) i
		ON (
			o.SEROVAR_GROUP IS NOT NULL
			AND i.ORGANISM_CODE_TYPE = 'SEROVAR_GROUP'
			AND o.SEROVAR_GROUP = i.ORGANISM_CODE
		) OR (
			i.ORGANISM_CODE_TYPE = 'WHONET_ORG_CODE'
			AND o.WHONET_ORG_CODE = i.ORGANISM_CODE
		) OR (
			o.SPECIES_GROUP IS NOT NULL
			AND i.ORGANISM_CODE_TYPE = 'SPECIES_GROUP'
			AND o.SPECIES_GROUP = i.ORGANISM_CODE
		) OR (
			o.GENUS_CODE IS NOT NULL
			AND i.ORGANISM_CODE_TYPE = 'GENUS_CODE'
			AND o.GENUS_CODE = i.ORGANISM_CODE
		) OR (
			o.GENUS_GROUP IS NOT NULL
			AND i.ORGANISM_CODE_TYPE = 'GENUS_GROUP'
			AND o.GENUS_GROUP = i.ORGANISM_CODE
		) OR (
			o.FAMILY_CODE IS NOT NULL
			AND i.ORGANISM_CODE_TYPE = 'FAMILY_CODE'
			AND o.FAMILY_CODE = i.ORGANISM_CODE
		) OR (
			o.SUBKINGDOM_CODE IS NOT NULL
			AND i.ORGANISM_CODE_TYPE = 'SUBKINGDOM_CODE'
			AND o.SUBKINGDOM_CODE = i.ORGANISM_CODE
		) OR (
			o.ANAEROBE = 'X'
			AND i.ORGANISM_CODE_TYPE = 'ANAEROBE+SUBKINGDOM_CODE'
			AND ((
					o.SUBKINGDOM_CODE = '+'
					AND i.ORGANISM_CODE = 'AN+'
				) OR (
					o.SUBKINGDOM_CODE = '-'
					AND i.ORGANISM_CODE = 'AN-'
			))			
		) OR (
			o.ANAEROBE = 'X'
			AND i.ORGANISM_CODE_TYPE = 'ANAEROBE'
			AND i.ORGANISM_CODE = 'ANA'
		)
	INNER JOIN Antibiotics a
		ON (
				i.ABX_CODE_TYPE = 'ATC_CODE'
				AND substr(a.ATC_CODE, 1, length(i.ABX_CODE)) = i.ABX_CODE
		) OR (
				i.ABX_CODE_TYPE = 'WHONET_ABX_CODE'
				AND i.ABX_CODE = a.WHONET_ABX_CODE
		)
WHERE o.WHONET_ORG_CODE = 'efa'
	AND o.TAXONOMIC_STATUS = 'C'
--	AND i.ABX_CODE = 'VAN'
	AND (
			-- Organism exceptions to the intrinsic rule, if applicable.
			coalesce(i.EXCEPTION_ORGANISM_CODE, '') = ''
			OR NOT (
				(
					o.SEROVAR_GROUP IS NOT NULL
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'SEROVAR_GROUP'
					AND (
						o.SEROVAR_GROUP = i.FirstException
						OR o.SEROVAR_GROUP = i.SecondException
					)
				) OR (
					i.EXCEPTION_ORGANISM_CODE_TYPE = 'WHONET_ORG_CODE'
					AND (
						o.WHONET_ORG_CODE = i.FirstException
						OR o.WHONET_ORG_CODE = i.SecondException
					)
				) OR (
					o.SPECIES_GROUP IS NOT NULL
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'SPECIES_GROUP'
					AND (
						o.SPECIES_GROUP = i.FirstException
						OR o.SPECIES_GROUP = i.SecondException
					)
				) OR (
					o.GENUS_CODE IS NOT NULL
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'GENUS_CODE'
					AND (
						o.GENUS_CODE = i.FirstException
						OR o.GENUS_CODE = i.SecondException
					)							
				) OR (
					o.GENUS_GROUP IS NOT NULL
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'GENUS_GROUP'
					AND (
						o.GENUS_GROUP = i.FirstException
						OR o.GENUS_GROUP = i.SecondException
					)
				) OR (
					o.FAMILY_CODE IS NOT NULL
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'FAMILY_CODE'
					AND (
						o.FAMILY_CODE = i.FirstException
						OR o.FAMILY_CODE = i.SecondException
					)
				) OR (
					o.SUBKINGDOM_CODE IS NOT NULL
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'SUBKINGDOM_CODE'
					AND (
						o.SUBKINGDOM_CODE = i.FirstException
						OR o.SUBKINGDOM_CODE = i.SecondException
					)
				) OR (
					o.ANAEROBE = 'X'
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'ANAEROBE+SUBKINGDOM_CODE'
					AND ((
							o.SUBKINGDOM_CODE = '+'
							AND (
								i.FirstException = 'AN+'
								OR i.SecondException = 'AN+'
							)
						) OR (
							o.SUBKINGDOM_CODE = '-'
							AND (
								i.FirstException = 'AN-'
								OR i.SecondException = 'AN-'
							)
					))			
				) OR (
					o.ANAEROBE = 'X'
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'ANAEROBE'
					AND (
						i.FirstException = 'ANA'
						OR i.SecondException = 'ANA'
					)
				)
			)
		)
	AND (
		-- Check for antibiotic exceptions to this rule.
		-- Simpler than organism exclusions because we don't have to worry about the code type.
		coalesce(i.ANTIBIOTIC_EXCEPTIONS, '') = ''
		OR instr(i.ANTIBIOTIC_EXCEPTIONS, a.WHONET_ABX_CODE) = 0
	)
ORDER BY i.GUIDELINE ASC,
	(
		CASE i.ORGANISM_CODE_TYPE
		WHEN 'SEROVAR_GROUP' THEN 1
		WHEN 'WHONET_ORG_CODE' THEN 2
		WHEN 'SPECIES_GROUP' THEN 3
		WHEN 'GENUS_CODE' THEN 4
		WHEN 'GENUS_GROUP' THEN 5
		WHEN 'FAMILY_CODE' THEN 6
		WHEN 'SUBKINGDOM_CODE' THEN 7
		WHEN 'ANAEROBE+SUBKINGDOM_CODE' THEN 8
		WHEN 'ANAEROBE' THEN 9
		ELSE 10
		END
	) ASC,
	(
		CASE i.ABX_CODE_TYPE
		WHEN 'WHONET_ABX_CODE' THEN 1
		WHEN 'ATC_CODE' THEN 2
		ELSE 3
		END
	) ASC,
	i.ABX_CODE ASC,
	a.WHONET_ABX_CODE ASC